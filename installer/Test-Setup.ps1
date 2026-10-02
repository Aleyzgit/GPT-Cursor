$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$version = ([xml](Get-Content (Join-Path $project 'GPTCursor.csproj') -Raw)).Project.PropertyGroup.Version
$testRoot = [IO.Path]::GetFullPath((Join-Path $project 'test-output\installer-lifecycle'))
$appDir = [IO.Path]::GetFullPath((Join-Path $testRoot 'app'))
if (-not $appDir.StartsWith($project + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Test directory outside workspace.' }
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{AC9BC32F-BA18-4A98-AEB1-F4A9492E5407}_is1'
if (Test-Path $uninstallKey) { throw 'An existing installation must not be overwritten by this test.' }
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if ((Get-Item $runKey).GetValue('GPTCursor')) { throw 'An existing startup entry must not be overwritten by this test.' }
$configPath = Join-Path $env:LOCALAPPDATA 'GPTCursor\settings.json'
$originalConfig = if (Test-Path -LiteralPath $configPath) { [IO.File]::ReadAllBytes($configPath) } else { $null }
New-Item -ItemType Directory -Force -Path $testRoot | Out-Null
$results = [Collections.Generic.List[string]]::new()
try {
    $setupArgs = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/TASKS=startup',('/DIR="' + $appDir + '"'),'/GROUP="GPT Cursor Installer Test"',('/LOG="' + (Join-Path $testRoot 'install.log') + '"'))
    $setup = Start-Process -FilePath (Join-Path $project "dist\installer\GPT-Cursor-Setup-$version.exe") -ArgumentList $setupArgs -WindowStyle Hidden -Wait -PassThru
    if ($setup.ExitCode -ne 0) { throw "Install failed: $($setup.ExitCode)" }
    if (-not (Test-Path $uninstallKey)) { throw 'Uninstall registration missing.' }
    if (-not (Test-Path (Join-Path $appDir 'coreclr.dll'))) { throw 'Bundled runtime missing.' }
    $command = Get-ItemPropertyValue $runKey -Name GPTCursor
    if ($command -ne ('"' + (Join-Path $appDir 'GPT Cursor.exe') + '" --autostart')) { throw 'Startup command mismatch.' }
    $results.Add('PASS: Installation, bundled runtime, uninstall registration and startup command')
    $smokeLog = Join-Path $testRoot 'installed-smoke.txt'
    $smoke = Start-Process -FilePath (Join-Path $appDir 'GPT Cursor.exe') -ArgumentList @('--smoke-test',('"' + $smokeLog + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($smoke.ExitCode -ne 0) { throw 'Installed app smoke failed.' }
    $results.Add('PASS: Installed application activates and restores the cursor with MouseX running')
    $app = Start-Process -FilePath (Join-Path $appDir 'GPT Cursor.exe') -ArgumentList '--autostart' -WindowStyle Hidden -PassThru
    $ready = $false
    for ($i = 0; $i -lt 30; $i++) {
        try { $signal = [Threading.EventWaitHandle]::OpenExisting(('Local\GPTCursorQuit-' + $env:USERNAME)); $signal.Dispose(); $ready = $true; break } catch { Start-Sleep -Milliseconds 100 }
    }
    if (-not $ready) { throw 'Startup instance not ready.' }
    Start-Sleep -Milliseconds 400
    $app.Refresh()
    if ($app.HasExited -or $app.MainWindowHandle -ne 0) { throw 'Startup did not stay in tray.' }
    $results.Add('PASS: Autostart runs activated in the tray')
    $uninstall = Start-Process -FilePath (Join-Path $appDir 'unins000.exe') -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="' + (Join-Path $testRoot 'uninstall.log') + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($uninstall.ExitCode -ne 0) { throw "Uninstall failed: $($uninstall.ExitCode)" }
    if (-not $app.WaitForExit(5000)) { throw 'Running app was not closed by uninstaller.' }
    if (Test-Path (Join-Path $appDir 'GPT Cursor.exe')) { throw 'Application file remains.' }
    if (Test-Path $uninstallKey) { throw 'Uninstall registration remains.' }
    if ((Get-Item $runKey).GetValue('GPTCursor')) { throw 'Startup entry remains.' }
    $results.Add('PASS: Uninstall closes the running app and removes files, startup and uninstall registration')
    $results | Set-Content (Join-Path $testRoot 'results.txt')
    $results
} finally {
    if (Test-Path -LiteralPath (Join-Path $appDir 'GPT Cursor.exe')) {
        Start-Process -FilePath (Join-Path $appDir 'GPT Cursor.exe') -ArgumentList '--quit' -WindowStyle Hidden -Wait
    }
    if (Test-Path -LiteralPath (Join-Path $appDir 'unins000.exe')) {
        Start-Process -FilePath (Join-Path $appDir 'unins000.exe') -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -WindowStyle Hidden -Wait
    }
    if ($null -ne $originalConfig) { [IO.File]::WriteAllBytes($configPath, $originalConfig) }
    elseif (Test-Path -LiteralPath $configPath) { Remove-Item -LiteralPath $configPath }
}
