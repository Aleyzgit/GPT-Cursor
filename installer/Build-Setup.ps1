param([string]$Compiler = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $Compiler) { $Compiler = Join-Path $projectRoot 'build-tools\inno\ISCC.exe' }
if (-not (Test-Path -LiteralPath $Compiler)) { throw 'Install Inno Setup 6 and pass -Compiler with the path to ISCC.exe.' }
Push-Location $projectRoot
try {
    dotnet publish GPTCursor.csproj -c Release -r win-x64 --self-contained true -o 'dist\package' --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }
    & $Compiler (Join-Path $PSScriptRoot 'GPTCursor.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
} finally { Pop-Location }
