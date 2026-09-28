param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
# Reflection loads the assembly but does not invoke its entry point or launch MSI.
$runtimePath = Join-Path $PSScriptRoot "SafeExamBrowser.Runtime\bin\x64\$Configuration\SafeExamBrowser.exe"
$runtime = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $runtimePath))
$updater = $runtime.GetType('SafeExamBrowser.Runtime.Operations.AutoUpdater')
$script = $updater.GetMethod('BuildInstallerScript',[Reflection.BindingFlags]'NonPublic,Static').Invoke($null,@("C:\User's files\update.msi",'C:\logs\update.log'))
$tokens = $null
$parseErrors = $null
[void][Management.Automation.Language.Parser]::ParseInput($script,[ref]$tokens,[ref]$parseErrors)
if ($parseErrors.Count) { throw $parseErrors[0] }
if (!$script.Contains('ElRinconSeguro\Application\SafeExamBrowser.exe')) { throw 'Updater must relaunch the independent installation.' }
if (!$script.Contains('/norestart REBOOT=ReallySuppress')) { throw 'Updater must suppress restarts.' }
if (!$script.Contains("User''s files")) { throw 'Updater must quote paths safely.' }
Write-Host 'PASS: updater destination, restart suppression and paths. Generated script was parsed, NOT executed.'
