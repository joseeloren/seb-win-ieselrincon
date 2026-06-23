/*
 * Copyright (c) 2026 Dpto. Informática IES El Rincón, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using SafeExamBrowser.UserInterface.Contracts.Windows.Data;

namespace SafeExamBrowser.UserInterface.Contracts.Windows
{
	/// <summary>
	/// Defines the functionality of a telemetry dialog.
	/// </summary>
	public interface ITelemetryDialog : IWindow
	{
		/// <summary>
		/// Shows the dialog as topmost window. If a parent window is specified, the dialog is rendered modally for the given parent.
		/// </summary>
		TelemetryDialogResult Show(IWindow parent = null);
	}
}
