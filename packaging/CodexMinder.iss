#ifndef AppVersion
  #error AppVersion must be supplied by the packaging script.
#endif
#ifndef PublishDir
  #error PublishDir must be supplied by the packaging script.
#endif
#ifndef ReleaseDir
  #error ReleaseDir must be supplied by the packaging script.
#endif

[Setup]
AppId={{A7569958-7292-4918-B11E-50BBAA628803}
AppName=CodexMinder
AppVersion={#AppVersion}
AppPublisher=Eric Kolb
AppPublisherURL=https://github.com/erickolb/CodexMinder
AppSupportURL=https://github.com/erickolb/CodexMinder/issues
DefaultDirName={localappdata}\Programs\CodexMinder
DefaultGroupName=CodexMinder
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
WizardStyle=modern dynamic
OutputDir={#ReleaseDir}
OutputBaseFilename=CodexMinder-{#AppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
LicenseFile=..\LICENSE
UninstallDisplayIcon={app}\CodexMinder.exe
CloseApplications=yes
RestartApplications=no
SetupMutex=Local\CodexMinder.Setup
Uninstallable=yes
UsePreviousTasks=yes

[Tasks]
Name: startup; Description: "Run CodexMinder when I sign in to Windows"; Flags: checkedonce

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "installed.marker"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\CodexMinder"; Filename: "{app}\CodexMinder.exe"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "CodexMinder"; ValueData: """{app}\CodexMinder.exe"""; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\CodexMinder.exe"; Description: "Launch CodexMinder"; Flags: nowait postinstall skipifsilent

[Code]
function StopWidget(): Boolean;
var
  ResultCode, Attempt: Integer;
begin
  Result := True;
  if not CheckForMutexes('Local\CodexMinder') then exit;
  if FileExists(ExpandConstant('{app}\CodexMinder.exe')) then
    Exec(ExpandConstant('{app}\CodexMinder.exe'), '--exit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  for Attempt := 1 to 50 do begin
    if not CheckForMutexes('Local\CodexMinder') then exit;
    Sleep(100);
  end;
  Result := False;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not StopWidget() then Result := 'Please exit CodexMinder from its context menu, then retry.';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and not WizardIsTaskSelected('startup') then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'CodexMinder');
end;

function InitializeUninstall(): Boolean;
begin
  Result := StopWidget();
  if not Result and not UninstallSilent then
    MsgBox('Please exit CodexMinder from its context menu, then retry uninstalling.', mbError, MB_OK);
end;
