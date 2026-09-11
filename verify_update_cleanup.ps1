$ErrorActionPreference = 'Stop'
# Compile the actual helper methods without the UI or an installed client.
$source = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'SafeExamBrowser.Runtime/Operations/AutoUpdater.cs'))
$source = $source.Substring(0, $source.IndexOf('        public static void CheckForUpdatesAndRun()')) + "`n    }`n}"
$source = $source.Replace('using System.Windows;', '').Replace('private static string BuildInstallerScript', 'public static string BuildInstallerScript')
Add-Type -TypeDefinition $source
$testRoot = Join-Path $PSScriptRoot ('cleanup-test-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testRoot)
try {
    foreach ($scenario in @('success', 'cancel', 'failure')) {
        $directory = Join-Path $testRoot ("Alumno O'Connor [1] " + $scenario)
        [void][IO.Directory]::CreateDirectory($directory)
        $msi = Join-Path $directory 'update.msi'
        $log = Join-Path $testRoot ($scenario + '.log')
        [IO.File]::WriteAllText($msi, 'Test fixture, not an installer')
        [IO.File]::SetAttributes($msi, [IO.FileAttributes]::Hidden)
        $script = [SafeExamBrowser.Runtime.Operations.AutoUpdater]::BuildInstallerScript($msi, $log)
        $tokens = $null; $parseErrors = $null
        [void][System.Management.Automation.Language.Parser]::ParseInput($script, [ref]$tokens, [ref]$parseErrors)
        if ($parseErrors.Count) { throw 'Generated PowerShell has syntax errors.' }
        if ($script -notmatch '-Wait -PassThru' -or $script -notmatch '/norestart REBOOT=ReallySuppress') { throw 'Missing installer wait or reboot suppression.' }
        # Mock only process launching and parent waiting. Run real deletion against fixtures.
        $script = $script -replace 'Wait-Process -Id \d+ -ErrorAction SilentlyContinue;', ''
        $launchPattern = '\$process = Start-Process .*? -Wait -PassThru;'
        if ([regex]::Matches($script, $launchPattern).Count -ne 1) { throw 'Cannot safely replace installer launch.' }
        $replacement = switch ($scenario) {
            'success' { '$process = [pscustomobject]@{ ExitCode = 0 };' }
            'cancel' { '$process = [pscustomobject]@{ ExitCode = 1602 };' }
            'failure' { "throw 'Simulated launch failure';" }
        }
        $script = [regex]::Replace($script, $launchPattern, [System.Text.RegularExpressions.MatchEvaluator]{ param($match) $replacement })
        $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($script))
        & "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -EncodedCommand $encoded
        if ($LASTEXITCODE -ne 0) { throw "Cleanup helper failed: $scenario" }
        if ([IO.File]::Exists($msi) -or [IO.Directory]::Exists($directory)) { throw "Cleanup failed: $scenario" }
        if (!(Test-Path -LiteralPath $log)) { throw "Missing outcome log: $scenario" }
        Remove-Item -LiteralPath $log
        Write-Host "PASS: $scenario, hidden MSI removed, special characters handled."
    }
} finally {
    # Non-recursive: preserve any unexpected files for inspection.
    [IO.Directory]::Delete($testRoot)
}
