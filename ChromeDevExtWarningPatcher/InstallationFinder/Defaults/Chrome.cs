﻿using System.Collections.Generic;
using System.IO;

namespace ChromeDevExtWarningPatcher.InstallationFinder.Defaults {
	internal class Chrome : Installation {
		public Chrome() : base("Chrome") { }

		public override List<InstallationPaths> FindInstallationPaths() {
			List<InstallationPaths> dllFiles = new List<InstallationPaths>();

			AddDllAndExeToList(dllFiles, this.GetLatestDllAndExe(new DirectoryInfo(@"C:\Program Files (x86)\Google\Chrome\Application"), "chrome.dll", "chrome.exe"));
			AddDllAndExeToList(dllFiles, this.GetLatestDllAndExe(new DirectoryInfo(@"C:\Program Files (x86)\Google\Chrome Beta\Application"), "chrome.dll", "chrome.exe"));

			AddDllAndExeToList(dllFiles, this.GetLatestDllAndExe(new DirectoryInfo(@"C:\Program Files\Google\Chrome\Application"), "chrome.dll", "chrome.exe"));
			AddDllAndExeToList(dllFiles, this.GetLatestDllAndExe(new DirectoryInfo(@"C:\Program Files\Google\Chrome Beta\Application"), "chrome.dll", "chrome.exe"));

			return dllFiles;
		}
	}
}
