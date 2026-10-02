$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$root = [IO.Path]::GetFullPath((Join-Path $project 'test-output\upgrade-lifecycle'))
$appDir = Join-Path $root 'app'
if (-not $appDir.StartsWith($project + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid test directory.' }
$registry = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{AC9BC32F-BA18-4A98-AEB1-F4A9492E5407}_is1'
$run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if ((Test-Path $registry) -or (Get-Item $run).GetValue('GPTCursor')) { throw 'Existing installation/startup must not be touched.' }
$config = Join-Path $env:LOCALAPPDATA 'GPTCursor\settings.json'
$backup = if (Test-Path $config) { [IO.File]::ReadAllBytes($config) } else { $null }
New-Item -ItemType Directory -Force -Path $root | Out-Null
function Install-Version([string]$version, [string[]]$extra = @()) {
    $args = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/DIR="' + $appDir + '"'),'/GROUP="GPT Cursor Upgrade Test"',('/LOG="' + (Join-Path $root ("install-" + $version + '.log')) + '"')) + $extra
    $p = Start-Process -FilePath (Join-Path $project "dist\installer\GPT-Cursor-Setup-$version.exe") -ArgumentList $args -WindowStyle Hidden -Wait -PassThru
    if ($p.ExitCode -ne 0) { throw "Setup $version failed: $($p.ExitCode)" }
}
try {
    Install-Version '1.1.0' @('/TASKS=startup')
    New-Item -ItemType Directory -Force -Path (Split-Path $config) | Out-Null
    '{"Size":47,"Language":"de","ClickRings":false,"CheckUpdates":false}' | Set-Content -LiteralPath $config
    $app = Start-Process -FilePath (Join-Path $appDir 'GPT Cursor.exe') -ArgumentList '--autostart' -WindowStyle Hidden -PassThru
    Start-Sleep -Milliseconds 1000
    # The old app normalizes its settings on startup. Compare the saved file across the upgrade.
    $saved = Get-Content -LiteralPath $config -Raw | ConvertFrom-Json
    if ($saved.Size -ne 47 -or $saved.Language -ne 'de' -or $saved.ClickRings -ne $false) { throw 'Old app did not load the test preferences.' }
    $settingsHash = (Get-FileHash $config).Hash
    Install-Version '1.2.0'
    if (-not $app.WaitForExit(5000)) { throw 'Old app did not exit during upgrade.' }
    if ((Get-ItemProperty $registry).DisplayVersion -ne '1.2.0') { throw 'Installed version incorrect.' }
    if ((Get-FileHash $config).Hash -ne $settingsHash) { throw 'Settings changed by upgrade.' }
    if (-not (Get-Item $run).GetValue('GPTCursor')) { throw 'Enabled startup lost during upgrade.' }
    Remove-ItemProperty -LiteralPath $run -Name GPTCursor
    Install-Version '1.2.0'
    if ((Get-Item $run).GetValue('GPTCursor')) { throw 'Upgrade re-enabled disabled startup.' }
    @('PASS: Running 1.1.0 upgrades to 1.2.0','PASS: Settings preserved','PASS: Enabled startup preserved','PASS: Disabled startup stays disabled') | Set-Content (Join-Path $root 'results.txt')
    Get-Content (Join-Path $root 'results.txt')
} finally {
    if (Test-Path (Join-Path $appDir 'unins000.exe')) {
        $p = Start-Process -FilePath (Join-Path $appDir 'unins000.exe') -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -WindowStyle Hidden -Wait -PassThru
        if ($p.ExitCode -ne 0) { Write-Error 'Test installation cleanup failed.' }
    }
    if ($null -ne $backup) { [IO.File]::WriteAllBytes($config, $backup) }
    elseif (Test-Path $config) { Remove-Item -LiteralPath $config }
}
