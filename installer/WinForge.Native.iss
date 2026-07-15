; WinForge Native C++ installer / WinForge 原生 C++ 安裝程式
; The CI workflow passes MyAppVersion and MyPublishDir to ISCC.

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#ifndef MyPublishDir
  #define MyPublishDir "..\src\WinForge.App\bin\x64\Release"
#endif

#define MyAppName "WinForge Native"
#define MyAppPublisher "codingmachineedge"
#define MyAppExe "WinForge.exe"
#define MyAppUrl "https://github.com/codingmachineedge/WinForge"

[Setup]
AppId={{B87F4D8B-7F9E-4DB9-9E7A-5C6C8D02C9D0}
SetupMutex=WinForgeNativeSetupMutex
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}
DefaultDirName={localappdata}\Programs\WinForge-Native
DefaultGroupName=WinForge Native
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=out-native
OutputBaseFilename=WinForge-Native-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=force
RestartApplications=no
UninstallDisplayIcon={app}\{#MyAppExe}
AppComments=Native C++20 WinUI 3 rewrite · 原生 C++20 WinUI 3 重寫

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut · 建立桌面捷徑"; GroupDescription: "Shortcuts · 捷徑"

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb,*.ilk"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\WinForge Native"; Filename: "{app}\{#MyAppExe}"
Name: "{group}\Uninstall WinForge Native · 解除安裝 WinForge Native"; Filename: "{uninstallexe}"
Name: "{autodesktop}\WinForge Native"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "Launch WinForge Native · 啟動 WinForge Native"; Flags: nowait postinstall skipifsilent
