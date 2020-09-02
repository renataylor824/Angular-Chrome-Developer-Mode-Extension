#pragma once

namespace ChromePatch {
	struct ReadPatchResult {
		bool UsingWrongVersion{};
	};

	struct PatchPattern {
		std::vector<byte> pattern{};
		int searchOffset{};
	};

	struct Patch {
		std::vector<PatchPattern> patterns{};
		byte origByte{}, patchByte{};
		std::vector<int> offsets{};
		bool isSig{};
		int sigOffset{};
		bool finishedPatch{}, successfulPatch{};
	};

	class Patches {
	public:
		HMODULE chromeDll{};
		std::wstring chromeDllPath{};
		std::vector<Patch> patches{};

		ReadPatchResult ReadPatchFile();
		int ApplyPatches();
	private:
		std::wstring MultibyteToWide(const std::string& str);
		std::string ReadString(std::ifstream& stream);
		unsigned int ReadUInteger(std::ifstream& stream);
	};

	inline Patches patches;
}