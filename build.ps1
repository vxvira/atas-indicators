param(
    [string]$Atas = 'C:\Program Files\ATAS X',
    [switch]$Test,
    [switch]$Deploy
)
$ErrorActionPreference = 'Stop'
$env:AtasDir = "$Atas\"
$root = $PSScriptRoot
$dist = Join-Path $root 'dist'
$tmp = Join-Path $dist '_build'
$indicators = Join-Path $env:APPDATA 'ATAS X\Indicators'
New-Item -ItemType Directory -Force $dist, $tmp | Out-Null

function Invoke-Dotnet {
    dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($args -join ' ') failed" }
}

function Build-Project($csproj, $name) {
    $out = Join-Path $tmp $name
    Invoke-Dotnet build (Join-Path $root $csproj) -c Release -o $out --nologo -v q
    Copy-Item (Join-Path $out "$name.dll") $dist -Force
}

Build-Project 'VolumeProfileX\VolumeProfileX.csproj' 'VolumeProfileX'
Build-Project 'StyledCandlesX\src\StyledCandlesX\StyledCandlesX.csproj' 'StyledCandlesX'

$fpOut = Join-Path $tmp 'FootprintPatcher'
Invoke-Dotnet build (Join-Path $root 'FootprintX\Patcher') -c Release -o $fpOut --nologo -v q
Invoke-Dotnet (Join-Path $fpOut 'Patcher.dll') (Join-Path $root 'FootprintX\base\FootprintX.dll') (Join-Path $dist 'FootprintX.dll') $Atas

if ($Test) {
    Invoke-Dotnet test (Join-Path $root 'VolumeProfileX\tests\VolumeProfileX.Tests') -c Release --nologo -v q
    Invoke-Dotnet test (Join-Path $root 'StyledCandlesX\tests\StyledCandlesX.Tests') -c Release --nologo -v q
}

Remove-Item $tmp -Recurse -Force

if ($Deploy) {
    New-Item -ItemType Directory -Force $indicators | Out-Null
    Get-ChildItem $dist -Filter *.dll | ForEach-Object { Copy-Item $_.FullName $indicators -Force }
}

Get-ChildItem $dist -Filter *.dll | Select-Object Name, Length
