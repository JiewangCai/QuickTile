$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
if ([string]::IsNullOrEmpty($root)) {
    $root = Split-Path -Parent $MyInvocation.MyCommand.Path
}

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    Write-Error "C# compiler not found at: $csc"
    exit 1
}

Push-Location $root
try {
    $distDir = Join-Path $root "dist"
    if (-not (Test-Path $distDir)) {
        New-Item -ItemType Directory -Force -Path $distDir | Out-Null
    }

    $outExe = "dist\QuickTile.exe"
    $args = @(
        "/nologo",
        "/target:winexe",
        "/platform:x64",
        "/optimize+",
        "/codepage:65001",
        "/win32manifest:app.manifest",
        "/out:$outExe",
        "/reference:System.dll",
        "/reference:System.Core.dll",
        "/reference:System.Drawing.dll",
        "/reference:System.Windows.Forms.dll",
        "/reference:System.Runtime.Serialization.dll",
        "/reference:System.Xml.dll",
        "src\*.cs"
    )

    & $csc @args
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }

    $item = Get-Item (Join-Path $root $outExe)
    $sizeKb = [Math]::Round($item.Length / 1KB, 2)
    Write-Host ("Built {0} ({1} KB)" -f $item.FullName, $sizeKb)
}
finally {
    Pop-Location
}
