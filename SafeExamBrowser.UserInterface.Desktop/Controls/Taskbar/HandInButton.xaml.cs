using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading.Tasks;

namespace SafeExamBrowser.UserInterface.Desktop.Controls.Taskbar
{
	internal partial class HandInButton : UserControl
	{
		public HandInButton()
		{
			InitializeComponent();
		}

		private async void Button_Click(object sender, RoutedEventArgs e)
		{
			string currentExamCode = Environment.GetEnvironmentVariable("SEB_CURRENT_EXAM_CODE");
			string currentStudentName = Environment.GetEnvironmentVariable("SEB_CURRENT_STUDENT_NAME");

			if (string.IsNullOrEmpty(currentExamCode) || string.IsNullOrEmpty(currentStudentName))
			{
				MessageBox.Show(Window.GetWindow(this), "No hay sesión activa para solicitar la entrega.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			Button.IsEnabled = false;
			ButtonText.Text = "Solicitando...";

			try
			{
				var json = "{" +
					"\"examCode\":\"" + EscapeJson(currentExamCode) + "\"," +
					"\"studentName\":\"" + EscapeJson(currentStudentName) + "\"" +
				"}";

				var requestUrl = $"https://cf289235-5e01-4122-afbe.a2a2d422d2c6.sites.escritorios.ieselrincon.es/api/exams/{System.Uri.EscapeDataString(currentExamCode)}/request-url";

				var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(requestUrl);
				request.Method = "POST";
				request.ContentType = "application/json";
				request.Timeout = 10000;

				byte[] payload = System.Text.Encoding.UTF8.GetBytes(json);
				request.ContentLength = payload.Length;

				using (var dataStream = request.GetRequestStream())
				{
					dataStream.Write(payload, 0, payload.Length);
				}

				using (var response = (System.Net.HttpWebResponse)request.GetResponse())
				{
					if (response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.Created)
					{
						ButtonText.Text = "Solicitado";
						Button.Background = new SolidColorBrush(Colors.LightGreen);
						ButtonText.Foreground = new SolidColorBrush(Colors.Black);
					}
					else
					{
						throw new Exception("Bad status");
					}
				}
			}
			catch
			{
				ButtonText.Text = "Fallo red";
				Button.Background = new SolidColorBrush(Colors.Red);
				Button.IsEnabled = true;
			}
		}

		private string EscapeJson(string value)
		{
			if (string.IsNullOrEmpty(value)) return "";
			return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
		}
	}
}
