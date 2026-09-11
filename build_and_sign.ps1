$ErrorActionPreference = 'Stop'

# Comprobar todas las fuentes antes de compilar, incluidas las de Windows-1252.
$assemblyFiles = Get-ChildItem -Path "$PSScriptRoot\*\Properties\AssemblyInfo.cs"
$releaseVersions = @($assemblyFiles | ForEach-Object {
    $source = Get-Content -LiteralPath $_.FullName -Raw
    [regex]::Matches($source, '(?m)^\[assembly: Assembly(?:File|Informational)?Version\("([^"]+)"\)\]') | ForEach-Object { $_.Groups[1].Value }
} | Sort-Object -Unique)
if ($releaseVersions.Count -ne 1 -or $releaseVersions[0] -notmatch '^\d+\.\d+\.\d+$') {
    throw 'Las versiones de AssemblyInfo.cs no coinciden o no tienen tres numeros.'
}

$certPath = "IESElRincon.pfx"
$certPasswordString = "ies2024"
$signtool = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe"
$msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"

Write-Host "1. Compilando solucion completa..."
& $msbuild /t:Rebuild /p:Configuration=Release /p:Platform=x64 SafeExamBrowser.sln
if ($LASTEXITCODE -ne 0) { throw "Error compilando solucion" }

Write-Host "2. Firmando ejecutables principales..."
$exesToSign = @(
    "SafeExamBrowser.Runtime\bin\x64\Release\SafeExamBrowser.exe",
    "SafeExamBrowser.Client\bin\x64\Release\SafeExamBrowser.Client.exe",
    "SafeExamBrowser.Runtime\bin\x64\Release\SafeExamBrowser.Client.exe",
    "SafeExamBrowser.Service\bin\x64\Release\SafeExamBrowser.Service.exe",
    "SafeExamBrowser.ResetUtility\bin\x64\Release\SafeExamBrowser.ResetUtility.exe"
)

foreach ($exe in $exesToSign) {
    if (Test-Path $exe) {
        Write-Host "   Firmando $exe ..."
        & $signtool sign /f $certPath /p $certPasswordString /tr http://timestamp.digicert.com /td sha256 /fd sha256 $exe
        if ($LASTEXITCODE -ne 0) { throw "Error firmando $exe" }
    }
}

Write-Host "3. Re-empaquetando el MSI con los ejecutables firmados..."
# No recompilar los ejecutables después de firmarlos: se perderían sus firmas.
& $msbuild /t:Rebuild /p:Configuration=Release /p:Platform=x64 /p:BuildProjectReferences=false "/p:SolutionDir=$PSScriptRoot/" Setup\Setup.wixproj
if ($LASTEXITCODE -ne 0) { throw "Error re-empaquetando MSI" }

Write-Host "4. Generando y firmando el MSI final..."
$fileVersion = [version](Get-Item "SafeExamBrowser.Runtime\bin\x64\Release\SafeExamBrowser.exe").VersionInfo.FileVersion
$version = $fileVersion.ToString(3)
$msiPath = "Setup\bin\x64\Release\ElRinconSeguro_$version.msi"

Copy-Item "Setup\bin\x64\Release\Setup.msi" -Destination $msiPath -Force
# El proceso separado libera los handles COM de lectura antes de firmar el MSI.
& powershell -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\verify_installer.ps1" -MsiPath $msiPath -ExpectedVersion $version
if ($LASTEXITCODE -ne 0) { throw 'Error verificando MSI' }
& $signtool sign /f $certPath /p $certPasswordString /tr http://timestamp.digicert.com /td sha256 /fd sha256 $msiPath
if ($LASTEXITCODE -ne 0) { throw "Error firmando MSI" }

Write-Host "=============================================="
Write-Host " Proceso completado exitosamente."
Write-Host " Instalador final: $msiPath"
Write-Host "=============================================="
