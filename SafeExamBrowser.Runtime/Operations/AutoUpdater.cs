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
                    string url = "https://cf289235-5e01-4122-afbe.a2a2d422d2c6.sites.escritorios.ieselrincon.es/api/version";
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
                            MessageBox.Show($"Hay una actualización obligatoria ({apiVersion}). La versión actual es {currentVersion}. Descargando el nuevo instalador...", "Actualización Obligatoria", MessageBoxButton.OK, MessageBoxImage.Information);
                            
                            string tempPath = Path.Combine(Path.GetTempPath(), "ElArrinconadorUpdate.msi");
                            client.DownloadFile(downloadUrl, tempPath);
                            
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "msiexec.exe",
                                Arguments = $"/i \"{tempPath}\"",
                                UseShellExecute = true
                            });
                            
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
