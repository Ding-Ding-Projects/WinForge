; WinTune installer · WinTune 安裝程式 (Inno Setup)
; Packages the self-contained publish output into WinTune-Setup.exe.
; Version and publish folder are passed in by CI: iscc /DMyAppVersion=.. /DMyPublishDir=..

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#ifndef MyPublishDir
  #define MyPublishDir "..\bin\x64\Release\net11.0-windows10.0.26100.0\win-x64\publish"
#endif

#define MyAppName "WinTune"
#define MyAppPublisher "cafepromenade"
#define MyAppExe "WinTune.exe"
#define MyAppUrl "https://github.com/cafepromenade/WinTune"

[Setup]
AppId={{B7A1C0E2-7C2E-4E8A-9C7E-0F1A2B3C4D5E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}
DefaultDirName={autopf}\WinTune
DefaultGroupName=WinTune
DisableProgramGroupPage=yes
; OutputDir is relative to this script's folder (installer/), so "out" => installer\out
OutputDir=out
OutputBaseFilename=WinTune-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExe}
; A bilingual one-line summary shown in Add/Remove Programs.
AppComments=Windows 11 convenience suite · Windows 11 便利套件

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut · 建立桌面捷徑"; GroupDescription: "Shortcuts · 捷徑"

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\WinTune"; Filename: "{app}\{#MyAppExe}"
Name: "{group}\Uninstall WinTune · 解除安裝 WinTune"; Filename: "{uninstallexe}"
Name: "{autodesktop}\WinTune"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "Launch WinTune · 啟動 WinTune"; Flags: nowait postinstall skipifsilent
