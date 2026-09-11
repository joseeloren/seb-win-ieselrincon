param(
    [Parameter(Mandatory=$true)][string]$MsiPath,
    [Parameter(Mandatory=$true)][string]$ExpectedVersion
)
$ErrorActionPreference = 'Stop'
if ($ExpectedVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'La version debe tener tres numeros.' }
$installer = New-Object -ComObject WindowsInstaller.Installer
$database = $installer.OpenDatabase((Resolve-Path -LiteralPath $MsiPath).Path, 0)
function Read-MsiValue([string]$Query) {
    $view = $database.OpenView($Query)
    [void]$view.Execute()
    $record = $view.Fetch()
    if (!$record) { throw "Falta un dato del MSI: $Query" }
    $value = $record.StringData(1)
    [void]$view.Close()
    return $value
}
$productVersion = Read-MsiValue "SELECT Value FROM Property WHERE Property = 'ProductVersion'"
$productName = Read-MsiValue "SELECT Value FROM Property WHERE Property = 'ProductName'"
$upgradeCode = Read-MsiValue "SELECT Value FROM Property WHERE Property = 'UpgradeCode'"
if ($productVersion -ne $ExpectedVersion -or $productName -notmatch " $([regex]::Escape($ExpectedVersion)) \(x64\)$") {
    throw "Version interna incorrecta: $productName / $productVersion; esperada: $ExpectedVersion"
}
if ((Split-Path $MsiPath -Leaf) -ne "ElRinconSeguro_$ExpectedVersion.msi") { throw 'Nombre de archivo incorrecto.' }
if ($upgradeCode -ne '{97A8B13E-48FB-4BE1-A7C2-DD1863F95CCB}') { throw 'UpgradeCode incompatible con versiones anteriores.' }
$initialize = [int](Read-MsiValue "SELECT Sequence FROM InstallExecuteSequence WHERE Action = 'InstallInitialize'")
$remove = [int](Read-MsiValue "SELECT Sequence FROM InstallExecuteSequence WHERE Action = 'RemoveExistingProducts'")
$install = [int](Read-MsiValue "SELECT Sequence FROM InstallExecuteSequence WHERE Action = 'InstallFiles'")
if (!($initialize -lt $remove -and $remove -lt $install)) { throw 'Orden incorrecto de desinstalacion e instalacion.' }
$maximum = Read-MsiValue "SELECT VersionMax FROM Upgrade WHERE ActionProperty = 'WIX_UPGRADE_DETECTED'"
$attributes = [int](Read-MsiValue "SELECT Attributes FROM Upgrade WHERE ActionProperty = 'WIX_UPGRADE_DETECTED'")
if ($maximum -ne $ExpectedVersion -or ($attributes -band 2) -or ($attributes -band 4) -or !($attributes -band 512)) {
    throw 'La regla de sustitucion no cubre las versiones anteriores y la misma version, o ignora errores.'
}
$reboot = Read-MsiValue "SELECT Value FROM Property WHERE Property = 'REBOOT'"
if ($reboot -ne 'ReallySuppress') { throw 'El MSI no bloquea los reinicios.' }
$files = $database.OpenView('SELECT FileName, Version FROM File')
[void]$files.Execute()
$checkedFiles = 0
while ($file = $files.Fetch()) {
    $name = ($file.StringData(1) -split '\|')[-1]
    if ($name -match '^(SafeExamBrowser(?:\..+)?|SebWindowsConfig)\.(exe|dll)$') {
        $version = [version]$file.StringData(2)
        if ($version.ToString(3) -ne $ExpectedVersion) { throw "Version incorrecta en $name : $version" }
        $checkedFiles++
    }
}
[void]$files.Close()
if ($checkedFiles -eq 0) { throw 'No se encontraron los ejecutables del cliente en el MSI.' }
Write-Host "$checkedFiles archivos del cliente con version $ExpectedVersion dentro del MSI."
Write-Host "MSI verificado: $productName; ProductVersion=$productVersion; secuencia $initialize < $remove < $install; sin reinicios."
