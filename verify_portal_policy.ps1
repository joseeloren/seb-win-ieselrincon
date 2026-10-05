#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$jsonAssembly = Join-Path $PSScriptRoot 'packages\Newtonsoft.Json.13.0.3\lib\net45\Newtonsoft.Json.dll'
[void][Reflection.Assembly]::LoadFrom($jsonAssembly)
$policySource = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'SafeExamBrowser.Browser\Handlers\PortalResourcePolicy.cs'))
$policySource = $policySource.Replace('internal static class PortalResourcePolicy', 'public static class PortalResourcePolicy').Replace('internal static string Token', 'public static string Token').Replace('internal static bool Managed', 'public static bool Managed').Replace('internal static async Task<bool> PermittedAsync', 'public static async Task<bool> PermittedAsync')
$policySource = [regex]::Replace($policySource, 'private static readonly HttpClient Client = .*?;', 'private static readonly HttpClient Client = new HttpClient(new PolicyFixture());')
$stubs = @'
namespace SafeExamBrowser.Settings.Browser { public class BrowserSettings { public string StartUrl { get; set; } } }
namespace SafeExamBrowser.Core.Contracts { public static class ApiConstants { public const string BaseUrl = "https://elrinconseguro.ieselrincon.es"; } }
public class PolicyFixture : System.Net.Http.HttpMessageHandler {
 public static string Payload, Authorization;
 public static bool Fail;
 protected override System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken token) {
  Authorization = request.Headers.Authorization.ToString();
  var response = new System.Net.Http.HttpResponseMessage(Fail ? System.Net.HttpStatusCode.Forbidden : System.Net.HttpStatusCode.OK);
  response.Content = new System.Net.Http.StringContent(Payload);
  return System.Threading.Tasks.Task.FromResult(response);
 }
}
'@
$references = @((Get-ChildItem (Join-Path $PSHOME 'ref') -Filter '*.dll').FullName) + @($jsonAssembly)
Add-Type -TypeDefinition ($policySource + $stubs) -ReferencedAssemblies $references
[PolicyFixture]::Payload = @{ navigationRules = @('^https://github\.com/alice/project(?:/[^?#]*)?(?:\?[^#]*)?$'); resourceRules = @('^https://github\.com/alice/project(?:/[^?#]*)?(?:\?[^#]*)?$', '^https://raw\.githubusercontent\.com/alice/project/[^#]*$') } | ConvertTo-Json -Compress
$settings = New-Object SafeExamBrowser.Settings.Browser.BrowserSettings
$settings.StartUrl = 'https://exam.example/?token=fake-session'
$policy = [SafeExamBrowser.Browser.Handlers.PortalResourcePolicy]
foreach ($case in @(
 @('https://github.com/alice/project/tree/main', $true, $true),
 @('https://github.com/bob/project', $true, $false),
 @('https://github.com/alice/project-other', $false, $false),
 @('https://raw.githubusercontent.com/alice/project/main/README.md', $false, $true),
 @('https://raw.githubusercontent.com/bob/project/main/README.md', $false, $false),
 @('https://github.com/alice/project%2fother', $false, $false),
 @('https://user:password@github.com/alice/project', $true, $false)
)) {
 $actual = $policy::PermittedAsync($case[0], $settings, $case[1]).GetAwaiter().GetResult()
 if ($actual -ne $case[2]) { throw "Policy mismatch: $($case[0])" }
}
if ([PolicyFixture]::Authorization -ne 'Bearer fake-session') { throw 'Policy identity incorrect' }
[PolicyFixture]::Fail = $true
$settings.StartUrl = 'https://exam.example/?token=revoked-session'
if ($policy::PermittedAsync('https://github.com/alice/project', $settings, $true).GetAwaiter().GetResult()) { throw 'Revoked policy allowed request' }
Write-Host 'PASS: native client gates navigation and encrypted resources, rejects sibling repositories, encoded separators, credentials and revoked sessions.'
