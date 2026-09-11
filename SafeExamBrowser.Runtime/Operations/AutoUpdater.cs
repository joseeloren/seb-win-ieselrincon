using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Reflection;
using System.Threading;
using Microsoft.Win32;

namespace SafeExamBrowser.Runtime.Operations
{
    public static class AutoUpdater
    {
        // UpgradeCode fijo del instalador WiX (Product.wxs)
        private const string UpgradeCode = "{97A8B13E-48FB-4BE1-A7C2-DD1863F95CCB}";

        /// <summary>
        /// Busca el ProductCode instalado actualmente usando el UpgradeCode en el registro de Windows.
        /// Devuelve null si no se encuentra ninguna instalación.
        /// </summary>
        private static string FindInstalledProductCode()
        {
            try
            {
                // La clave de upgrades está en HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UpgradeCodes
                // con el UpgradeCode transformado (invertido por bloques)
                string transformedUpgrade = TransformGuid(UpgradeCode);
                string upgradeKeyPath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UpgradeCodes\{transformedUpgrade}";

                using (var key = Registry.LocalMachine.OpenSubKey(upgradeKeyPath))
                {
                    if (key != null)
                    {
                        foreach (var valueName in key.GetValueNames())
                        {
                            // Cada valor es un ProductCode transformado — lo revertimos
                            string productCode = ReverseTransformGuid(valueName);
                            if (!string.IsNullOrEmpty(productCode))
                            {
                                return productCode;
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Transforma un GUID al formato comprimido que usa Windows Installer en el registro.
        /// </summary>
        private static string TransformGuid(string guid)
        {
            guid = guid.Trim('{', '}').Replace("-", "").ToUpper();
            if (guid.Length != 32) return guid;

            return Reverse(guid.Substring(0, 8))
                 + Reverse(guid.Substring(8, 4))
                 + Reverse(guid.Substring(12, 4))
                 + Reverse(guid.Substring(16, 2)) + Reverse(guid.Substring(18, 2))
                 + Reverse(guid.Substring(20, 2)) + Reverse(guid.Substring(22, 2))
                 + Reverse(guid.Substring(24, 2)) + Reverse(guid.Substring(26, 2))
                 + Reverse(guid.Substring(28, 2)) + Reverse(guid.Substring(30, 2));
        }

        private static string ReverseTransformGuid(string compressed)
        {
            try
            {
                if (compressed.Length != 32) return null;
                string p1 = Reverse(compressed.Substring(0, 8));
                string p2 = Reverse(compressed.Substring(8, 4));
                string p3 = Reverse(compressed.Substring(12, 4));
                string p4a = Reverse(compressed.Substring(16, 2)) + Reverse(compressed.Substring(18, 2));
                string p4b = Reverse(compressed.Substring(20, 2)) + Reverse(compressed.Substring(22, 2))
                           + Reverse(compressed.Substring(24, 2)) + Reverse(compressed.Substring(26, 2))
                           + Reverse(compressed.Substring(28, 2)) + Reverse(compressed.Substring(30, 2));
                return $"{{{p1}-{p2}-{p3}-{p4a}-{p4b}}}";
            }
            catch { return null; }
        }

        private static string Reverse(string s)
        {
            var arr = s.ToCharArray();
            Array.Reverse(arr);
            return new string(arr);
        }

        /// <summary>
        /// Lanza msiexec con los argumentos dados, espera hasta 5 minutos y devuelve el código de salida.
        /// </summary>
        private static int RunMsiexec(string arguments, string logPath)
        {
            File.AppendAllText(logPath, $"[{DateTime.Now}] Running: msiexec {arguments}\n");
            var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "msiexec.exe",
                Arguments = arguments,
                UseShellExecute = true
            });
            proc.WaitForExit(300000); // máx 5 min
            int exitCode = proc.HasExited ? proc.ExitCode : -1;
            File.AppendAllText(logPath, $"[{DateTime.Now}] msiexec exit code: {exitCode}\n");
            return exitCode;
        }

        public static void CheckForUpdatesAndRun()
        {
            try
            {
                using (var client = new WebClient())
                {
                    string url = $"{SafeExamBrowser.Core.Contracts.ApiConstants.BaseUrl}/api/version";
                    string json = client.DownloadString(url);

                    var versionMatch = Regex.Match(json, @"""version""\s*:\s*""([^""]+)""");
                    var downloadUrlMatch = Regex.Match(json, @"""downloadUrl""\s*:\s*""([^""]+)""");

                    if (versionMatch.Success && downloadUrlMatch.Success)
                    {
                        string apiVersion = versionMatch.Groups[1].Value.Trim();
                        string downloadUrl = downloadUrlMatch.Groups[1].Value;

                        string currentVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

                        string logPath = Path.Combine(Path.GetTempPath(), "ElArrinconador_update.log");
                        File.AppendAllText(logPath, $"[{DateTime.Now}] API version: '{apiVersion}' | Current version: '{currentVersion}'\n");

                        if (Version.TryParse(apiVersion, out Version apiVer) && Version.TryParse(currentVersion, out Version currentVer) && apiVer > currentVer)
                        {
                            File.AppendAllText(logPath, $"[{DateTime.Now}] Update needed: {apiVer} > {currentVer}\n");

                            // --- Ventana de descarga con barra de progreso ---
                            System.Windows.Window progressWindow = new System.Windows.Window
                            {
                                Title = "Actualizando El Rincón Seguro",
                                Width = 450,
                                Height = 150,
                                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                                ResizeMode = System.Windows.ResizeMode.NoResize,
                                Topmost = true
                            };

                            System.Windows.Controls.StackPanel panel = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(20) };
                            System.Windows.Controls.TextBlock label = new System.Windows.Controls.TextBlock { Text = $"Descargando versión {apiVersion}...", Margin = new System.Windows.Thickness(0, 0, 0, 10) };
                            System.Windows.Controls.ProgressBar progressBar = new System.Windows.Controls.ProgressBar { Height = 25, Minimum = 0, Maximum = 100 };

                            panel.Children.Add(label);
                            panel.Children.Add(progressBar);
                            progressWindow.Content = panel;

                            string tempPath = Path.Combine(Path.GetTempPath(), "ElArrinconadorUpdate.msi");
                            bool downloadSucceeded = false;

                            client.DownloadProgressChanged += (s, e) =>
                            {
                                progressWindow.Dispatcher.Invoke(() =>
                                {
                                    progressBar.Value = e.ProgressPercentage;
                                    double receivedMB = e.BytesReceived / 1048576.0;
                                    double totalMB = e.TotalBytesToReceive / 1048576.0;
                                    label.Text = $"Descargando versión {apiVersion}... {receivedMB:F1} MB / {totalMB:F1} MB";
                                });
                            };

                            client.DownloadFileCompleted += (s, e) =>
                            {
                                if (e.Error == null && !e.Cancelled)
                                    downloadSucceeded = true;
                                else
                                    File.AppendAllText(logPath, $"[{DateTime.Now}] Download error: {e.Error?.Message}\n");

                                progressWindow.Dispatcher.Invoke(() => progressWindow.Close());
                            };

                            client.DownloadFileAsync(new Uri(downloadUrl), tempPath);
                            progressWindow.ShowDialog();

                            if (downloadSucceeded && File.Exists(tempPath))
                            {
                                bool installed = false;

                                // --- Paso 1: REPARAR si hay una instalación previa ---
                                string installedProductCode = FindInstalledProductCode();

                                if (!string.IsNullOrEmpty(installedProductCode))
                                {
                                    File.AppendAllText(logPath, $"[{DateTime.Now}] Found installed product: {installedProductCode}. Attempting repair...\n");
                                    // /fecums: reinstala ficheros faltantes/corruptos, shortcuts y entradas de registro
                                    int repairCode = RunMsiexec($"/fecums \"{tempPath}\"", logPath);
                                    installed = (repairCode == 0);

                                    if (installed)
                                        File.AppendAllText(logPath, $"[{DateTime.Now}] Repair succeeded.\n");
                                    else
                                        File.AppendAllText(logPath, $"[{DateTime.Now}] Repair failed (exit {repairCode}). Falling back to full reinstall...\n");
                                }
                                else
                                {
                                    File.AppendAllText(logPath, $"[{DateTime.Now}] No existing installation found. Fresh install.\n");
                                }

                                // --- Paso 2: REINSTALAR si la reparación falló o no había instalación ---
                                if (!installed)
                                {
                                    File.AppendAllText(logPath, $"[{DateTime.Now}] Launching full installer: {tempPath}\n");
                                    Process.Start(new ProcessStartInfo
                                    {
                                        FileName = "msiexec.exe",
                                        Arguments = $"/i \"{tempPath}\"",
                                        UseShellExecute = true
                                    });
                                    Thread.Sleep(2000);
                                }
                            }

                            Environment.Exit(0);
                        }
                        else
                        {
                            File.AppendAllText(logPath, $"[{DateTime.Now}] No update needed.\n");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    string logPath = Path.Combine(Path.GetTempPath(), "ElArrinconador_update.log");
                    File.AppendAllText(logPath, $"[{DateTime.Now}] Exception: {ex}\n");
                }
                catch { }

                MessageBox.Show("No se pudo comprobar la versión de El Rincón Seguro. Compruebe su conexión a internet.", "Error de conexión", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }
    }
}
