/*
 * Copyright (c) 2026 Dpto. Informática IES El Rincón, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using SafeExamBrowser.Core.Contracts.OperationModel;
using SafeExamBrowser.Core.Contracts.OperationModel.Events;
using SafeExamBrowser.I18n.Contracts;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.UserInterface.Contracts;

namespace SafeExamBrowser.Client.Operations
{
	internal class TelemetryOperation : ClientOperation
	{
		private readonly ILogger logger;
		private readonly IUserInterfaceFactory uiFactory;

		public override event StatusChangedEventHandler StatusChanged;

		public static string CurrentExamCode { get; private set; }
		public static string CurrentStudentName { get; private set; }

		public TelemetryOperation(ClientContext context, ILogger logger, IUserInterfaceFactory uiFactory) : base(context)
		{
			this.logger = logger;
			this.uiFactory = uiFactory;
		}

		private static Task pingTask;

		public override OperationResult Perform()
		{
			logger.Info("Initializing Telemetry (Student Identification)...");

			var startUrl = Context.Settings?.Browser?.StartUrl;
			string examToken = null;
			if (!string.IsNullOrEmpty(startUrl) && startUrl.Contains("token="))
			{
				var uri = new Uri(startUrl);
				var queryStr = uri.Query.TrimStart('?');
				var pairs = queryStr.Split('&');
				foreach(var pair in pairs) 
				{
					var parts = pair.Split('=');
					if(parts.Length == 2 && parts[0] == "token") 
					{
						examToken = parts[1];
					}
				}
			}

			if (!string.IsNullOrEmpty(examToken))
			{
				CurrentExamCode = "vdi-moodle-flow";
				CurrentStudentName = "Token-Session";
				logger.Info($"Token found in StartUrl. Skipping TelemetryDialog. Starting continuous telemetry ping with token...");
				
				if (pingTask == null)
				{
					pingTask = Task.Run(async () => 
					{
						while (true)
						{
							await SendTelemetryAsync(examToken, null, true);
							await Task.Delay(TimeSpan.FromSeconds(5));
						}
					});
				}
				
				return OperationResult.Success;
			}

			var dialog = uiFactory.CreateTelemetryDialog();
			
			// TelemetryDialog might not be supported on all platforms (e.g. Mobile fallback returns null)
			if (dialog == null)
			{
				logger.Warn("Telemetry dialog is not supported on this platform.");
				return OperationResult.Success;
			}

			var result = dialog.Show();

			if (result != null && result.Success)
			{
				CurrentExamCode = result.ExamCode;
				CurrentStudentName = result.StudentName;
				Environment.SetEnvironmentVariable("SEB_CURRENT_EXAM_CODE", result.ExamCode);
				Environment.SetEnvironmentVariable("SEB_CURRENT_STUDENT_NAME", result.StudentName);
				logger.Info($"Student '{result.StudentName}' connected to exam '{result.ExamCode}'. Starting continuous telemetry ping...");
				
				if (pingTask == null)
				{
					pingTask = Task.Run(async () => 
					{
						while (true)
						{
							await SendTelemetryAsync(result.ExamCode, result.StudentName, false);
							await Task.Delay(TimeSpan.FromSeconds(5));
						}
					});
				}
				
				return OperationResult.Success;
			}

			logger.Warn("Telemetry dialog cancelled by user.");
			return OperationResult.Aborted;
		}

		public override OperationResult Revert()
		{
			return OperationResult.Success;
		}

		private string cachedDesktopName = null;

		private async Task SendTelemetryAsync(string idOrToken, string studentName, bool isTokenFlow = false)
		{
			try
			{
				var hostname = Environment.MachineName;
				var localIp = GetLocalIPAddress();
				var publicIp = await GetPublicIpAsync();
				
				if (cachedDesktopName == null)
				{
					try
					{
						if (Context?.Browser != null)
						{
							var windows = Context.Browser.GetWindows()?.ToList();
							if (windows != null && windows.Any())
							{
								var mainWindow = windows.FirstOrDefault(w => w.IsMainWindow) ?? windows.First();
								var tcs = new TaskCompletionSource<string>();
								mainWindow.ExecuteJavaScript("document.getElementById('statusDesktopName') ? document.getElementById('statusDesktopName').innerText : ''", (success, result) => 
								{
									if (success && result != null)
									{
										tcs.TrySetResult(result.ToString());
									}
									else
									{
										tcs.TrySetResult("");
									}
								});
								
								var jsTask = await Task.WhenAny(tcs.Task, Task.Delay(1000));
								if (jsTask == tcs.Task)
								{
									cachedDesktopName = await tcs.Task;
								}
							}
						}
					}
					catch { }
				}

				var desktopName = cachedDesktopName ?? "";
				string json;

				if (isTokenFlow)
				{
					json = "{" +
						"\"token\":\"" + EscapeJson(idOrToken) + "\"," +
						"\"hostname\":\"" + EscapeJson(hostname) + "\"," +
						"\"desktopName\":\"" + EscapeJson(desktopName) + "\"," +
						"\"localIp\":\"" + EscapeJson(localIp) + "\"," +
						"\"publicIp\":\"" + EscapeJson(publicIp) + "\"" +
					"}";
				}
				else
				{
					json = "{" +
						"\"examCode\":\"" + EscapeJson(idOrToken) + "\"," +
						"\"studentName\":\"" + EscapeJson(studentName) + "\"," +
						"\"hostname\":\"" + EscapeJson(hostname) + "\"," +
						"\"desktopName\":\"" + EscapeJson(desktopName) + "\"," +
						"\"localIp\":\"" + EscapeJson(localIp) + "\"," +
						"\"publicIp\":\"" + EscapeJson(publicIp) + "\"" +
					"}";
				}

				using (var client = new HttpClient())
				{
					client.Timeout = TimeSpan.FromSeconds(10);
					var content = new StringContent(json, Encoding.UTF8, "application/json");
					
					var serverUrl = "https://cf289235-5e01-4122-afbe.a2a2d422d2c6.sites.escritorios.ieselrincon.es/api/telemetry";
					logger.Debug($"Sending telemetry POST to {serverUrl} : {json}");
					
					var response = await client.PostAsync(serverUrl, content);
					
					if (response.IsSuccessStatusCode)
					{
						logger.Debug("Telemetry successfully sent to server.");
					}
					else
					{
						logger.Warn($"Failed to send telemetry. Server responded with: {response.StatusCode}");
					}
				}
			}
			catch (Exception ex)
			{
				logger.Error("An error occurred while sending telemetry to the server.", ex);
			}
		}

		private string cachedPublicIp = null;
		private async Task<string> GetPublicIpAsync()
		{
			if (cachedPublicIp != null) return cachedPublicIp;
			try
			{
				using (var client = new HttpClient())
				{
					client.Timeout = TimeSpan.FromSeconds(3);
					cachedPublicIp = (await client.GetStringAsync("https://api.ipify.org")).Trim();
					return cachedPublicIp;
				}
			}
			catch
			{
				return "";
			}
		}

		private string GetLocalIPAddress()
		{
			try
			{
				var host = Dns.GetHostEntry(Dns.GetHostName());
				var ip = host.AddressList.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
				return ip?.ToString() ?? "127.0.0.1";
			}
			catch
			{
				return "127.0.0.1";
			}
		}

		private string EscapeJson(string value)
		{
			if (string.IsNullOrEmpty(value)) return "";
			return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
		}
	}
}
