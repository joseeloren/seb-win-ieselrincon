param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
# Reflection loads the assembly but does not invoke its entry point or launch MSI.
$runtimePath = Join-Path $PSScriptRoot "SafeExamBrowser.Runtime\bin\x64\$Configuration\SafeExamBrowser.exe"
$runtime = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $runtimePath))
$updater = $runtime.GetType('SafeExamBrowser.Runtime.Operations.AutoUpdater')
$script = $updater.GetMethod('BuildInstallerScript',[Reflection.BindingFlags]'NonPublic,Static').Invoke($null,@("C:\User's files\update.msi",'C:\logs\update.log','0.0.183'))
$tokens = $null
$parseErrors = $null
[void][Management.Automation.Language.Parser]::ParseInput($script,[ref]$tokens,[ref]$parseErrors)
if ($parseErrors.Count) { throw $parseErrors[0] }
$source = Get-Content (Join-Path $PSScriptRoot 'SafeExamBrowser.Runtime\Operations\InstallUpdate.ps1') -Raw
if (!$script.EndsWith($source)) { throw 'The compiled updater resource differs from the tested source.' }
if (!$script.Contains('ElRinconSeguro\Application\SafeExamBrowser.exe')) { throw 'Updater must relaunch the independent installation.' }
if (!$script.Contains('/norestart REBOOT=ReallySuppress')) { throw 'Updater must suppress restarts.' }
if (!$script.Contains("User''s files")) { throw 'Updater must quote paths safely.' }
if (!$script.Contains('WaitForExit(15000)') -or !$script.Contains('StartTime.ToUniversalTime()') -or !$script.Contains('/L*v')) { throw 'Missing bounded wait, identity check or MSI logging.' }
Write-Host 'PASS: updater destination, restart suppression and paths. Generated script was parsed, NOT executed.'
