param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

function Invoke-External([string]$File, [string[]]$Arguments)
{
    & $File @Arguments
    if ($LASTEXITCODE -ne 0)
    {
        throw "Befehl fehlgeschlagen ($File $($Arguments -join ' ')) mit ExitCode $LASTEXITCODE."
    }
}

$root = Split-Path -Parent $PSScriptRoot
$setupProject = Join-Path $root "XTouchVMBridge.Setup\\XTouchVMBridge.Setup.wixproj"
$testsProject = Join-Path $root "XTouchVMBridge.Tests\\XTouchVMBridge.Tests.csproj"
$msiPath = Join-Path $root "XTouchVMBridge.Setup\\bin\\x64\\$Configuration\\XTouchVMBridge.Setup.msi"
$publishDir = Join-Path $root "XTouchVMBridge.Setup\\bin\\$Configuration\\publish"
$artifactsDir = Join-Path $root "artifacts\\release\\$Configuration"
$zipPath = Join-Path $artifactsDir "XTouchVMBridge-win-x64.zip"
$checksumPath = Join-Path $artifactsDir "SHA256SUMS.txt"

Write-Host "==> Restore"
Invoke-External "dotnet" @("restore", "$root\\XTouchVMBridge.slnx")
Invoke-External "dotnet" @("restore", $setupProject)

if (-not $SkipTests)
{
    Write-Host "==> Test"
    Invoke-External "dotnet" @("test", $testsProject, "-c", $Configuration, "--no-restore")
}

Write-Host "==> Build MSI"
Invoke-External "dotnet" @("build", $setupProject, "-c", $Configuration, "--no-restore")

if (-not (Test-Path $msiPath))
{
    throw "MSI wurde nicht erzeugt: $msiPath"
}

if (-not (Test-Path $publishDir))
{
    throw "Publish-Verzeichnis fehlt: $publishDir"
}

New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null
Copy-Item $msiPath -Destination $artifactsDir -Force

if (Test-Path $zipPath)
{
    Remove-Item $zipPath -Force
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath

$filesForHash = @(
    (Join-Path $artifactsDir (Split-Path $msiPath -Leaf)),
    $zipPath
)

$hashLines = foreach ($file in $filesForHash)
{
    $hash = Get-FileHash -Path $file -Algorithm SHA256
    "{0} *{1}" -f $hash.Hash.ToLowerInvariant(), (Split-Path $file -Leaf)
}

Set-Content -Path $checksumPath -Value $hashLines

Write-Host "==> Done"
Write-Host "Artifacts: $artifactsDir"
Write-Host "Checksums: $checksumPath"
