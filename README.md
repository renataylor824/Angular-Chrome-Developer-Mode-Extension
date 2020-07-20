# Disable Chromium's and Chrome's Developer Mode Extension Warning Popup & Elision WWW/HTTPS Hiding & Debugging Extension Popup
**Download** it in the [release section](https://github.com/Ceiridge/Chrome-Developer-Mode-Extension-Warning-Patcher/releases). The patterns and patches auto-update with the `patterns.xml`.

## Supported browsers
See below for the custom paths (commandline option).
```javascript
- All x64 and x86/x32 bit Chromium-based browsers, including:
- Chrome ✓
- Chromium
- Brave ✓
- New Edge ✓
- Opera?
- Vivaldi
- Blisk
- Colibri
- Epic Browser
- Iron Browser
- Ungoogled Chromium?
```

## Gui Screenshot
![Gui Screenshot](https://raw.githubusercontent.com/Ceiridge/Chrome-Developer-Mode-Extension-Warning-Patcher/master/media/guiscreenshot.png)

## Commandline Options
All commandline options are **optional** and not required. If none are given, the gui will start.

```
ChromeDevExtWarningPatcher.exe 
  --groups           Set what patch groups you want to use. See patterns.xml to get the group ids (comma-seperated: 0,1,2,etc.)

  -w, --noWait       Disable the almost-pointless wait after finishing

  --customPath       Instead of automatically detecting and patching all chrome.exe files, define a custom Application-folder path
                     (see README) (string in quotes is recommended)

  --help             Display this help screen.

  --version          Display version information.
```

**Recommended `customPath`s:**
```java
Chrome (default): "C:\Program Files (x86)\Google\Chrome\Application"
Brave: "C:\Program Files (x86)\BraveSoftware\Brave-Browser\Application"
Edge: "C:\Program Files (x86)\Microsoft\Edge\Application"

Remember: The path always needs to include the latest version folder of the browser (e. g. 83.0.1123.123).
Create a new issue with a path, if you want to contribute to this list.
```
Find more [here](https://github.com/Ceiridge/Chrome-Developer-Mode-Extension-Warning-Patcher/tree/master/ChromeDevExtWarningPatcher/InstallationFinder/Defaults).

## What can it patch

Read the [patterns.xml](https://github.com/Ceiridge/Chrome-Developer-Mode-Extension-Warning-Patcher/blob/master/patterns.xml) file for more information.
- Remove extension warning (Removes the warning => main purpose of the patcher)
- Remove debugging warning (Removes warning when using chrome.debugger in extensions)
- Disable Elision (Force showing WWW and HTTPS in the url bar/omnibar)
- Remove crash warning (Remove the "Chromium crashed" popup)
- Remove send to self (Remove the menu option "Send To Your Devices" when using Google Sync)

## Contributing
Clone this repository with `git clone --recursive https://github.com/Ceiridge/Chrome-Developer-Mode-Extension-Warning-Patcher.git` and open the `.sln` file with Visual Studio 2019 or newer.

## Message to Chromium contributors
This project is not meant for malicious use, especially because patching requires Administrator rights. If an attacker wants to get rid of that notification, they will always be able to do it somehow, since they have access to the computer and to other methods anyway. For example, you could just install a crx-file and allow it with group policies. This makes no sense, because it punishes developers with annoying popups, but crx files that are already packed - and not on the store - can strangely be installed easily.

The idea originates from an answer on StackOverflow that also patched the `chrome.dll` and used to work on old versions.

Used open source libraries:
- [dahall/taskscheduler](https://github.com/dahall/taskscheduler)
- [commandlineparser/commandline](https://github.com/commandlineparser/commandline)
