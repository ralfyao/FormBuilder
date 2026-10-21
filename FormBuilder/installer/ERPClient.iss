; ERP 系統（使用者端）安裝程式（Inno Setup 6）
; 由 build-release.ps1 -Project ERPClient 呼叫：ISCC.exe /DAppVersion=1.0.0 /DSourceDir=... ERPClient.iss
; 安裝到使用者自己的資料夾（%LOCALAPPDATA%\Programs\ERPClient），不需要系統管理員權限，
; 自動更新時程式才能直接覆蓋自己的檔案

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\release-client\files"
#endif
#ifndef OutputDir
  #define OutputDir "..\release-client"
#endif

[Setup]
AppId={{B7C41E2A-3D5F-4A86-9E17-0C2D8F6A4B53}
AppName=ERP 系統
AppVersion={#AppVersion}
AppPublisher=YJF
DefaultDirName={localappdata}\Programs\ERPClient
DefaultGroupName=ERP 系統
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename=ERPClient-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName=ERP 系統
UninstallDisplayIcon={app}\ERPClient.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "建立桌面捷徑"; GroupDescription: "其他："

[Files]
; 自含式發布（含 .NET 執行環境），使用者電腦不用另外裝 .NET
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; IT 可以預先放一份 config.json（系統資料庫連線、更新來源）一起安裝；已經有的不覆蓋
Source: "config.json"; DestDir: "{app}"; Flags: onlyifdoesntexist skipifsourcedoesntexist

[Icons]
Name: "{group}\ERP 系統"; Filename: "{app}\ERPClient.exe"
Name: "{autodesktop}\ERP 系統"; Filename: "{app}\ERPClient.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\ERPClient.exe"; Description: "啟動 ERP 系統"; Flags: nowait postinstall skipifsilent
