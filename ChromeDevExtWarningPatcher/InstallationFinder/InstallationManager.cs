﻿using ChromeDevExtWarningPatcher.InstallationFinder.Defaults;
using System;
using System.Collections.Generic;
using System.IO;

namespace ChromeDevExtWarningPatcher.InstallationFinder {
	public class InstallationManager {
		private readonly List<Installation> installationFinders = new List<Installation>();

		public InstallationManager() {
			this.installationFinders.Clear();
			this.installationFinders.Add(new Chrome());
			this.installationFinders.Add(new Brave());
			this.installationFinders.Add(new Edge());
			this.installationFinders.Add(new Yandex());
		}

		public List<InstallationPaths> FindAllChromiumInstallations() {
			List<InstallationPaths> installations = new List<InstallationPaths>();

			foreach (Installation installation in this.installationFinders) {
				foreach (InstallationPaths paths in installation.FindInstallationPaths()) {
					if (paths.Is64Bit()) { // Force x64 installations
						installations.Add(paths);
					}
				}
			}

			return installations;
		}

		// Taken from https://stackoverflow.com/questions/480696/how-to-find-if-a-native-dll-file-is-compiled-as-x64-or-x86
		public static bool IsImageX64(string peFilePath) {
			using FileStream stream = new FileStream(peFilePath, FileMode.Open, FileAccess.Read);
			using BinaryReader reader = new BinaryReader(stream);

			//check the MZ signature to ensure it's a valid Portable Executable image
			if (reader.ReadUInt16() != 23117) {
				throw new BadImageFormatException("Not a valid Portable Executable image", peFilePath);
			}

			// seek to, and read, e_lfanew then advance the stream to there (start of NT header)
			stream.Seek(0x3A, SeekOrigin.Current);
			stream.Seek(reader.ReadUInt32(), SeekOrigin.Begin);

			// Ensure the NT header is valid by checking the "PE\0\0" signature
			if (reader.ReadUInt32() != 17744) {
				throw new BadImageFormatException("Not a valid Portable Executable image", peFilePath);
			}

			// seek past the file header, then read the magic number from the optional header
			stream.Seek(20, SeekOrigin.Current);
			ushort magicByte = reader.ReadUInt16();
			return magicByte == 0x20B;
		}
	}
}
