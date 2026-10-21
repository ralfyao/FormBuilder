; ERP Studio 安裝程式（Inno Setup 6）
; 由 build-release.ps1 呼叫：ISCC.exe /DAppVersion=1.0.0 /DSourceDir=...\release\files ERPStudio.iss
; 安裝到使用者自己的資料夾（%LOCALAPPDATA%\Programs\ERPStudio），不需要系統管理員權限，
; 自動更新時程式才能直接覆蓋自己的檔案

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\release\files"
#endif
#ifndef OutputDir
  #define OutputDir "..\release"
#endif

[Setup]
AppId={{6E0B5D2C-8F3A-4E7B-9C61-5D2F0A7B3E91}
AppName=ERP Studio
AppVersion={#AppVersion}
AppPublisher=YJF
DefaultDirName={localappdata}\Programs\ERPStudio
DefaultGroupName=ERP Studio
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename=ERPStudio-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName=ERP Studio
UninstallDisplayIcon={app}\FormBuilder.exe

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
Name: "{group}\ERP Studio"; Filename: "{app}\FormBuilder.exe"
Name: "{group}\ERP Studio 畫面設計（開發用）"; Filename: "{app}\FormBuilder.exe"; Parameters: "--designer"
Name: "{autodesktop}\ERP Studio"; Filename: "{app}\FormBuilder.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\FormBuilder.exe"; Description: "啟動 ERP Studio"; Flags: nowait postinstall skipifsilent
