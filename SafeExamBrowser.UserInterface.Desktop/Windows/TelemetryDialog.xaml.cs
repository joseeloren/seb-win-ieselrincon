/*
 * Copyright (c) 2026 Dpto. Informática IES El Rincón, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System.Windows;
using System.Windows.Input;
using SafeExamBrowser.I18n.Contracts;
using SafeExamBrowser.UserInterface.Contracts.Windows;
using SafeExamBrowser.UserInterface.Contracts.Windows.Data;
using SafeExamBrowser.UserInterface.Contracts.Windows.Events;

namespace SafeExamBrowser.UserInterface.Desktop.Windows
{
	internal partial class TelemetryDialog : Window, ITelemetryDialog
	{
		private readonly IText text;

		private WindowClosedEventHandler closed;
		private WindowClosingEventHandler closing;

		event WindowClosedEventHandler IWindow.Closed
		{
			add { closed += value; }
			remove { closed -= value; }
		}

		event WindowClosingEventHandler IWindow.Closing
		{
			add { closing += value; }
			remove { closing -= value; }
		}

		internal TelemetryDialog(IText text)
		{
			this.text = text;

			InitializeComponent();
			InitializeTelemetryDialog();
		}

		public void BringToForeground()
		{
			Dispatcher.Invoke(Activate);
		}

		public TelemetryDialogResult Show(IWindow parent = null)
		{
			return Dispatcher.Invoke(() =>
			{
				var result = new TelemetryDialogResult { Success = false };

				if (parent is Window)
				{
					Owner = parent as Window;
					WindowStartupLocation = WindowStartupLocation.CenterOwner;
				}

				if (ShowDialog() is true)
				{
					result.StudentName = StudentName.Text;
					result.ExamCode = ExamCode.Text;
					result.Success = true;
				}

				return result;
			});
		}

		private void InitializeTelemetryDialog()
		{
			WindowStartupLocation = WindowStartupLocation.CenterScreen;

			Closed += (o, args) => closed?.Invoke();
			Closing += (o, args) => closing?.Invoke();
			Loaded += (o, args) => Activate();

			ConfirmButton.Click += ConfirmButton_Click;

			StudentName.KeyDown += TextBox_KeyDown;
			ExamCode.KeyDown += TextBox_KeyDown;
		}

		private void ConfirmButton_Click(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(StudentName.Text) || string.IsNullOrWhiteSpace(ExamCode.Text))
			{
				MessageBox.Show(this, "Debe introducir tanto el correo como el Código del Examen.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			if (!StudentName.Text.Trim().EndsWith("@alumno.ieselrincon.es", System.StringComparison.OrdinalIgnoreCase))
			{
				MessageBox.Show(this, "El correo de alumno debe ser un correo válido terminado en @alumno.ieselrincon.es", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			try
			{
				ConfirmButton.IsEnabled = false;
				Mouse.OverrideCursor = Cursors.Wait;

				using (var client = new System.Net.WebClient())
				{
					string url = $"{SafeExamBrowser.Core.Contracts.ApiConstants.BaseUrl}/api/exams/{System.Uri.EscapeDataString(ExamCode.Text.Trim())}/validate?student={System.Uri.EscapeDataString(StudentName.Text.Trim())}";
					client.DownloadString(url);
				}
			}
			catch (System.Net.WebException ex)
			{
				if (ex.Response is System.Net.HttpWebResponse response)
				{
					if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
					{
						MessageBox.Show(this, "El examen no existe o no está activo.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
						return;
					}
					else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
					{
						MessageBox.Show(this, "Este alumno ya está conectado en el examen.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
						return;
					}
				}
				MessageBox.Show(this, "Error de conexión: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}
			finally
			{
				ConfirmButton.IsEnabled = true;
				Mouse.OverrideCursor = null;
			}

			DialogResult = true;
			Close();
		}

		private void TextBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				ConfirmButton_Click(sender, e);
			}
		}
	}
}
