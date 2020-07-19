﻿using ChromeDevExtWarningPatcher.InstallationFinder;
using Microsoft.Win32;
using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ChromeDevExtWarningPatcher {
	public partial class PatcherGui : Window {

		public PatcherGui() {
			InitializeComponent();
		}

		private void SelectFolderBtn_Click(object sender, RoutedEventArgs e) {
			OpenFileDialog openFile = new OpenFileDialog();
			openFile.Title = "Select a chrome.dll";
			openFile.Filter = "chrome.dll/msedge.dll file (chrome.dll;msedge.dll)|chrome.dll;msedge.dll|Alternative chrome.dll file (*.dll)|*.dll|All files (*.*)|*.*";
			openFile.FilterIndex = 1;
			openFile.CheckFileExists = openFile.CheckPathExists = openFile.AddExtension = true;

			if (openFile.ShowDialog(this) == true) { // No, I am not a noob, I have to do it like this and further below
				string chromeDllPath = openFile.FileName;

				openFile = new OpenFileDialog();
				openFile.Title = "Select a chrome.exe";
				openFile.Filter = "Browser executable file (*.exe)|*.exe|All files (*.*)|*.*";
				openFile.FilterIndex = 1;
				openFile.CheckFileExists = openFile.CheckPathExists = openFile.AddExtension = true;
				openFile.InitialDirectory = Directory.GetParent(Path.GetDirectoryName(chromeDllPath)).FullName;

				if (openFile.ShowDialog(this) == true) {
					string chromeExePath = openFile.FileName;
					AddChromiumInstallation(new InstallationPaths(chromeDllPath, chromeExePath));
				}
			}
		}

		private void PatchBtn_Click(object sender, RoutedEventArgs e) {
			Program.bytePatchManager.DisabledGroups.Clear();
			foreach (CustomCheckBox patchBox in PatchGroupList.Items) {
				if (patchBox.IsChecked == false)
					Program.bytePatchManager.DisabledGroups.Add(patchBox.Group);
			}

			foreach (CheckBox installationBox in InstallationList.Items) {
				if (installationBox.IsChecked == true) {
					string path = installationBox.Content.ToString();

					new Thread(() => {
						try {
							DllPatcher patcher = new DllPatcher(path);
							if (patcher.Patch(Log)) {
								installationBox.Dispatcher.Invoke(new Action(() => {
									installationBox.Foreground = installationBox.BorderBrush = new SolidColorBrush(Color.FromRgb(72, 207, 133));
								}));
							}
						} catch (Exception ex) {
							Log("Error while patching " + path + ":" + ex.Message);
						}
					}).Start();
				}
			}
		}

		private void CopyBtn_Click(object sender, RoutedEventArgs e) {
			Clipboard.SetText(ConsoleBox.GetTotalTextRange().Text);
		}

		protected override void OnInitialized(EventArgs e) {
			base.OnInitialized(e);

			ConsoleBox.GetTotalTextRange().Text = "";

			Log("Patcher gui initialized");
			Log("Searching for Chromium installations...");

			foreach (InstallationPaths paths in new InstallationFinder.InstallationManager().FindAllChromiumInstallations()) {
				AddChromiumInstallation(paths);
			}

			foreach (GuiPatchGroupData patchGroup in Program.bytePatchManager.PatchGroups) {
				PatchGroupList.Items.Add(new CustomCheckBox(patchGroup));
			}
		}

		public void Log(string str) {
			ConsoleBox.Dispatcher.Invoke(new Action(() => {
				Paragraph logParagraph = new Paragraph();
				logParagraph.Inlines.Add(str);

				ConsoleBox.Document.Blocks.Add(logParagraph);
				ConsoleBox.ScrollToEnd();
			}));
		}

		private void AddChromiumInstallation(InstallationPaths chromePaths) {
			CustomCheckBox installationBox = new CustomCheckBox(chromePaths.ChromeDllPath);
			installationBox.IsChecked = true;
			installationBox.ToolTip = chromePaths.ChromeDllPath + " & " + chromePaths.ChromeExePath;

			InstallationList.Items.Add(installationBox);
			Log("Added Chromium installation at " + chromePaths.ChromeDllPath);
		}
	}

	public static class RichTextBoxExtensions {
		public static TextRange GetTotalTextRange(this RichTextBox richTextBox) {
			return new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);
		}
	}
}
