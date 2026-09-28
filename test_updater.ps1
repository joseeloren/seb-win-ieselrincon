$ErrorActionPreference = 'Stop'
# Run the actual installer workflow with all process/installer effects replaced.
# The only real writes are isolated test logs and empty folders in TestResults.
$source = Get-Content (Join-Path $PSScriptRoot 'SafeExamBrowser.Runtime\Operations\InstallUpdate.ps1') -Raw
$source = $source.Replace("[System.Windows.MessageBox]::Show(`$message, 'El Rincon Seguro: actualizacion', 'OK', 'Error') | Out-Null", '$script:failureShown = $true')
if ($source.Contains('[System.Windows.MessageBox]::Show')) { throw 'Message box mock failed.' }
foreach ($case in @('exited','hung','recycled','wrong-process','msi-error','cancelled','missing-exe','wrong-version','restart-required')) {
    $script:case = $case
    $script:stopped = $false
    $script:launched = $false
    $script:installerStarted = $false
    $script:failureShown = $false
    $script:removed = $false
    $script:waits = @()
    $directory = Join-Path $PSScriptRoot ('TestResults\updater-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    & {
        $msi = Join-Path $directory 'download\update.msi'
        New-Item -ItemType Directory -Path (Split-Path $msi) -Force | Out-Null
        $log = Join-Path $directory 'update.log'
        $exe = 'C:\Program Files\ElRinconSeguro\Application\SafeExamBrowser.exe'
        $oldExe = 'C:\Program Files\SafeExamBrowser\Application\SafeExamBrowser.exe'
        $expectedVersion = '0.0.183'
        $parentId = 12345
        $parentStart = [datetime]::UtcNow.Ticks.ToString()
        $startTime = [datetime]::new([long]$parentStart, [DateTimeKind]::Utc)
        if ($script:case -eq 'recycled') { $startTime = $startTime.AddMinutes(1) }
        $path = if ($script:case -eq 'wrong-process') { 'C:\Other.exe' } else { $oldExe }
        $parentStub = [pscustomobject]@{ StartTime=$startTime; MainModule=[pscustomobject]@{FileName=$path} }
        $parentStub | Add-Member ScriptMethod WaitForExit {
            param($timeout)
            $script:waits += $timeout
            return $script:stopped
        }
        function Get-Process { param($Id,$ErrorAction) if ($script:case -ne 'exited') { $parentStub } }
        function Stop-Process { param($Id,$ErrorAction) if ($Id -ne 12345) { throw 'Wrong PID' }; $script:stopped=$true }
        function Start-Process {
            param($FilePath,$ArgumentList,[switch]$PassThru,$Verb)
            if ($FilePath -eq $exe) { $script:launched=$true; return }
            if ($FilePath -notlike '*\msiexec.exe' -or $Verb -ne 'RunAs') { throw 'Unexpected process request' }
            if ($ArgumentList -notlike '*/L*v*' -or $ArgumentList -notlike '*/norestart REBOOT=ReallySuppress*') { throw 'Missing installer options' }
            $script:installerStarted=$true
            if ($script:case -eq 'cancelled') { throw 'UAC cancelled' }
            $code = if ($script:case -eq 'msi-error') { 1603 } elseif ($script:case -eq 'restart-required') { 3010 } else { 0 }
            $stub = [pscustomobject]@{Handle=1;ExitCode=$code}
            $stub | Add-Member ScriptMethod WaitForExit { }
            $stub
        }
        function Test-Path { param($LiteralPath) return !($LiteralPath -eq $exe -and $script:case -eq 'missing-exe') }
        function Get-Item { param($LiteralPath) $version = if ($script:case -eq 'wrong-version') { '0.0.181' } else { '0.0.183' }; [pscustomobject]@{VersionInfo=[pscustomobject]@{FileVersion=$version}} }
        function Remove-Item { param($LiteralPath,[switch]$Force) $script:removed=$true }
        function Start-Sleep { param($Seconds) }
        function Add-Type { param($AssemblyName) }
        & ([scriptblock]::Create($source))
    }
    $success = $case -in @('exited','hung','recycled','restart-required')
    if ($script:launched -ne $success -or $script:removed -ne $success -or $script:failureShown -eq $success) { throw "Incorrect result for $case" }
    if ($case -eq 'hung' -and (!$script:stopped -or $script:waits[0] -ne 15000 -or $script:waits[1] -ne 10000)) { throw 'Hung parent was not handled with bounded waits.' }
    if ($case -eq 'recycled' -and $script:stopped) { throw 'Reused PID was stopped.' }
    if ($case -eq 'wrong-process' -and ($script:stopped -or $script:installerStarted)) { throw 'Unexpected process was modified.' }
    Write-Host "PASS: $case (installer and process effects simulated)"
}
