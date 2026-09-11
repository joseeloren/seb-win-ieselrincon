$version = (Get-Item "SafeExamBrowser.Runtime\bin\x64\Release\SafeExamBrowser.exe").VersionInfo.FileVersion
$certPath = "IESElRincon.pfx"
$certPasswordString = "ies2024"
$signtool = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe"
$msiPath = "Setup\bin\x64\Release\ElRinconSeguro_$version.msi"

Copy-Item "Setup\bin\x64\Release\Setup.msi" -Destination $msiPath -Force

Write-Host "Firmando $msiPath..."
& $signtool sign /f $certPath /p $certPasswordString /tr http://timestamp.digicert.com /td sha256 /fd sha256 $msiPath
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error al firmar."
    exit $LASTEXITCODE
}
Write-Host "Firma completada con exito."
