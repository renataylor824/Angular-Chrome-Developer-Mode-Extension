﻿using ChromeDevExtWarningPatcher.InstallationFinder;
using ChromeDevExtWarningPatcher.Patches;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using Task = Microsoft.Win32.TaskScheduler.Task;

namespace ChromeDevExtWarningPatcher {
	internal class PatcherInstaller {
		private static readonly uint FILE_HEADER = BitConverter.ToUInt32(BitConverter.GetBytes(0xCE161D6E).Reverse().ToArray(), 0);
		private static readonly uint PATCH_HEADER = BitConverter.ToUInt32(BitConverter.GetBytes(0x8A7C5000).Reverse().ToArray(), 0); // Reverse because of wrong endianess

		[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		private static extern bool SendNotifyMessage(IntPtr hWnd, uint msg, UIntPtr wParam,
   IntPtr lParam);

		private readonly List<InstallationPaths> installationPaths;

		public PatcherInstaller(List<InstallationPaths> installationPaths) {
			this.installationPaths = installationPaths;
		}

		private static byte[] GetPatchFileBinary(InstallationPaths paths, List<int> disabledGroups) {
			MemoryStream stream = new MemoryStream();
			BinaryWriter writer = new BinaryWriter(stream);

			writer.Write(FILE_HEADER);
			writer.Write(paths.ChromeDllPath!.Length); // Needed, so it takes 4 bytes
			writer.Write(Encoding.ASCII.GetBytes(paths.ChromeDllPath));

			foreach (BytePatch patch in App.BytePatchManager!.BytePatches) {
				if (disabledGroups.Contains(patch.Group)) {
					continue;
				}

				writer.Write(PATCH_HEADER);
				writer.Write(patch.Pattern.AlternativePatternsX64.Count);

				foreach (byte[] pattern in patch.Pattern.AlternativePatternsX64) { // Write all possible patterns
					writer.Write(pattern.Length);
					writer.Write(pattern);
				}

				writer.Write(patch.Offsets.Count);
				foreach (int offset in patch.Offsets) {
					writer.Write(offset);
				}

				writer.Write(patch.OrigByte);
				writer.Write(patch.PatchByte);
				writer.Write(patch.IsSig);
				writer.Write(patch.SigOffset);
			}

			writer.Close();
			byte[] data = stream.ToArray();
			stream.Close();
			return data;
		}

		private static RegistryKey OpenExesKey() {
			return Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Ceiridge\ChromePatcher\ChromeExes");
		}

		private static DirectoryInfo GetProgramsFolder() {
			return new DirectoryInfo(Path.Combine(Environment.ExpandEnvironmentVariables("%ProgramW6432%"), "Ceiridge", "ChromeDllInjector"));
		}

		private static void TryDeletePatcherDlls(DirectoryInfo folder) {
			foreach (FileInfo file in folder.EnumerateFiles()) {
				if (file.Name.EndsWith(".dll") && file.Name.Contains("ChromePatcherDll_")) {
					try {
						file.Delete();
					} catch (Exception) {
						// Ignore
					}
				}
			}
		}

		// This is needed to ignore deletion errors
		private static void DeleteFolderRecursively(DirectoryInfo folder) {
			foreach (FileInfo file in folder.EnumerateFiles()) {
				try {
					file.Delete();
				} catch (Exception) {
					// Ignore
				}
			}

			foreach (DirectoryInfo directory in folder.EnumerateDirectories()) {
				DeleteFolderRecursively(directory);
			}

			try {
				folder.Delete(false);
			} catch (Exception) {
				// Ignore
			}
		}

		public delegate void WriteToLog(string str);
		public bool Install(WriteToLog log, List<int> disabledGroups) {
			using (TaskService ts = new TaskService()) {
				foreach (Task task in ts.RootFolder.Tasks) {
					if (task.Name.Equals("ChromeDllInjector")) {
						if (task.State == TaskState.Running) {
							task.Stop();
						}
					}
				}
			}
			SendNotifyMessage(new IntPtr(0xFFFF), 0x0, UIntPtr.Zero, IntPtr.Zero); // HWND_BROADCAST a WM_NULL to make sure every injected dll unloads
			Thread.Sleep(1000); // Give Windows some time to unload all patch dlls

			using (RegistryKey dllPathKeys = OpenExesKey()) {
				foreach (string valueName in dllPathKeys.GetValueNames()) {
					if (valueName.Length > 0) {
						dllPathKeys.DeleteValue(valueName);
					}
				}
				log("Cleared ChromeExes registry");

				int i = 0;
				foreach (InstallationPaths paths in this.installationPaths) {
					string appDir = Path.GetDirectoryName(paths.ChromeExePath!)!;

					// Write patch data info file
					byte[] patchData = GetPatchFileBinary(paths, disabledGroups);
					File.WriteAllBytes(Path.Combine(appDir, "ChromePatches.bin"), patchData);
					log("Wrote patch file to " + appDir);

					// Add chrome dll to the registry key
					dllPathKeys.SetValue(i.ToString(), paths.ChromeExePath!);
					i++;
					log("Appended " + paths.ChromeDllPath + " to the registry key");
				}
			}

			// Write the injector to "Program Files"
			DirectoryInfo programsFolder = GetProgramsFolder();
			string patcherDllPath = Path.Combine(programsFolder.FullName, "ChromePatcherDll_" + Installation.GetUnixTime() + ".dll"); // Unique dll to prevent file locks

			if (!programsFolder.Exists) {
				Directory.CreateDirectory(programsFolder.FullName); // Also creates all subdirectories
			}

			using (ZipArchive zip = new ZipArchive(new MemoryStream(Properties.Resources.ChromeDllInjector), ZipArchiveMode.Read)) {
				foreach (ZipArchiveEntry entry in zip.Entries) {
					string combinedPath = Path.Combine(programsFolder.FullName, entry.FullName);
					new FileInfo(combinedPath).Directory?.Create(); // Create the necessary folders that contain the file

					using Stream entryStream = entry.Open();
					using FileStream writeStream = File.OpenWrite(combinedPath);
					entryStream.CopyTo(writeStream);
				}
			}
			log("Extracted injector zip");

			TryDeletePatcherDlls(programsFolder);
			File.WriteAllBytes(patcherDllPath, Properties.Resources.ChromePatcherDll);
			log("Wrote patcher dll to " + patcherDllPath);

			using (TaskService ts = new TaskService()) {
				TaskDefinition task = ts.NewTask();

				task.RegistrationInfo.Author = "Ceiridge";
				task.RegistrationInfo.Description = "A logon task that automatically injects the ChromePatcher.dll into every Chromium process that the user installed the patcher on. This makes removing the Developer Mode Extension Warning possible.";
				task.RegistrationInfo.URI = "https://github.com/Ceiridge/Chrome-Developer-Mode-Extension-Warning-Patcher";

				task.Triggers.Add(new LogonTrigger { });
				task.Actions.Add(new ExecAction(Path.Combine(programsFolder.FullName, "ChromeDllInjector.exe")));

				task.Principal.RunLevel = TaskRunLevel.Highest;
				task.Principal.LogonType = TaskLogonType.InteractiveToken;

				task.Settings.StopIfGoingOnBatteries = task.Settings.DisallowStartIfOnBatteries = false;
				task.Settings.RestartCount = 3;
				task.Settings.RestartInterval = new TimeSpan(0, 1, 0);
				task.Settings.Hidden = true;
				task.Settings.ExecutionTimeLimit = task.Settings.DeleteExpiredTaskAfter = TimeSpan.Zero;

				Task tsk = ts.RootFolder.RegisterTaskDefinition("ChromeDllInjector", task, TaskCreation.CreateOrUpdate, WindowsIdentity.GetCurrent().Name, null, TaskLogonType.InteractiveToken);

				if (tsk.State != TaskState.Running) {
					tsk.Run();
				}
				log("Created and started task");
			}

			log("Patches installed!");
			return true;
		}

		public bool UninstallAll(WriteToLog log) {
			using (TaskService ts = new TaskService()) {
				foreach (Task task in ts.RootFolder.Tasks) {
					if (task.Name.Equals("ChromeDllInjector")) {
						if (task.State == TaskState.Running) {
							task.Stop();
						}

						ts.RootFolder.DeleteTask("ChromeDllInjector");
						log("Deleted and stopped task");
					}
				}
			}
			SendNotifyMessage(new IntPtr(0xFFFF), 0x0, UIntPtr.Zero, IntPtr.Zero); // HWND_BROADCAST a WM_NULL to make sure every injected dll unloads
			Thread.Sleep(1000); // Give Windows some time to unload all patch dlls

			using (RegistryKey dllPathKeys = OpenExesKey()) {
				foreach (string valueName in dllPathKeys.GetValueNames()) {
					if (valueName.Length > 0) {
						dllPathKeys.DeleteValue(valueName);
					}
				}
				log("Cleared ChromeExes registry");
			}

			foreach (InstallationPaths paths in this.installationPaths) {
				string appDir = Path.GetDirectoryName(paths.ChromeExePath!)!;
				string patchesFile = Path.Combine(appDir, "ChromePatches.bin");

				if (File.Exists(patchesFile)) {
					File.Delete(patchesFile);
				}

				log("Deleted patch files for " + appDir);
			}

			DirectoryInfo programsFolder = GetProgramsFolder();
			if (programsFolder.Exists) {
				TryDeletePatcherDlls(programsFolder);
				DeleteFolderRecursively(programsFolder);
				log("(Mostly) deleted " + programsFolder.FullName);
			}

			log("Patcher uninstalled!");
			return true;
		}
	}
}
