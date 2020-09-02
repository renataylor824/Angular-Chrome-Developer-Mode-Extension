#include "stdafx.h"
#include "patches.hpp"
#include "threads.hpp"

using MessageBoxTimeoutWType = int(__stdcall*)(HWND hWnd, LPCWSTR lpText, LPCWSTR lpCaption, UINT uType, WORD wLanguageId, DWORD dwMilliseconds);

// Most of the code here is quite useless (it comes from older version that used other methods of injecting), but won't be removed (too lazy) (e. g. checking if it is Chrome)

BOOL APIENTRY ThreadMain(LPVOID lpModule) {
	FILE* fout = nullptr;
	FILE* ferr = nullptr;
#ifdef _DEBUG
	if (AllocConsole()) {
		freopen_s(&fout, "CONOUT$", "w", stdout);
		freopen_s(&ferr, "CONOUT$", "w", stderr);
	}
#else
	char winDir[MAX_PATH];
	GetWindowsDirectoryA(winDir, MAX_PATH);
	strcat_s(winDir, "\\Temp\\ChromePatcherDll.log"); // A relative path can't be used, because it's not writable without admin rights
	char winDirErr[MAX_PATH];
	GetWindowsDirectoryA(winDirErr, MAX_PATH);
	strcat_s(winDirErr, "\\Temp\\ChromePatcherDllErr.log"); // A relative path can't be used, because it's not writable without admin rights

	if (fopen_s(&fout, winDir, "a") == 0) {
		fclose(fout);
		freopen_s(&fout, winDir, "a", stdout);
	}
	if (fopen_s(&ferr, winDirErr, "a") == 0) {
		fclose(ferr);
		freopen_s(&ferr, winDirErr, "a", stderr);
	}
#endif
	std::cout << std::time(0) << std::endl; // Log time for debug purposes
	std::cerr << std::time(0) << std::endl;

	std::wstring mutexStr = std::wstring(L"ChromeDllMutex") + std::to_wstring(GetCurrentProcessId());
	HANDLE mutex = OpenMutex(MUTEX_ALL_ACCESS, FALSE, mutexStr.c_str()); // Never allow the dll to be injected twice
	if (mutex) {
		return TRUE;
	}
	else {
		mutex = CreateMutex(0, FALSE, mutexStr.c_str()); // Mutex closes automatically after the process exits
	}

	HANDLE proc = GetCurrentProcess();

	bool hasFoundChrome = false;
	int attempts = 0;
	std::wregex chromeDllRegex(L"\\\\Application\\\\(?:\\d+?\\.?)+\\\\[a-zA-Z0-9-]+\\.dll");

	while (!hasFoundChrome && attempts < 30000) { // Give it some attempts to find the chrome.dll module
		HMODULE modules[1024];
		DWORD cbNeeded;

		if (EnumProcessModules(proc, modules, sizeof(modules), &cbNeeded)) {
			for (int i = 0; i < (cbNeeded / sizeof(HMODULE)); i++) {
				HMODULE mod = modules[i];
				TCHAR modulePath[1024];

				if (GetModuleFileName(mod, modulePath, ARRAYSIZE(modulePath))) { // analyze the module's file path with regex
					if (std::regex_search(modulePath, chromeDllRegex)) {
						std::wcout << L"Found chrome.dll module: " << modulePath << L" with handle: " << mod << std::endl;
						
						ChromePatch::patches.chromeDll = mod;
						ChromePatch::patches.chromeDllPath = modulePath;
						hasFoundChrome = true;
					}
				}
			}
		}
		else {
			std::cerr << "Couldn't enumerate modules" << std::endl;
		}

		Sleep(1);
		attempts++;
	}
	ChromePatch::SuspendOtherThreads();
	CloseHandle(proc);

	int successfulPatches = -1;
	if (!hasFoundChrome) {
		std::cerr << "Couldn't find the chrome.dll, exiting" << std::endl;
	}
	else {
		try {
			ChromePatch::ReadPatchResult readResult = ChromePatch::patches.ReadPatchFile();
			static MessageBoxTimeoutWType messageBoxTimeoutW = (MessageBoxTimeoutWType)GetProcAddress(LoadLibrary(L"user32.dll"), "MessageBoxTimeoutW"); // Undocumented WinAPI function
			std::vector<std::thread> messageBoxThreads;

			if (readResult.UsingWrongVersion) { // Start new thread to allow patching in the background
				messageBoxThreads.push_back(std::thread(messageBoxTimeoutW, nullptr, L"Your Chromium version is newer than the patch definition's version! Please reuse the patcher to prevent bugs or failing pattern searches!", L"Outdated patch definitions!", MB_OK | MB_TOPMOST | MB_SETFOREGROUND, 0, 1000));
			}

			successfulPatches = ChromePatch::patches.ApplyPatches();

			for (std::thread& thread : messageBoxThreads) {
				thread.join(); // Necessary to prevent crashes because of std::terminate
			}
		}
		catch (const std::exception& ex) {
			std::cerr << "Error: " << ex.what() << std::endl;
		}
	}

	std::cout << "Unloading patcher dll and resuming threads" << std::endl;
	ChromePatch::ResumeOtherThreads();

	if (successfulPatches == 0) { // 0 if literally no patches were applied, but not -1 because it shows that it at least tried
		std::cerr << "Trying to reapply patches with running threads after a total failure..." << std::endl;

		try {
			ChromePatch::patches.ApplyPatches();
		}
		catch (const std::exception& ex) {
			std::cerr << "Error: " << ex.what() << std::endl;
		}
	}

	if (fout != nullptr)
		fclose(fout);
	if (ferr != nullptr)
		fclose(ferr);
#ifdef _DEBUG
	Sleep(5000); // Give the user some time to read
	FreeConsole();
#endif

	return TRUE;
}


BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved) {
	switch (ul_reason_for_call) {
		case DLL_PROCESS_ATTACH: {
			module = hModule;
			DisableThreadLibraryCalls(module);
			std::wstring cmdLine = GetCommandLine();

			WCHAR _exePath[1024];
			GetModuleFileNameW(NULL, _exePath, 1024);
			std::wstring exePath(_exePath);

			if (exePath.find(L"ChromeDllInjector.exe") != std::wstring::npos || cmdLine.find(L"--type=") != std::wstring::npos) { // Ignore the injector's process, but stay loaded & Ignore Chrome's subprocesses
				return TRUE;
			}

			HKEY hkey;
			DWORD dispo;
			bool isChromeExe = false;
			if (RegCreateKeyEx(HKEY_LOCAL_MACHINE, L"SOFTWARE\\Ceiridge\\ChromePatcher\\ChromeExes", NULL, NULL, NULL, KEY_READ | KEY_QUERY_VALUE, NULL, &hkey, &dispo) == ERROR_SUCCESS) { // Only continue with the DLL in Chrome processes
				WCHAR valueName[64];
				DWORD valueNameLength = 64, valueType, dwIndex = 0;
				WCHAR value[1024];
				DWORD valueLength = 1024;

				LSTATUS STATUS;
				while (STATUS = RegEnumValue(hkey, dwIndex, valueName, &valueNameLength, NULL, &valueType, (LPBYTE)&value, &valueLength) == ERROR_SUCCESS) {
					if (valueType == REG_SZ) {
						std::wstring path = value;

						if (path.compare(exePath) == 0) {
							isChromeExe = true;
							break;
						}
					}

					valueLength = 1024;
					valueNameLength = 64;
					dwIndex++;
				}

				RegCloseKey(hkey);
			}
			else {
				std::cerr << "Couldn't open regkey\n";
				return TRUE;
			}

			if (!isChromeExe) {
				return TRUE;
			}

			ChromePatch::mainThreadHandle = CreateThread(NULL, NULL, (LPTHREAD_START_ROUTINE)ThreadMain, hModule, NULL, NULL);
			break;
		}
		case DLL_THREAD_ATTACH:
			break;
		case DLL_THREAD_DETACH:
			break;
		case DLL_PROCESS_DETACH:
			break;
	}

	return TRUE;
}
