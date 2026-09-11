$ErrorActionPreference = 'Stop'

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
$version = (Get-Item "SafeExamBrowser.Runtime\bin\x64\Release\SafeExamBrowser.exe").VersionInfo.FileVersion
$msiPath = "Setup\bin\x64\Release\ElRinconSeguro_$version.msi"

Copy-Item "Setup\bin\x64\Release\Setup.msi" -Destination $msiPath -Force
& $signtool sign /f $certPath /p $certPasswordString /tr http://timestamp.digicert.com /td sha256 /fd sha256 $msiPath
if ($LASTEXITCODE -ne 0) { throw "Error firmando MSI" }

Write-Host "=============================================="
Write-Host " Proceso completado exitosamente."
Write-Host " Instalador final: $msiPath"
Write-Host "=============================================="
