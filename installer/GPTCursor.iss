#ifndef AppVersion
  #error Build using installer/Build-Setup.ps1
#endif
[Setup]
AppId={{AC9BC32F-BA18-4A98-AEB1-F4A9492E5407}
AppName=GPT Cursor
AppVersion={#AppVersion}
AppPublisher=GPT Cursor
DefaultDirName={localappdata}\Programs\GPT Cursor
DefaultGroupName=GPT Cursor
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\dist\installer
OutputBaseFilename=GPT-Cursor-Setup-{#AppVersion}
SetupIconFile=..\assets\app.ico
UninstallDisplayIcon={app}\GPT Cursor.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[CustomMessages]
english.Startup=Start GPT Cursor when I sign in to Windows
german.Startup=GPT Cursor bei der Windows-Anmeldung starten
english.Desktop=Create a desktop shortcut
german.Desktop=Desktop-Verknüpfung erstellen
english.Launch=Launch GPT Cursor
german.Launch=GPT Cursor starten

[Tasks]
Name: "startup"; Description: "{cm:Startup}"; Flags: unchecked
Name: "desktopicon"; Description: "{cm:Desktop}"; Flags: unchecked

[Files]
Source: "..\dist\package\*"; DestDir: "{app}"; Excludes: "settings.json,*.pdb,installed.flag"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "installed.flag"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\SOURCES.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\GPT Cursor"; Filename: "{app}\GPT Cursor.exe"; Parameters: "--activate"
Name: "{group}\{cm:UninstallProgram,GPT Cursor}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\GPT Cursor"; Filename: "{app}\GPT Cursor.exe"; Parameters: "--activate"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "GPTCursor"; ValueData: """{app}\GPT Cursor.exe"" --autostart"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\GPT Cursor.exe"; Parameters: "--activate"; Description: "{cm:Launch}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\GPT Cursor.exe"; Parameters: "--quit"; Flags: runhidden waituntilterminated; RunOnceId: "StopCursor"

[Code]
var
  StartupInitialized: Boolean;

procedure InitializeStartupChoice();
var
  Command: String;
begin
  if StartupInitialized then exit;
  StartupInitialized := True;
  { An upgrade must reflect the current startup choice, not the last installer task. }
  if FileExists(ExpandConstant('{app}\installed.flag')) then begin
    WizardForm.TasksList.Checked[0] := False;
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'GPTCursor', Command) then
      WizardForm.TasksList.Checked[0] := Pos('"' + ExpandConstant('{app}\GPT Cursor.exe') + '"', Command) = 1;
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpSelectTasks then InitializeStartupChoice();
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then InitializeStartupChoice();
end;

function StopInstalledCursor(): Boolean;
var
  ExitCode: Integer;
begin
  Result := True;
  if FileExists(ExpandConstant('{app}\GPT Cursor.exe')) then
    Result := Exec(ExpandConstant('{app}\GPT Cursor.exe'), '--quit', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) and (ExitCode = 0);
end;

function InitializeUninstall(): Boolean;
begin
  Result := StopInstalledCursor();
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not StopInstalledCursor() then
    Result := 'Please close GPT Cursor before continuing.';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'GPTCursor', Command) then
      if Pos('"' + ExpandConstant('{app}\GPT Cursor.exe') + '"', Command) = 1 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'GPTCursor');
end;
