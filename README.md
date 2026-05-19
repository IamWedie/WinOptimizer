# ⚡ WinOptimizer Pro

A free, open-source Windows optimization tool built with WPF + C#.

## ⬇️ Download

👉 **[Click here to download the latest version]([../../releases/latest](https://github.com/IamWedie/WinOptimizer/releases/tag/v1.0))**

### How to install
1. Download `WinOptimizer-v1.0.zip`
2. Right-click the zip → **Extract All**
3. Open the extracted folder
4. Right-click `WinOptimizer.exe` → **Run as Administrator**

> ⚠️ Keep all files in the same folder — the `.exe` needs the `.dll` files next to it to run.

---

## Features

| Module | Description |
|--------|-------------|
| 🏠 Home Dashboard | Live PC stats with skeleton loading |
| 🔧 Services Manager | View all services with risk ratings and descriptions |
| 🗑️ App Uninstaller | Deep uninstall — cleans registry and leftover folders |
| 🚀 Cache Cleaner | Clears RAM, temp, DNS, browser and Windows Update cache |
| 🛡️ Debloat | Removes Xbox, Cortana, bloatware and more |
| 🔒 Privacy Tweaks | Disables telemetry, tracking and ads |

---

## Requirements
- Windows 10 or Windows 11
- Must be run as **Administrator**

## ⚠️ Disclaimer
Always create a restore point before making system changes.
This tool modifies Windows settings — use at your own risk.

## 🔨 Build from Source
1. Clone the repo
2. Open `WinOptimizer.csproj` in Visual Studio 2022
3. Install NuGet packages: `Microsoft.Win32.Registry` and `System.ServiceProcess.ServiceController`
4. Press `Ctrl+Shift+B` to build
