/*
 * Copyright (c) 2026 Dpto. Informática IES El Rincón, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SafeExamBrowser.UserInterface.Shared.Utilities
{
	public static class WindowExtensions
	{
		private const int GWL_STYLE = -16;
		private const uint MF_BYCOMMAND = 0x0;
		private const uint MF_ENABLED = 0x0;
		private const uint MF_GRAYED = 0x1;
		private const uint SC_CLOSE = 0xF060;
		private const uint SWP_SHOWWINDOW = 0x40;
		private const uint WDA_EXCLUDEFROMCAPTURE = 0x11;
		private const int WS_SYSMENU = 0x80000;

		private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

		// Recover modifiers carried across RDP focus/desktop changes. Only key-up
		// events are sent, so recovery cannot invoke a shortcut or type text.
		public static bool ReleaseRemoteModifiers()
		{
			if (GetSystemMetrics(0x1000) == 0) return true; // SM_REMOTESESSION
			var keys = new ushort[] { 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C };
			var inputs = new System.Collections.Generic.List<RecoveryInput>();
			foreach (var key in keys)
			{
				if ((GetAsyncKeyState(key) & 0x8000) != 0)
				{
					inputs.Add(new RecoveryInput { Type = 1, Data = new RecoveryUnion {
						Keyboard = new RecoveryKeyboard { Key = key, Flags = 2 | (key == 0xA3 || key == 0xA5 || key == 0x5B || key == 0x5C ? 1u : 0u) }
					} });
				}
			}
			return inputs.Count == 0 || SendInput((uint) inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(RecoveryInput))) == inputs.Count;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct RecoveryInput { public uint Type; public RecoveryUnion Data; }
		[StructLayout(LayoutKind.Explicit)]
		private struct RecoveryUnion
		{
			[FieldOffset(0)] public RecoveryKeyboard Keyboard;
			[FieldOffset(0)] public RecoveryMouse Mouse; // Preserves native INPUT union size/alignment.
		}
		[StructLayout(LayoutKind.Sequential)]
		private struct RecoveryKeyboard { public ushort Key, Scan; public uint Flags, Time; public UIntPtr Extra; }
		[StructLayout(LayoutKind.Sequential)]
		private struct RecoveryMouse { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
		[DllImport("user32.dll")]
		private static extern int GetSystemMetrics(int index);
		[DllImport("user32.dll")]
		private static extern short GetAsyncKeyState(int key);
		[DllImport("user32.dll", SetLastError = true)]
		private static extern uint SendInput(uint count, RecoveryInput[] inputs, int size);

		public static void DisableCloseButton(this Window window)
		{
			var helper = new WindowInteropHelper(window);
			var systemMenu = GetSystemMenu(helper.Handle, false);

			if (systemMenu != IntPtr.Zero)
			{
				EnableMenuItem(systemMenu, SC_CLOSE, MF_BYCOMMAND | MF_GRAYED);
			}
		}

		public static void EnableCloseButton(this Window window)
		{
			var helper = new WindowInteropHelper(window);
			var systemMenu = GetSystemMenu(helper.Handle, false);

			if (systemMenu != IntPtr.Zero)
			{
				EnableMenuItem(systemMenu, SC_CLOSE, MF_BYCOMMAND | MF_ENABLED);
			}
		}

		public static bool ExcludeFromCapture(this Window window)
		{
			var helper = new WindowInteropHelper(window);

			return SetWindowDisplayAffinity(helper.Handle, WDA_EXCLUDEFROMCAPTURE);
		}

		public static void ExecuteWithAccess(this Window window, Action action)
		{
			if (window.CheckAccess())
			{
				action();
			}
			else
			{
				window.Dispatcher.Invoke(action);
			}
		}

		public static void HideCloseButton(this Window window)
		{
			var helper = new WindowInteropHelper(window);
			var style = GetWindowLong(helper.Handle, GWL_STYLE) & ~WS_SYSMENU;

			SetWindowLong(helper.Handle, GWL_STYLE, style);
		}

		public static void MoveToBackground(this Window window)
		{
			var helper = new WindowInteropHelper(window);
			var x = (int) window.TransformFromPhysical(window.Left, 0).X;
			var y = (int) window.TransformFromPhysical(0, window.Top).Y;
			var width = (int) window.TransformFromPhysical(window.Width, 0).X;
			var height = (int) window.TransformFromPhysical(0, window.Height).Y;

			SetWindowPos(helper.Handle, HWND_BOTTOM, x, y, width, height, SWP_SHOWWINDOW);
		}

		[DllImport("user32.dll")]
		private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

		[DllImport("user32.dll")]
		private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

		[DllImport("user32.dll")]
		private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll")]
		public static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint dwAffinity);

		[DllImport("user32.dll")]
		private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

		[DllImport("user32.dll")]
		private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
	}
}
