$ErrorActionPreference = 'Stop'
$installed = $false
$msiLog = $log + '.msi.log'
try {
    Add-Content -LiteralPath $log -Value 'Update helper started; waiting for the startup process.'
    $parent = Get-Process -Id $parentId -ErrorAction SilentlyContinue
    if ($parent -and $parent.StartTime.ToUniversalTime().Ticks.ToString() -eq $parentStart) {
        if (!$parent.WaitForExit(15000)) {
            # Only the exact startup process that requested this update may be stopped.
            $parent = Get-Process -Id $parentId -ErrorAction SilentlyContinue
            if ($parent -and $parent.StartTime.ToUniversalTime().Ticks.ToString() -eq $parentStart) {
                if ($parent.MainModule.FileName -ne $oldExe) { throw 'El proceso de origen no coincide.' }
                Add-Content -LiteralPath $log -Value 'Startup process did not exit; stopping the verified requesting process.'
                Stop-Process -Id $parentId -ErrorAction Stop
                if (!$parent.WaitForExit(10000)) { throw 'No se pudo cerrar el proceso de inicio.' }
            }
        }
    }
    $arguments = '/i "' + $msi + '" /qn /norestart REBOOT=ReallySuppress /L*v "' + $msiLog + '"'
    $process = Start-Process -FilePath (Join-Path $env:SystemRoot 'System32\msiexec.exe') -ArgumentList $arguments -PassThru -Verb RunAs
    $null = $process.Handle
    $process.WaitForExit()
    $exitCode = $process.ExitCode
    Add-Content -LiteralPath $log -Value ('Installer exit code: ' + $exitCode)
    if ($exitCode -ne 0 -and $exitCode -ne 3010) { throw "Windows Installer ha devuelto el error $exitCode. Consulte $msiLog" }
    if (!(Test-Path -LiteralPath $exe)) { throw 'El instalador no ha creado el ejecutable esperado.' }
    $actualVersion = [version](Get-Item -LiteralPath $exe).VersionInfo.FileVersion
    if ($actualVersion.ToString(3) -ne ([version]$expectedVersion).ToString(3)) { throw "La version instalada ($actualVersion) no coincide con la anunciada ($expectedVersion)." }
    $installed = $true
    Add-Content -LiteralPath $log -Value ('Starting updated application: ' + $exe + ' (' + $actualVersion + ')')
    Start-Process -FilePath $exe
} catch {
    $message = 'No se ha completado la actualizacion de El Rincon Seguro. ' + $_.Exception.Message + "`nRegistro: " + $log
    Add-Content -LiteralPath $log -Value $message
    Add-Type -AssemblyName PresentationFramework
    [System.Windows.MessageBox]::Show($message, 'El Rincon Seguro: actualizacion', 'OK', 'Error') | Out-Null
} finally {
    # Keep a failed download and its MSI log available for diagnosis/manual retry.
    if ($installed) {
        for ($attempt = 0; $attempt -lt 10; $attempt++) {
            try {
                if (Test-Path -LiteralPath $msi) { Remove-Item -LiteralPath $msi -Force }
                [IO.Directory]::Delete([IO.Path]::GetDirectoryName($msi))
                break
            } catch { Start-Sleep -Seconds 1 }
        }
    }
}
