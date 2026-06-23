/*
 * Copyright (c) 2026 Dpto. Informática IES El Rincón, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

namespace SafeExamBrowser.UserInterface.Contracts.Windows.Data
{
	/// <summary>
	/// Defines the user interaction result of an <see cref="ITelemetryDialog"/>.
	/// </summary>
	public class TelemetryDialogResult
	{
		/// <summary>
		/// The student name entered by the user.
		/// </summary>
		public string StudentName { get; set; }

		/// <summary>
		/// The exam code entered by the user.
		/// </summary>
		public string ExamCode { get; set; }

		/// <summary>
		/// Indicates whether the user confirmed the dialog or not.
		/// </summary>
		public bool Success { get; set; }
	}
}
