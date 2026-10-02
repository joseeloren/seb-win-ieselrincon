$ErrorActionPreference = 'Stop'
# Exercise the production recovery code against a fake RDP input stream.
# No keys are sent to the user's actual desktop.
$source = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'SafeExamBrowser.UserInterface.Shared/Utilities/WindowExtensions.cs'))
$start = $source.IndexOf('public static bool ReleaseRemoteModifiers()')
$end = $source.IndexOf('public static void DisableCloseButton')
$methods = $source.Substring($start, $end - $start)
$methods = $methods -replace '\[DllImport\("user32.dll"(?:, SetLastError = true)?\)\]\s*private static extern int GetSystemMetrics\(int index\);', 'private static int GetSystemMetrics(int index) { return Remote ? 1 : 0; }'
$methods = $methods -replace '\[DllImport\("user32.dll"\)\]\s*private static extern short GetAsyncKeyState\(int key\);', 'private static short GetAsyncKeyState(int key) { return Held.Contains(key) ? unchecked((short)0x8000) : (short)0; }'
$methods = $methods -replace '\[DllImport\("user32.dll", SetLastError = true\)\]\s*private static extern uint SendInput\(uint count, RecoveryInput\[\] inputs, int size\);', @'
private static uint SendInput(uint count, RecoveryInput[] inputs, int size) {
 if (size != (IntPtr.Size == 8 ? 40 : 28)) throw new Exception("Incorrect native INPUT size");
 foreach (var input in inputs) {
  if (input.Type != 1 || (input.Data.Keyboard.Flags & 2) == 0) throw new Exception("Recovery must only release keys");
  if (input.Data.Keyboard.Scan != 0 || input.Data.Keyboard.Extra != UIntPtr.Zero) throw new Exception("Unexpected input metadata");
  Sent.Add(input.Data.Keyboard.Key);
 }
 return Fail ? 0 : count;
}
'@
Add-Type -TypeDefinition ("using System; using System.Runtime.InteropServices; using System.Collections.Generic; public static class RecoveryTest { public static bool Remote, Fail; public static HashSet<int> Held = new HashSet<int>(); public static List<int> Sent = new List<int>(); " + $methods + '}')
foreach ($key in @(0xA0,0xA1,0xA2,0xA3,0xA4,0xA5,0x5B,0x5C)) { [void][RecoveryTest]::Held.Add($key) }
if (![RecoveryTest]::ReleaseRemoteModifiers() -or [RecoveryTest]::Sent.Count) { throw 'Local keyboard should remain unchanged' }
[RecoveryTest]::Remote = $true
if (![RecoveryTest]::ReleaseRemoteModifiers() -or [RecoveryTest]::Sent.Count -ne 8) { throw 'Missing modifier releases' }
if (([RecoveryTest]::Sent | Select-Object -Unique).Count -ne 8) { throw 'Duplicate or missing modifiers' }
[RecoveryTest]::Sent.Clear(); [RecoveryTest]::Held.Clear()
if (![RecoveryTest]::ReleaseRemoteModifiers() -or [RecoveryTest]::Sent.Count) { throw 'No held keys should produce no input' }
[void][RecoveryTest]::Held.Add(0xA4); [RecoveryTest]::Fail = $true
if ([RecoveryTest]::ReleaseRemoteModifiers()) { throw 'Recovery failure should be reported' }
Write-Host 'PASS: remote modifier recovery; native INPUT layout; only key-up events; local keyboard unchanged; failures reported.'

# Exercise the compiled interceptor and hook tracker, without installing hooks.
$binaryRoot = Join-Path $PSScriptRoot 'SafeExamBrowser.Client/bin/x64/Release'
foreach ($name in @('SafeExamBrowser.Settings','SafeExamBrowser.Logging.Contracts','SafeExamBrowser.Logging','SafeExamBrowser.WindowsApi.Contracts','SafeExamBrowser.Monitoring.Contracts','SafeExamBrowser.Monitoring','SafeExamBrowser.WindowsApi')) {
 [void][Reflection.Assembly]::LoadFrom((Join-Path $binaryRoot ($name + '.dll')))
}
$settings = New-Object SafeExamBrowser.Settings.Monitoring.KeyboardSettings
$settings.AllowCtrlC = $true; $settings.AllowCtrlV = $true; $settings.AllowCtrlX = $true
$logger = New-Object SafeExamBrowser.Logging.Logger
$interceptor = New-Object SafeExamBrowser.Monitoring.Keyboard.KeyboardInterceptor($logger, $null, $settings)
$callback = $interceptor.GetType().GetMethod('KeyboardHookCallback', [Reflection.BindingFlags]'NonPublic,Instance')
$injected = [SafeExamBrowser.WindowsApi.Contracts.Events.KeyModifier]::Injected
$ctrl = [SafeExamBrowser.WindowsApi.Contracts.Events.KeyModifier]::Ctrl
$pressed = [SafeExamBrowser.WindowsApi.Contracts.Events.KeyState]::Pressed
$released = [SafeExamBrowser.WindowsApi.Contracts.Events.KeyState]::Released
foreach ($key in @(0xA0,0xA1,0xA2,0xA3,0xA4,0xA5,0x5B,0x5C)) {
 if ($callback.Invoke($interceptor, @($key,$injected,$released))) { throw 'Modifier release blocked' }
}
if (!$callback.Invoke($interceptor, @(0x5B,$ctrl,$pressed))) { throw 'Windows key should remain blocked' }
if (!$callback.Invoke($interceptor, @(0x43,$injected,$pressed))) { throw 'Injected key presses should remain blocked' }
foreach ($key in @(0x41,0x43,0x56,0x58)) {
 if ($callback.Invoke($interceptor, @($key,$ctrl,$pressed))) { throw 'Normal Ctrl editing shortcut blocked' }
}
Write-Host 'PASS: compiled interceptor allows all modifier releases and Ctrl+A/C/V/X, preserves press restrictions.'

$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $binaryRoot 'SafeExamBrowser.WindowsApi.dll'))
$hookType = $assembly.GetType('SafeExamBrowser.WindowsApi.Hooks.KeyboardHook')
$hook = [Activator]::CreateInstance($hookType, [Reflection.BindingFlags]'NonPublic,Instance', $null, @($null), $null)
$dataType = $assembly.GetType('SafeExamBrowser.WindowsApi.Types.KBDLLHOOKSTRUCT')
$keyField = $dataType.GetField('KeyCode', [Reflection.BindingFlags]'NonPublic,Instance')
$getModifiers = $hookType.GetMethod('GetModifiers', [Reflection.BindingFlags]'NonPublic,Instance')
function Modifiers([int]$key, [int]$message) {
 $data = [Activator]::CreateInstance($dataType)
 $keyField.SetValue($data, [uint32]$key)
 return $getModifiers.Invoke($hook, @($data, $message))
}
[void](Modifiers 0xA2 0x100); [void](Modifiers 0xA3 0x100)
if (!((Modifiers 0xA2 0x101).HasFlag($ctrl))) { throw 'Releasing left Ctrl must preserve right Ctrl' }
if ((Modifiers 0xA3 0x101).HasFlag($ctrl)) { throw 'Ctrl remained pressed after both releases' }
[void](Modifiers 0xA4 0x104); [void](Modifiers 0xA5 0x104)
$alt = [SafeExamBrowser.WindowsApi.Contracts.Events.KeyModifier]::Alt
if (!((Modifiers 0xA4 0x105).HasFlag($alt))) { throw 'Releasing left Alt must preserve right Alt' }
if ((Modifiers 0xA5 0x105).HasFlag($alt)) { throw 'Alt remained pressed after both releases' }
Write-Host 'PASS: compiled hook tracks left/right Ctrl and Alt independently.'
