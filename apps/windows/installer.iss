; DeepSeek Harness - Windows installer (Inno Setup).
;
; Packages the launcher exe, the three WebView2 DLLs, the bundled dsh\ web
; runtime, and a portable Node.js runtime (runtime\node) into a single Setup.exe
; that installs to Program Files, so the user needs no pre-installed Node.js or
; any web toolchain. Build with apps/windows/build-installer.ps1 (or in CI via
; the App Release workflow).
;
; Compile:
;   ISCC.exe /DMyAppVersion=<version> installer.iss
;
; The version (without the dsh-v prefix) is passed as MyAppVersion; it drives
; the display version and the Setup output filename.

#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif

#define MyAppName "DeepSeek Harness"
#define MyAppExe "DeepSeek Harness.exe"
#define MyAppPublisher "DeepSeek Harness fork"
#define MyAppURL "https://github.com/BuyMyCheatPlz/Deepseek_Harness_Updated"
#define BuildDir "build"

[Setup]
AppId={{FB9A1FC0-2F2E-4C88-9A9E-6B25E3C0F1AA}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
; Layout mirror: everything lives under one Program Files dir.
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; The app binary is the installed launcher.
UninstallDisplayName={#MyAppName} {#MyAppVersion}
UninstallDisplayIcon={app}\{#MyAppExe}
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
OutputDir={#BuildDir}
OutputBaseFilename=DeepSeek-Harness-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
MinVersion=10.0
VersionInfoVersion={#MyAppVersion}

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "zh"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Launcher + WebView2 runtime.
Source: "{#MyAppSourcePath}{#BuildDir}\{#MyAppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyAppSourcePath}{#BuildDir}\Microsoft.Web.WebView2.Core.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyAppSourcePath}{#BuildDir}\Microsoft.Web.WebView2.WinForms.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyAppSourcePath}{#BuildDir}\WebView2Loader.dll"; DestDir: "{app}"; Flags: ignoreversion
; Bundled web runtime (the fork's dsh, already rebundled).
Source: "{#MyAppSourcePath}{#BuildDir}\dsh\*"; DestDir: "{app}\dsh"; Flags: recursesubdirs ignoreversion
; Bundled portable Node.js runtime (node.exe + npm). Supplied by build-installer.ps1.
Source: "{#MyAppSourcePath}{#BuildDir}\runtime\node\*"; DestDir: "{app}\runtime\node"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
