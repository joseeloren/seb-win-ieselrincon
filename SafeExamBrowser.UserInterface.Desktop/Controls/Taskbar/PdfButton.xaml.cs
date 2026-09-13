/*
 * Copyright (c) 2026 Dpto. Informática IES El Rincón, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SafeExamBrowser.Core.Contracts.Resources.Icons;
using SafeExamBrowser.UserInterface.Contracts.Shell.Events;
using SafeExamBrowser.UserInterface.Shared.Utilities;

namespace SafeExamBrowser.UserInterface.Desktop.Controls.Taskbar
{
	internal partial class PdfButton : UserControl
	{
		internal event PdfButtonClickedEventHandler Clicked;

		public PdfButton()
		{
			InitializeComponent();
			LoadIcon();
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			Clicked?.Invoke();
		}

		private void LoadIcon()
		{
			Button.Content = new TextBlock { Text = "PDF", FontWeight = FontWeights.Bold, Margin = new Thickness(6, 0, 6, 0) };
		}
	}
}
