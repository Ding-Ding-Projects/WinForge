#define MyAppName "Dew Encryption"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "codingmachineedge"
#define MyAppURL "https://github.com/codingmachineedge/dew-encryption"
#define MyAppExeName "dew-encryption.exe"
#define MyAppGuiExeName "dew-encryption-gui.exe"

[Setup]
AppId={{9E91A740-2663-4DB2-9C7C-198FC592E3FB}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\Dew Encryption
DefaultGroupName=Dew Encryption
DisableProgramGroupPage=yes
OutputDir=..\dist\installer
OutputBaseFilename=DewEncryptionSetup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\assets\icons\dew-main.ico
UninstallDisplayIcon={app}\{#MyAppGuiExeName}

[Files]
Source: "..\dist\app\dew-encryption.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\app\dew-encryption-gui.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\installer\create-elevated-tasks.ps1"; DestDir: "{app}\installer"; Flags: ignoreversion
Source: "..\installer\remove-elevated-tasks.ps1"; DestDir: "{app}\installer"; Flags: ignoreversion
Source: "..\installer\launch-create-elevated-tasks.ps1"; DestDir: "{app}\installer"; Flags: ignoreversion
Source: "..\installer\launch-remove-elevated-tasks.ps1"; DestDir: "{app}\installer"; Flags: ignoreversion
Source: "..\assets\icons\dew-main.ico"; DestDir: "{app}\icons"; Flags: ignoreversion
Source: "..\assets\icons\dew-archive.ico"; DestDir: "{app}\icons"; Flags: ignoreversion
Source: "..\assets\icons\dew-watch.ico"; DestDir: "{app}\icons"; Flags: ignoreversion
Source: "..\assets\icons\dew-history.ico"; DestDir: "{app}\icons"; Flags: ignoreversion
Source: "..\assets\icons\dew-veracrypt-encrypt.ico"; DestDir: "{app}\icons"; Flags: ignoreversion
Source: "..\assets\icons\dew-veracrypt-decrypt.ico"; DestDir: "{app}\icons"; Flags: ignoreversion

[Icons]
Name: "{group}\Dew Encryption"; Filename: "{app}\{#MyAppGuiExeName}"
Name: "{group}\Dew Encryption README"; Filename: "{app}\README.md"

[Tasks]
Name: "installveracrypt"; Description: "Install VeraCrypt with winget if missing"; GroupDescription: "Dependencies:"
Name: "startup"; Description: "Start Dew Drive auto-sync at Windows login"; GroupDescription: "Startup:"

[Run]
Filename: "powershell"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""if (-not (Get-Command VeraCrypt -ErrorAction SilentlyContinue)) {{ winget install -e --id IDRIX.VeraCrypt --accept-source-agreements --accept-package-agreements }}"""; Description: "Install VeraCrypt"; Flags: runhidden; Tasks: installveracrypt
Filename: "{app}\{#MyAppGuiExeName}"; Description: "Launch Dew Encryption"; Flags: nowait postinstall skipifsilent

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "DewEncryptionDewDrive"; ValueData: """{app}\{#MyAppGuiExeName}"" --auto-sync --minimized"; Flags: uninsdeletevalue; Tasks: startup

Root: HKCU; Subkey: "Software\Classes\*\shell\dew-encryption"; ValueType: string; ValueName: "MUIVerb"; ValueData: "dew encryption"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\*\shell\dew-encryption"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\icons\dew-archive.ico"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\*\shell\dew-encryption\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey

Root: HKCU; Subkey: "Software\Classes\Directory\shell\dew-encryption"; ValueType: string; ValueName: "MUIVerb"; ValueData: "dew encryption"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\dew-encryption"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\icons\dew-archive.ico"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\dew-encryption\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey

Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\dew-encryption"; ValueType: string; ValueName: "MUIVerb"; ValueData: "dew encryption"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\dew-encryption"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\icons\dew-archive.ico"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\dew-encryption\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%V"""; Flags: uninsdeletekey

Root: HKCU; Subkey: "Software\Classes\Directory\shell\dew-encryption-watch"; ValueType: string; ValueName: "MUIVerb"; ValueData: "dew encryption start file history"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\dew-encryption-watch"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\icons\dew-watch.ico"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\dew-encryption-watch\command"; ValueType: string; ValueName: ""; ValueData: "powershell -NoProfile -ExecutionPolicy Bypass -Command ""Start-Process -WindowStyle Hidden -FilePath '{app}\{#MyAppExeName}' -ArgumentList @('watch','%1')"""; Flags: uninsdeletekey

Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\dew-encryption-watch"; ValueType: string; ValueName: "MUIVerb"; ValueData: "dew encryption start file history"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\dew-encryption-watch"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\icons\dew-watch.ico"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\dew-encryption-watch\command"; ValueType: string; ValueName: ""; ValueData: "powershell -NoProfile -ExecutionPolicy Bypass -Command ""Start-Process -WindowStyle Hidden -FilePath '{app}\{#MyAppExeName}' -ArgumentList @('watch','%V')"""; Flags: uninsdeletekey

Root: HKCU; Subkey: "Software\Classes\Directory\shell\dew-encryption-manager"; ValueType: string; ValueName: "MUIVerb"; ValueData: "dew encryption file history manager"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\dew-encryption-manager"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\icons\dew-history.ico"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\dew-encryption-manager\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppGuiExeName}"" ""%1"" --history"; Flags: uninsdeletekey

Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\dew-encryption-manager"; ValueType: string; ValueName: "MUIVerb"; ValueData: "dew encryption file history manager"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\dew-encryption-manager"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\icons\dew-history.ico"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\dew-encryption-manager\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppGuiExeName}"" ""%V"" --history"; Flags: uninsdeletekey

Root: HKCU; Subkey: "Software\Classes\*\shell\dew-encryption-veracrypt-encrypt"; ValueType: string; ValueName: "MUIVerb"; ValueData: "dew encryption VeraCrypt encrypt"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\*\shell\dew-encryption-veracrypt-encrypt"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\*\shell\dew-encryption-veracrypt-encrypt"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\icons\dew-veracrypt-encrypt.ico"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\*\shell\dew-encryption-veracrypt-encrypt\command"; ValueType: string; ValueName: ""; ValueData: "powershell -NoProfile -ExecutionPolicy Bypass -NoExit -Command ""& '{app}\{#MyAppExeName}' veracrypt-encrypt '%1' %*; Read-Host 'Press Enter to close'"""; Flags: uninsdeletekey

Root: HKCU; Subkey: "Software\Classes\Directory\shell\dew-encryption-veracrypt-encrypt"; ValueType: string; ValueName: "MUIVerb"; ValueData: "dew encryption VeraCrypt encrypt"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\dew-encryption-veracrypt-encrypt"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\dew-encryption-veracrypt-encrypt"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\icons\dew-veracrypt-encrypt.ico"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\dew-encryption-veracrypt-encrypt\command"; ValueType: string; ValueName: ""; ValueData: "powershell -NoProfile -ExecutionPolicy Bypass -NoExit -Command ""& '{app}\{#MyAppExeName}' veracrypt-encrypt '%1' %*; Read-Host 'Press Enter to close'"""; Flags: uninsdeletekey

Root: HKCU; Subkey: "Software\Classes\.hc\shell\dew-encryption-veracrypt-decrypt"; ValueType: string; ValueName: "MUIVerb"; ValueData: "dew encryption VeraCrypt decrypt"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.hc\shell\dew-encryption-veracrypt-decrypt"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.hc\shell\dew-encryption-veracrypt-decrypt"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\icons\dew-veracrypt-decrypt.ico"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.hc\shell\dew-encryption-veracrypt-decrypt\command"; ValueType: string; ValueName: ""; ValueData: "powershell -NoProfile -ExecutionPolicy Bypass -NoExit -Command ""& '{app}\{#MyAppExeName}' veracrypt-decrypt '%1' %*; Read-Host 'Press Enter to close'"""; Flags: uninsdeletekey

Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\dew-encryption-create-elevated-tasks"; ValueType: string; ValueName: "MUIVerb"; ValueData: "dew encryption create elevated tasks"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\dew-encryption-create-elevated-tasks"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\icons\dew-main.ico"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\dew-encryption-create-elevated-tasks\command"; ValueType: string; ValueName: ""; ValueData: "powershell -NoProfile -ExecutionPolicy Bypass -File ""{app}\installer\launch-create-elevated-tasks.ps1"""; Flags: uninsdeletekey

Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\dew-encryption-remove-elevated-tasks"; ValueType: string; ValueName: "MUIVerb"; ValueData: "dew encryption remove elevated tasks"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\dew-encryption-remove-elevated-tasks"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\icons\dew-main.ico"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\dew-encryption-remove-elevated-tasks\command"; ValueType: string; ValueName: ""; ValueData: "powershell -NoProfile -ExecutionPolicy Bypass -File ""{app}\installer\launch-remove-elevated-tasks.ps1"""; Flags: uninsdeletekey

Root: HKCU; Subkey: "Software\Classes\*\shell\dew-encryption-dew-drive-add"; ValueType: string; ValueName: "MUIVerb"; ValueData: "dew encryption add to Dew Drive"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\*\shell\dew-encryption-dew-drive-add"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\icons\dew-archive.ico"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\*\shell\dew-encryption-dew-drive-add\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" dew-drive add ""%1"""; Flags: uninsdeletekey

Root: HKCU; Subkey: "Software\Classes\Directory\shell\dew-encryption-dew-drive-add"; ValueType: string; ValueName: "MUIVerb"; ValueData: "dew encryption add to Dew Drive"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\dew-encryption-dew-drive-add"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\icons\dew-archive.ico"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\dew-encryption-dew-drive-add\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" dew-drive add ""%1"""; Flags: uninsdeletekey

Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\dew-encryption-dew-drive-sync"; ValueType: string; ValueName: "MUIVerb"; ValueData: "dew encryption sync Dew Drive"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\dew-encryption-dew-drive-sync"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\icons\dew-archive.ico"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\dew-encryption-dew-drive-sync\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" dew-drive sync --push"; Flags: uninsdeletekey

[Code]
function CommandExists(Name: String): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec(ExpandConstant('{cmd}'), '/C where ' + Name, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function InitializeSetup(): Boolean;
var
  Missing: String;
begin
  Missing := '';
  if not CommandExists('git') then
    Missing := Missing + 'Git' + #13#10;
  if not CommandExists('7z') then
    Missing := Missing + '7-Zip' + #13#10;
  if not CommandExists('VeraCrypt') then
    Missing := Missing + 'VeraCrypt' + #13#10;

  if Missing <> '' then
    MsgBox('Dew Encryption requires these command-line dependencies on PATH for all actions:' + #13#10#13#10 + Missing + #13#10 + 'The installer will continue, but related actions need these tools. The PowerShell bootstrap installer can install Git and 7-Zip automatically; VeraCrypt is installed separately.', mbInformation, MB_OK);

  Result := True;
end;
