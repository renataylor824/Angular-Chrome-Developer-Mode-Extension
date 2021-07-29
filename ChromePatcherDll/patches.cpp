#include "stdafx.h"
#include "patches.hpp"

#define ReadVar(variable) file.read(reinterpret_cast<char*>(&variable), sizeof(variable)); // Makes everything easier to read

namespace ChromePatch {
	ReadPatchResult Patches::ReadPatchFile() {
		static const unsigned int FILE_HEADER = 0xCE161D6E; // Magic values
		static const unsigned int PATCH_HEADER = 0x8A7C5000;
		ReadPatchResult result{};

		bool firstFile = true;
	RETRY_FILE_LABEL:
		std::ifstream file(firstFile ? "ChromePatches.bin" : "..\\ChromePatches.bin", std::ios::binary);
		file.unsetf(std::ios::skipws); // Disable skipping of leading whitespaces while reading

		if (!file.good()) {
			if (firstFile) {
				firstFile = false;
				goto RETRY_FILE_LABEL; // Retry once if the file was not found
			}

			throw std::exception("ChromePatches.bin file not found or not accessible");
		}

		unsigned int header = ReadUInteger(file);
		if (header != FILE_HEADER) {
			throw std::runtime_error("Invalid file: Wrong header " + std::to_string(header));
		}

		std::wstring dllPath = MultibyteToWide(ReadString(file));
		if (dllPath.compare(chromeDllPath) != 0) {
			result.UsingWrongVersion = true;
		}

		while (file.peek() != EOF) { // For each patch
			unsigned int patchHeader = ReadUInteger(file);
			if (patchHeader != PATCH_HEADER) {
				throw std::runtime_error("Invalid file: Wrong patch header " + std::to_string(patchHeader));
			}

			std::vector<PatchPattern> patterns;
			int patternsSize;
			ReadVar(patternsSize);
			for (int i = 0; i < patternsSize; i++) { // For each pattern, read its values
				int patternLength;
				ReadVar(patternLength);
				std::vector<byte> pattern;

				for (int i = 0; i < patternLength; i++) {
					byte patternByte;
					ReadVar(patternByte);
					pattern.push_back(patternByte);
				}

				patterns.push_back(PatchPattern{ pattern });
			}

			int offsetCount; // Read the Offsets list
			ReadVar(offsetCount);
			std::vector<int> offsets;
			for (int i = 0; i < offsetCount; i++) {
				int offset;
				ReadVar(offset);
				offsets.push_back(offset);
			}

			int newBytesCount; // Read the NewBytes array
			ReadVar(newBytesCount);
			std::vector<byte> newBytes;
			for(int i = 0; i < newBytesCount; i++) {
				byte newByte;
				ReadVar(newByte);
				newBytes.push_back(newByte);
			}

			int sigOffset; // Read the rest of the data
			byte origByte, patchByte, isSig;
			ReadVar(origByte);
			ReadVar(patchByte);
			ReadVar(isSig);
			ReadVar(sigOffset);

			Patch patch{ patterns, origByte, patchByte, offsets, newBytes, isSig > 0, sigOffset };
			patches.push_back(patch);
			
			std::cout << "Loaded patch: " << patterns[0].pattern.size() << " " << (int)origByte << " " << (int)patchByte << " " << offsets[0] << std::endl;
		}

		file.close();
		return result;
	}

	// Convert a UTF8 string to a UTF16LE string
	std::wstring Patches::MultibyteToWide(const std::string& str) {
		if (str.empty()) {
			return std::wstring();
		}

		int len = MultiByteToWideChar(CP_UTF8, NULL, str.c_str(), str.length(), NULL, 0);
		std::wstring result(len, '\0');

		if (len > 0) {
			if (MultiByteToWideChar(CP_UTF8, NULL, str.c_str(), str.length(), result.data(), len)) {
				return result;
			}
		}

		return std::wstring();
	}

	std::string Patches::ReadString(std::ifstream& file) {
		int length;
		ReadVar(length);

		std::string str(length, '\0');
		file.read(str.data(), length);
		return str;
	}

	unsigned int Patches::ReadUInteger(std::ifstream& file) {
		unsigned int integer;
		ReadVar(integer);

		return _byteswap_ulong(integer); // Convert to Big Endian (for the magic values)
	}

	// TODO: Externalize this function in different implementations (traditional and with SIMD support) and add multithreading
	int Patches::ApplyPatches() {
		int successfulPatches = 0;
		std::cout << "Applying patches, please wait..." << std::endl;
		HANDLE proc = GetCurrentProcess();
		MODULEINFO chromeDllInfo;

		GetModuleInformation(proc, chromeDll, &chromeDllInfo, sizeof(chromeDllInfo));
		MEMORY_BASIC_INFORMATION mbi{};

		int patchedPatches = 0;
		for (uintptr_t i = (uintptr_t)chromeDll; i < (uintptr_t)chromeDll + (uintptr_t)chromeDllInfo.SizeOfImage; i++) {
			if (VirtualQuery((LPCVOID)i, &mbi, sizeof(mbi))) {
				if (mbi.Protect & (PAGE_GUARD | PAGE_NOCACHE | PAGE_NOACCESS) || !(mbi.State & MEM_COMMIT)) {
					i += mbi.RegionSize;	
				}
				else {
					for (uintptr_t addr = (uintptr_t)mbi.BaseAddress; addr < (uintptr_t)mbi.BaseAddress + (uintptr_t)mbi.RegionSize; addr++) {
						byte byt = *reinterpret_cast<byte*>(addr);

						for (Patch& patch : patches) {
							if (patch.finishedPatch) {
								continue;
							}

							for (PatchPattern& pattern : patch.patterns) {
								byte searchByte = pattern.pattern[pattern.searchOffset];
								if (searchByte == byt || searchByte == 0xFF) {
									pattern.searchOffset++;
								}
								else {
									pattern.searchOffset = 0;
								}

								__try {
									if (pattern.searchOffset == pattern.pattern.size()) {
										int offset = 0;
									RETRY_OFFSET_LABEL:
										uintptr_t patchAddr = addr - pattern.searchOffset + patch.offsets.at(offset) + 1;
										std::cout << "Reading address " << std::hex << patchAddr << std::endl;

										if (patch.isSig) { // Add the offset found at the patchAddr (with a 4 byte rel. addr. offset) to the patchAddr
											patchAddr += *(reinterpret_cast<std::int32_t*>(patchAddr)) + 4 + patch.sigOffset;
											std::cout << "New aftersig address: " << std::hex << patchAddr << std::endl;
										}

										byte* patchByte = reinterpret_cast<byte*>(patchAddr);

										if (*patchByte == patch.origByte || patch.origByte == 0xFF) {
											std::cout << "Patching byte " << (int)*patchByte << " to " << (int)patch.patchByte << " at " << std::hex << patchAddr << std::endl;

											DWORD oldProtect;
											VirtualProtect(mbi.BaseAddress, mbi.RegionSize, PAGE_EXECUTE_READWRITE, &oldProtect);

											if(patch.newBytes.empty()) { // Patch a single byte
												*patchByte = patch.patchByte;
											} else { // Write the newBytes array if it is filled instead
												const int newBytesSize = patch.newBytes.size();
												for(int newByteI = 0; newByteI < newBytesSize; newByteI++) {
													patchByte[newByteI] = patch.newBytes[newByteI];
												}

												std::cout << newBytesSize << " NewBytes have been written" << std::endl;
											}

											
											VirtualProtect(mbi.BaseAddress, mbi.RegionSize, oldProtect, &oldProtect);
											patch.successfulPatch = true;
										}
										else {
											offset++;
											if (offset != patch.offsets.size()) {
												goto RETRY_OFFSET_LABEL;
											}

											std::cerr << "Byte (" << (int)*patchByte << ") not original (" << (int)patch.origByte << ") at " << std::hex << patchAddr << std::endl;
										}

										pattern.searchOffset = -1;
										patch.finishedPatch = true;
										patchedPatches++;

										if (patchedPatches >= patches.size()) {
											goto END_PATCH_SEARCH_LABEL;
										}
										break;
									}
								}
								__except (EXCEPTION_EXECUTE_HANDLER) {
									std::cerr << "SEH error" << std::endl;
								}
							}
						}
					}

					i = (uintptr_t)mbi.BaseAddress + mbi.RegionSize;
				}
			}
		}

	END_PATCH_SEARCH_LABEL:
		for (Patch& patch : patches) {
			if (!patch.successfulPatch) {
				std::cerr << "Couldn't patch " << patch.patterns[0].pattern.size() << " " << patch.offsets[0] << std::endl;
			}
			else {
				successfulPatches++;
			}
		}

		CloseHandle(proc);
		return successfulPatches;
	}
}
