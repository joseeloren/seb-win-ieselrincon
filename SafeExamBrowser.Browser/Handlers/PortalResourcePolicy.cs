using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SafeExamBrowser.Core.Contracts;
using SafeExamBrowser.Settings.Browser;

namespace SafeExamBrowser.Browser.Handlers
{
 // Full URL checks happen before encrypted resource requests leave the managed browser.
 internal static class PortalResourcePolicy
 {
  private sealed class Snapshot
  {
   internal DateTime Expires;
   internal string[] Navigation = new string[0];
   internal string[] Resources = new string[0];
   internal readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
  }
  private static readonly HttpClient Client = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(5) };
  private static readonly ConcurrentDictionary<string, Snapshot> Policies = new ConcurrentDictionary<string, Snapshot>();
  internal static string Token(BrowserSettings settings)
  {
   if (!Uri.TryCreate(settings.StartUrl, UriKind.Absolute, out var uri)) return null;
   foreach (var pair in uri.Query.TrimStart('?').Split('&'))
   {
    var parts = pair.Split(new[] { '=' }, 2);
    if (parts.Length == 2 && parts[0] == "token") return Uri.UnescapeDataString(parts[1]);
   }
   return null;
  }
  internal static bool Managed(BrowserSettings settings) => !string.IsNullOrEmpty(Token(settings));
  internal static bool Infrastructure(Uri uri)
  {
   return uri.Host.Equals(new Uri(ApiConstants.BaseUrl).Host, StringComparison.OrdinalIgnoreCase) ||
    new[] { "accounts.google.com", "accounts.google.es", "api.ipify.org", "www.gstatic.com", "ssl.gstatic.com", "fonts.gstatic.com", "fonts.googleapis.com" }.Contains(uri.Host.ToLowerInvariant());
  }
  internal static async Task<bool> PermittedAsync(string target, BrowserSettings settings, bool navigation)
  {
   if (!Uri.TryCreate(target, UriKind.Absolute, out var uri)) return false;
   if (uri.Scheme != "http" && uri.Scheme != "https") return new[] { "chrome-extension", "data", "about" }.Contains(uri.Scheme);
   if (Infrastructure(uri)) return true;
   if (!string.IsNullOrEmpty(uri.UserInfo) || Regex.IsMatch(uri.AbsolutePath, "%2f|%5c", RegexOptions.IgnoreCase)) return false;
   var token = Token(settings);
   if (string.IsNullOrEmpty(token)) return false;
   var snapshot = Policies.GetOrAdd(token, _ => new Snapshot());
   await snapshot.Gate.WaitAsync().ConfigureAwait(false);
   try
   {
    if (snapshot.Expires <= DateTime.UtcNow)
    {
     snapshot.Navigation = new string[0]; snapshot.Resources = new string[0];
     using (var request = new HttpRequestMessage(HttpMethod.Get, ApiConstants.BaseUrl + "/api/proxy-session"))
     {
      request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
      using (var response = await Client.SendAsync(request).ConfigureAwait(false))
      {
       if (!response.IsSuccessStatusCode) { snapshot.Expires = DateTime.UtcNow.AddSeconds(1); return false; }
       var policy = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
       snapshot.Navigation = policy["navigationRules"]?.Values<string>().ToArray() ?? new string[0];
       snapshot.Resources = policy["resourceRules"]?.Values<string>().ToArray() ?? new string[0];
       snapshot.Expires = DateTime.UtcNow.AddSeconds(5);
      }
     }
    }
    return (navigation ? snapshot.Navigation : snapshot.Resources).Any(pattern => Regex.IsMatch(uri.AbsoluteUri, pattern, RegexOptions.None, TimeSpan.FromMilliseconds(50)));
   }
   catch { snapshot.Expires = DateTime.UtcNow.AddSeconds(1); return false; }
   finally { snapshot.Gate.Release(); }
  }
 }
}
