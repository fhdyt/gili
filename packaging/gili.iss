; Gili's GitHub installer (F-PUB-03, PRD 9.4, design 5b): one per-user Gili-Setup-<version>.exe, no UAC, self-contained.
; Built by scripts/package.ps1 -Installer, which passes /DAppVersion and /DSourceDir (the self-contained publish).

#ifndef AppVersion
  #error Build this with scripts/package.ps1 -Installer
#endif

[Setup]
; Never change AppId: it is how a new installer finds the copy it updates and how Windows lists it in Apps.
AppId={{A70F41F1-862E-40D7-9B5C-16B5E4FC0029}
AppName=Gili
AppVersion={#AppVersion}
AppVerName=Gili {#AppVersion}
AppPublisher=Gili
VersionInfoVersion={#AppVersion}
; AppPaths.KindOf knows the installed copy by this folder, so there is no choice of folder.
DefaultDirName={localappdata}\Programs\Gili
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
DisableWelcomePage=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
; Gili is closed through --quit below, which releases its AppBar strip; Restart Manager would just end it.
CloseApplications=no
RestartApplications=no
SetupIconFile=Gili.ico
UninstallDisplayIcon={app}\Gili.exe
UninstallDisplayName=Gili
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=Gili-Setup-{#AppVersion}

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"

[InstallDelete]
; A self-contained build can change its list of DLLs between versions. The folder holds only program files; notes and
; settings live in %LOCALAPPDATA%\Gili and %USERPROFILE%\.gili.
Type: filesandordirs; Name: "{app}\*"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; The AUMID lets Windows tie toasts to the installed copy (PRD 9.4).
Name: "{userprograms}\Gili"; Filename: "{app}\Gili.exe"; AppUserModelID: "Gili"

[Run]
; Not skipifsilent: a silent install for an update starts Gili again (F-PUB-04).
Filename: "{app}\Gili.exe"; Description: "Launch Gili"; Flags: nowait postinstall

[UninstallRun]
; Quits Gili, takes its Claude hooks out, brings the taskbar back and removes the start entry; always exits with 0.
Filename: "{app}\Gili.exe"; Parameters: "--prepare-uninstall"; Flags: runhidden waituntilterminated; RunOnceId: "PrepareUninstall"

[Code]
const
  SingleInstance = 'Local\Gili.SingleInstance';

var
  DeleteData: Boolean;

{ Up to 5 seconds for the running copy to let go of its mutex, then a moment for its process to end. }
function GiliQuit(): Boolean;
var
  Waited: Integer;
begin
  Waited := 0;
  while CheckForMutexes(SingleInstance) and (Waited < 5000) do
  begin
    Sleep(250);
    Waited := Waited + 250;
  end;
  Result := not CheckForMutexes(SingleInstance);
  if Result then Sleep(500);
end;

{ Installing over an older copy: it quits the normal way first, releasing its AppBar strip and the taskbar. }
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Exe: String;
  Code: Integer;
begin
  Result := '';
  Exe := ExpandConstant('{app}\Gili.exe');
  if FileExists(Exe) then Exec(Exe, '--quit', '', SW_HIDE, ewWaitUntilTerminated, Code);
  while not GiliQuit() do
  begin
    if SuppressibleMsgBox('Gili is still running. Quit it from the bar menu, then click Retry.', mbError, MB_RETRYCANCEL, IDCANCEL) = IDCANCEL then
    begin
      Result := 'Gili is still running.';
      Exit;
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    { A silent uninstall keeps the data. }
    DeleteData := (not UninstallSilent()) and
      (MsgBox('Also delete my notes and settings?', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES);
  if (CurUninstallStep = usPostUninstall) and DeleteData then
  begin
    DelTree(ExpandConstant('{localappdata}\Gili'), True, True, True);
    DelTree(ExpandConstant('{%USERPROFILE}\.gili'), True, True, True);
  end;
end;
