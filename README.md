# Better Disk Cleanup

Better Disk Cleanup is a modern, extensible, and powerful alternative to the built-in Windows Disk Cleanup utility. Built with WPF and .NET 8, it provides an intuitive user interface and a robust core engine for analyzing storage and recovering disk space safely.

## Features
- **Browser Cleanup**: Clears caches, cookies, and temporary data for Chromium-based browsers (Chrome, Edge, Brave, etc.) and Firefox.
- **Duplicate File Finder**: Finds and helps you remove duplicate files to save space, using fast hashing algorithms.
- **Large File Finder**: Quickly scans your disk for files larger than a specific threshold (e.g., 500MB) so you can delete them if no longer needed.
- **Storage Analyzer**: Visualizes your storage usage using an interactive Treemap, making it easy to spot what's taking up your disk space.
- **Startup Manager**: Manage applications that start with Windows, easily disabling or completely removing them with safety guards.
- **Real-Time Monitoring**: Background monitoring for critical disk space and automatic warnings when too many junk files accumulate.
- **Safety First**: Includes whitelist protection to prevent accidental deletion of critical system files, and a comprehensive rollback (undo) system for changes.

## Architecture
See [ARCHITECTURE.md](ARCHITECTURE.md) for a detailed overview of the system design and architecture.

## Requirements
- Windows 10/11 (x64)
- .NET 8.0 Runtime (if not using self-contained release)

## Building the Project
You can build the project from source using the .NET CLI or Visual Studio 2022.

```powershell
# Restore dependencies and build
dotnet build BetterDiskCleanup.sln -c Release

# Run tests
dotnet test BetterDiskCleanup.sln -c Release
```

To create a self-contained executable and a setup installer, you can use the provided PowerShell script (requires Inno Setup 6 to be installed):
```powershell
.\build_release.ps1
```

## Disclaimer
This tool deletes files from your system. While safety mechanisms are in place, always ensure you have backups of your important data.
