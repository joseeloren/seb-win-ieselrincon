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
using System.Net.NetworkInformation;
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
		private static volatile string activeToken;

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
						examToken = Uri.UnescapeDataString(parts[1]);
					}
				}
			}

			if (!string.IsNullOrEmpty(examToken))
			{
				CurrentExamCode = "vdi-moodle-flow";
				CurrentStudentName = "Token-Session";
				logger.Info($"Token found in StartUrl. Skipping TelemetryDialog. Starting continuous telemetry ping with token...");
				
				activeToken = examToken;
				if (pingTask == null || pingTask.IsCompleted)
				{
					pingTask = Task.Run(async () => 
					{
						while (true)
						{
							var token = activeToken;
							if (!string.IsNullOrEmpty(token)) await SendTelemetryAsync(token, null, true);
							await Task.Delay(TimeSpan.FromSeconds(5));
						}
					});
				}
				
				return OperationResult.Success;
			}

			logger.Warn("No token found in StartUrl. Redirecting to El Arrinconador to force authentication.");
			if (Context.Settings?.Browser != null)
			{
				Context.Settings.Browser.StartUrl = SafeExamBrowser.Core.Contracts.ApiConstants.BaseUrl + "/alumno";
				if (Context.Settings.Security != null)
				{
					Context.Settings.Security.AllowReconfiguration = true;
					Context.Settings.Security.ReconfigurationUrl = "*";
				}
			}
			return OperationResult.Success;
		}

		public override OperationResult Revert()
		{
			activeToken = null;
			return OperationResult.Success;
		}


		private async Task SendTelemetryAsync(string idOrToken, string studentName, bool isTokenFlow = false)
		{
			try
			{
				var hostname = Environment.MachineName;
				var localIp = GetLocalIPAddress();
				var publicIp = await GetPublicIPAddressAsync();
				
				var desktopName = "";
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
					
					var serverUrl = $"{SafeExamBrowser.Core.Contracts.ApiConstants.BaseUrl}/api/telemetry";
					logger.Debug($"Sending heartbeat to {serverUrl}.");
					
					var response = await client.PostAsync(serverUrl, content);
					
					if (response.IsSuccessStatusCode)
					{
						logger.Debug("Telemetry successfully sent to server.");
					}
					else
					{
                        if (response.StatusCode == HttpStatusCode.Forbidden && activeToken == idOrToken)
                        {
                            activeToken = null;
                            Context.Settings.Browser.StartUrl = SafeExamBrowser.Core.Contracts.ApiConstants.BaseUrl + "/alumno";
                            Context.Browser?.ApplyExamSettings();
                        }
						logger.Warn($"Failed to send telemetry. Server responded with: {response.StatusCode}");
					}
				}
			}
			catch (Exception ex)
			{
				logger.Error("An error occurred while sending telemetry to the server.", ex);
			}
		}

        private static string cachedPublicIp = "";
        private static DateTime nextPublicIpCheck = DateTime.MinValue;
        private async Task<string> GetPublicIPAddressAsync()
        {
            if (DateTime.UtcNow < nextPublicIpCheck) return cachedPublicIp;
            nextPublicIpCheck = DateTime.UtcNow.AddMinutes(1);
            try
            {
                using (var handler = new HttpClientHandler { UseProxy = false })
                using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) })
                {
                    var value = (await client.GetStringAsync("https://api.ipify.org")).Trim();
                    if (IPAddress.TryParse(value, out var address)) cachedPublicIp = address.ToString();
                }
            }
            catch { /* Mantener la última IP detectada durante fallos temporales. */ }
            return cachedPublicIp;
        }

		private string GetLocalIPAddress()
		{
			try
			{
				var ip = NetworkInterface.GetAllNetworkInterfaces()
					.Where(adapter => adapter.OperationalStatus == OperationalStatus.Up && adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
					.SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
					.Select(address => address.Address)
					.FirstOrDefault(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
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
