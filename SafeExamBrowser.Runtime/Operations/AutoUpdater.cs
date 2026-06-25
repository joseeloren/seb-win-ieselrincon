using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Reflection;

namespace SafeExamBrowser.Runtime.Operations
{
    public static class AutoUpdater
    {
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
                        string apiVersion = versionMatch.Groups[1].Value;
                        string downloadUrl = downloadUrlMatch.Groups[1].Value;
                        
                        string currentVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                        
                        // Si la versión de la API es diferente a la del cliente (ej. la de la API es más nueva o distinta)
                        if (apiVersion != currentVersion)
                        {
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
                                {
                                    Process.Start(new ProcessStartInfo
                                    {
                                        FileName = "msiexec.exe",
                                        Arguments = $"/i \"{tempPath}\"",
                                        UseShellExecute = true
                                    });
                                }

                                progressWindow.Dispatcher.Invoke(() => 
                                {
                                    progressWindow.Close();
                                });
                            };

                            client.DownloadFileAsync(new Uri(downloadUrl), tempPath);
                            
                            // ShowDialog bloquea la ejecución hasta que la ventana se cierre
                            progressWindow.ShowDialog();
                            
                            // Si el usuario cierra la ventana forzosamente antes de terminar la descarga, cerramos la app.
                            Environment.Exit(0);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Si no hay red, bloqueamos por seguridad según requerimiento
                MessageBox.Show("No se pudo comprobar la versión de El Rincón Seguro. Compruebe su conexión a internet.", "Error de conexión", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }
    }
}
