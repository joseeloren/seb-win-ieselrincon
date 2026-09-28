using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Reflection;
using System.Text;

namespace SafeExamBrowser.Runtime.Operations
{
    public static class AutoUpdater
    {
        private static string PowerShellLiteral(string value) => "'" + value.Replace("'", "''") + "'";

        private static void DeleteDownload(string path)
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
        }

        private static string BuildInstallerScript(string installerPath, string logPath, string expectedVersion)
        {
            var process = Process.GetCurrentProcess();
            var executable = process.MainModule.FileName;
            var target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "ElRinconSeguro", "Application", "SafeExamBrowser.exe");
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SafeExamBrowser.Runtime.Operations.InstallUpdate.ps1"))
            using (var reader = new StreamReader(stream))
            {
                return "$msi = " + PowerShellLiteral(installerPath) + "; $log = " + PowerShellLiteral(logPath) +
                    "; $exe = " + PowerShellLiteral(target) + "; $oldExe = " + PowerShellLiteral(executable) +
                    "; $parentId = " + process.Id + "; $parentStart = " + PowerShellLiteral(process.StartTime.ToUniversalTime().Ticks.ToString()) +
                    "; $expectedVersion = " + PowerShellLiteral(expectedVersion) + "; " + reader.ReadToEnd();
            }
        }

        private static void LaunchInstallerAndCleanup(string installerPath, string logPath, string expectedVersion)
        {
            string script = BuildInstallerScript(installerPath, logPath, expectedVersion);
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"),
                Arguments = "-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script)),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }

        public static bool CheckForUpdatesAndRun()
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

                            string downloadDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                "ElRinconSeguro", "Updates", Guid.NewGuid().ToString("N"));
                            Directory.CreateDirectory(downloadDirectory);
                            File.SetAttributes(Path.GetDirectoryName(downloadDirectory), FileAttributes.Directory | FileAttributes.Hidden);
                            File.SetAttributes(downloadDirectory, FileAttributes.Directory | FileAttributes.Hidden);
                            string tempPath = Path.Combine(downloadDirectory, "update.msi");
                            bool downloadSucceeded = false;
                            bool downloadFinished = false;
                            progressWindow.Closing += (s, e) =>
                            {
                                if (!downloadFinished && client.IsBusy)
                                {
                                    e.Cancel = true;
                                    client.CancelAsync();
                                }
                            };

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
                                downloadFinished = true;
                                if (e.Error == null && !e.Cancelled)
                                    downloadSucceeded = true;
                                else
                                    File.AppendAllText(logPath, $"[{DateTime.Now}] Download error: {e.Error?.Message}\n");

                                progressWindow.Dispatcher.Invoke(() => progressWindow.Close());
                            };

                            bool cleanupHandedOff = false;
                            try
                            {
                                client.DownloadFileAsync(new Uri(downloadUrl), tempPath);
                                progressWindow.ShowDialog();

                                if (downloadSucceeded && File.Exists(tempPath))
                                {
                                    File.SetAttributes(tempPath, FileAttributes.Hidden);
                                    // MajorUpgrade del MSI sustituye la versión anterior automáticamente.
                                    File.AppendAllText(logPath, $"[{DateTime.Now}] Launching upgrade installer: {tempPath}\n");
                                    LaunchInstallerAndCleanup(tempPath, logPath, apiVersion);
                                    cleanupHandedOff = true;
                                }
                            }
                            finally
                            {
                                if (!cleanupHandedOff)
                                {
                                    client.Dispose();
                                    DeleteDownload(tempPath);
                                    Directory.Delete(downloadDirectory);
                                }
                            }

                            return false;
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
                return false;
            }
            return true;
        }
    }
}
