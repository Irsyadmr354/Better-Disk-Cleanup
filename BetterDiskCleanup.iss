[Setup]
AppName=Better Disk Cleanup
AppVersion=1.0.0
Publisher=Royan
DefaultDirName={autopf}\Better Disk Cleanup
DefaultGroupName=Better Disk Cleanup
OutputDir=Output
OutputBaseFilename=BetterDiskCleanup_Setup_v1.0.0
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\BetterDiskCleanup.App.exe

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "Publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Better Disk Cleanup"; Filename: "{app}\BetterDiskCleanup.App.exe"
Name: "{autodesktop}\Better Disk Cleanup"; Filename: "{app}\BetterDiskCleanup.App.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\BetterDiskCleanup.App.exe"; Description: "{cm:LaunchProgram,Better Disk Cleanup}"; Flags: nowait postinstall skipifsilent
