param([string]$Compiler = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $Compiler) { $Compiler = Join-Path $projectRoot 'build-tools\inno\ISCC.exe' }
if (-not (Test-Path -LiteralPath $Compiler)) { throw 'Install Inno Setup 6 and pass -Compiler with the path to ISCC.exe.' }
Push-Location $projectRoot
try {
    dotnet publish GPTCursor.csproj -c Release -r win-x64 --self-contained true -o 'dist\package' --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }
    $version = ([xml](Get-Content 'GPTCursor.csproj' -Raw)).Project.PropertyGroup.Version
    & $Compiler "/DAppVersion=$version" (Join-Path $PSScriptRoot 'GPTCursor.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    $setup = Join-Path $projectRoot "dist\installer\GPT-Cursor-Setup-$version.exe"
    $hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([IO.Path]::GetFileName($setup))" | Set-Content -LiteralPath ($setup + '.sha256') -Encoding ascii
} finally { Pop-Location }
