param([switch]$SkipInstall)
$ErrorActionPreference = 'Stop'
$env:DOTNET_ROOT = 'C:\Users\TAIKUT~1\AppData\Local\Temp\opencode\dotnet'
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
$env:LLT_SOURCE = 'C:\Users\Taikutsu Lyrz\Desktop\testww3\original\LenovoLegionToolkit-master'
$env:DirectoryBuildPropsPath = Join-Path $PSScriptRoot 'BuildIsolation.props'
Push-Location $PSScriptRoot
try {
    & "$env:DOTNET_ROOT\dotnet.exe" build QuickSettings\QuickSettings.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
    $dll = Get-ChildItem (Join-Path $PSScriptRoot 'artifacts\bin\QuickSettings\Release') -Filter QuickSettings.dll -Recurse -File | Select-Object -First 1
    if ($null -eq $dll) { throw 'QuickSettings.dll not found.' }
    $foreign = Get-ChildItem $dll.DirectoryName -Filter '*.dll' -Recurse -File | Where-Object { $_.Name -ne 'QuickSettings.dll' }
    if ($foreign) { throw ('Unexpected runtime DLLs: ' + ($foreign.FullName -join ', ')) }
    $out = Join-Path $PSScriptRoot 'out'
    New-Item -ItemType Directory -Path $out -Force | Out-Null
    Copy-Item -LiteralPath $dll.FullName -Destination (Join-Path $out 'QuickSettings.dll') -Force
    Compress-Archive -LiteralPath (Join-Path $out 'QuickSettings.dll') -DestinationPath (Join-Path $out 'QuickSettings.zip') -Force
    if (-not $SkipInstall) {
        $running = Get-Process -Name 'Lenovo Legion Toolkit' -ErrorAction SilentlyContinue
        if ($running) {
            Write-Warning 'LLT is running. Quit LLT, then run build.ps1 again to install. DLL and ZIP are in out.'
        } else {
            $install = Join-Path $env:LOCALAPPDATA 'LenovoLegionToolkit\Plugins\QuickSettings'
            New-Item -ItemType Directory -Path $install -Force | Out-Null
            Copy-Item -LiteralPath $dll.FullName -Destination (Join-Path $install 'QuickSettings.dll') -Force
            Write-Host "Installed: $install\QuickSettings.dll"
        }
    }
    Write-Host "Package: $out\QuickSettings.zip"
} finally { Pop-Location }
