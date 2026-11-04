; Desk Widgets - Inno Setup installer script
; Build:  "C:\Users\saifa\AppData\Local\Programs\Inno Setup 6\ISCC.exe" packaging\installer.iss
; Output: bin\DeskWidgets-Setup.exe

#define MyAppName "Desk Widgets"
#define MyAppVersion "1.8.0"
#define MyAppPublisher "Desk Widgets"
#define MyAppURL "https://whoissaaif.github.io/desk-widgets/"
#define MyAppExeName "DeskWidgets.exe"

[Setup]
AppId={{E8A3B2F1-7C4D-4E5A-9F6B-1D2E3F4A5B6C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=license.rtf
OutputDir=..\bin
OutputBaseFilename=DeskWidgets-Setup
SetupIconFile=..\assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardImageFile=assets\WizardImage.bmp
WizardSmallImageFile=assets\WizardSmallImage.bmp
WizardSizePercent=120,120
WizardImageStretch=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
MinVersion=10.0
CloseApplications=yes
RestartApplications=no
DisableWelcomePage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
WelcomeLabel1=Desk Widgets.
WelcomeLabel2=Lightweight desktop widgets for Windows.
FinishedHeadingLabel=You're all set.
FinishedLabel=Desk Widgets is ready to use.
ClickFinish=Click Finish to launch.

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Options:"; Flags: unchecked
Name: "startwithwindows"; Description: "Start with Windows"; GroupDescription: "Options:"

[Files]
Source: "..\bin\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; URL protocol: deskwidgets://
Root: HKCU; Subkey: "Software\Classes\deskwidgets"; ValueType: string; ValueData: "URL:Desk Widgets"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\deskwidgets"; ValueName: "URL Protocol"; ValueType: string; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\deskwidgets\DefaultIcon"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"",0"
Root: HKCU; Subkey: "Software\Classes\deskwidgets\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""
; Autostart
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "DeskWidgets"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --background"; Tasks: startwithwindows; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "taskkill.exe"; Parameters: "/F /IM {#MyAppExeName}"; Flags: runhidden; RunOnceId: "KillApp"

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\DeskWidgets"
Type: filesandordirs; Name: "{localappdata}\DeskWidgets"

[Code]
var
  ResultCode: Integer;

function GetWindowLong(hWnd: HWND; nIndex: Integer): Longint;
  external 'GetWindowLongW@user32.dll stdcall';
function SetWindowLong(hWnd: HWND; nIndex: Integer; dwNewLong: Longint): Longint;
  external 'SetWindowLongW@user32.dll stdcall';
function SetWindowPos(hWnd: HWND; hWndInsertAfter: HWND; X, Y, cx, cy: Integer; uFlags: Cardinal): Boolean;
  external 'SetWindowPos@user32.dll stdcall';
function SetWindowTheme(hWnd: HWND; pszSubAppName: String; pszSubIdList: String): Integer;
  external 'SetWindowTheme@uxtheme.dll stdcall';

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    Exec('taskkill.exe', '/F /IM DeskWidgets.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;

procedure InitializeWizard();
var
  ExStyle: Longint;
begin
  WizardForm.Color := $FFFFFF;
  WizardForm.Font.Color := $1F1D1D;
  WizardForm.Font.Name := 'Segoe UI Variable Text';

  WizardForm.MainPanel.Color := $FFFFFF;
  WizardForm.InnerPage.Color := $FFFFFF;
  WizardForm.WelcomePage.Color := $FFFFFF;
  WizardForm.FinishedPage.Color := $FFFFFF;
  WizardForm.LicensePage.Color := $FFFFFF;
  WizardForm.SelectDirPage.Color := $FFFFFF;
  WizardForm.SelectTasksPage.Color := $FFFFFF;
  WizardForm.InstallingPage.Color := $FFFFFF;
  WizardForm.ReadyPage.Color := $FFFFFF;

  // Header
  WizardForm.PageNameLabel.Font.Color := $1F1D1D;
  WizardForm.PageNameLabel.Font.Name := 'Segoe UI Variable Display';
  WizardForm.PageNameLabel.Font.Style := [fsBold];
  WizardForm.PageDescriptionLabel.Font.Color := $8B8686;
  // Nudge header labels down to prevent top clipping
  WizardForm.PageNameLabel.Top := WizardForm.PageNameLabel.Top + 4;
  WizardForm.PageDescriptionLabel.Top := WizardForm.PageDescriptionLabel.Top + 4;

  // Welcome page
  WizardForm.WelcomeLabel1.Font.Color := $1F1D1D;
  WizardForm.WelcomeLabel1.Font.Name := 'Segoe UI Variable Display';
  WizardForm.WelcomeLabel1.Font.Size := 22;
  WizardForm.WelcomeLabel1.Font.Style := [fsBold];
  WizardForm.WelcomeLabel2.Font.Color := $736E6E;
  WizardForm.WelcomeLabel2.Font.Name := 'Segoe UI Variable Text';

  // Finished page
  WizardForm.FinishedHeadingLabel.Font.Color := $1F1D1D;
  WizardForm.FinishedHeadingLabel.Font.Name := 'Segoe UI Variable Display';
  WizardForm.FinishedHeadingLabel.Font.Size := 22;
  WizardForm.FinishedHeadingLabel.Font.Style := [fsBold];
  WizardForm.FinishedLabel.Font.Color := $736E6E;
  WizardForm.FinishedLabel.Font.Name := 'Segoe UI Variable Text';

  // Directory edit
  WizardForm.DirEdit.Color := $F7F5F5;
  WizardForm.DirEdit.Font.Color := $1F1D1D;
  WizardForm.SelectDirBrowseLabel.Font.Color := $736E6E;
  WizardForm.SelectDirLabel.Font.Color := $736E6E;
  WizardForm.DiskSpaceLabel.Font.Color := $8B8686;

  // Tasks
  WizardForm.TasksList.Color := $FFFFFF;
  WizardForm.TasksList.Font.Color := $1F1D1D;
  WizardForm.SelectTasksLabel.Font.Color := $736E6E;

  // License
  WizardForm.LicenseMemo.Color := $F7F5F5;
  WizardForm.LicenseMemo.Font.Color := $1F1D1D;
  WizardForm.LicenseMemo.Font.Name := 'Cascadia Mono';
  WizardForm.LicenseMemo.Font.Size := 9;
  WizardForm.LicenseAcceptedRadio.Font.Color := $1F1D1D;
  WizardForm.LicenseNotAcceptedRadio.Font.Color := $1F1D1D;
  WizardForm.LicenseLabel1.Font.Color := $736E6E;

  // Ready to Install memo
  WizardForm.ReadyMemo.Color := $F7F5F5;
  WizardForm.ReadyMemo.Font.Color := $736E6E;
  WizardForm.ReadyLabel.Font.Color := $736E6E;

  // Installing
  WizardForm.StatusLabel.Font.Color := $736E6E;
  WizardForm.FilenameLabel.Font.Color := $8B8686;

  // Run list (launch checkbox on finish page)
  WizardForm.RunList.Color := $FFFFFF;
  WizardForm.RunList.Font.Color := $1F1D1D;

  // Bevel lines
  WizardForm.Bevel.Visible := False;
  WizardForm.Bevel1.Visible := False;

  // Remove white 3D border from memo controls
  ExStyle := GetWindowLong(WizardForm.LicenseMemo.Handle, -20);
  SetWindowLong(WizardForm.LicenseMemo.Handle, -20, ExStyle and (not $00000200));
  SetWindowPos(WizardForm.LicenseMemo.Handle, 0, 0, 0, 0, 0, $0027);

  ExStyle := GetWindowLong(WizardForm.ReadyMemo.Handle, -20);
  SetWindowLong(WizardForm.ReadyMemo.Handle, -20, ExStyle and (not $00000200));
  SetWindowPos(WizardForm.ReadyMemo.Handle, 0, 0, 0, 0, 0, $0027);

  // Light scrollbars
  SetWindowTheme(WizardForm.LicenseMemo.Handle, 'Explorer', '');
  SetWindowTheme(WizardForm.ReadyMemo.Handle, 'Explorer', '');
end;
