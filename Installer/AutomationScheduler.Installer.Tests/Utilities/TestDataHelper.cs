using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace AutomationScheduler.Installer.Tests.Utilities
{
    public static class TestDataHelper
    {
        public static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), $"InstallerTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(path);
            return path;
        }

        public static void CreateAppSettingsFile(string directory, JObject content = null)
        {
            var appSettings = content ?? new JObject
            {
                ["ConnectionStrings"] = new JObject
                {
                    ["DefaultConnection"] = "Server=localhost;Database=test;"
                },
                ["Logging"] = new JObject
                {
                    ["LogLevel"] = new JObject
                    {
                        ["Default"] = "Information",
                        ["Microsoft"] = "Warning"
                    }
                },
                ["AllowedHosts"] = "*"
            };

            var path = Path.Combine(directory, "appsettings.json");
            File.WriteAllText(path, appSettings.ToString());
        }

        public static void CreateConfigurationFiles(string directory)
        {
            // Create config1.json
            var config1 = new JObject
            {
                ["ConfigurationMode"] = "Default",
                ["Features"] = new JObject
                {
                    ["AdvancedLogging"] = false,
                    ["BasicMode"] = true,
                    ["DebugMode"] = false
                },
                ["Settings"] = new JObject
                {
                    ["MaxRetries"] = 3,
                    ["Timeout"] = 30
                }
            };
            File.WriteAllText(Path.Combine(directory, "config1.json"), config1.ToString());

            // Create config2.json
            var config2 = new JObject
            {
                ["Profile"] = "Default",
                ["Version"] = "1.0.0",
                ["Settings"] = new JObject
                {
                    ["Theme"] = "Light",
                    ["Language"] = "en-US"
                }
            };
            File.WriteAllText(Path.Combine(directory, "config2.json"), config2.ToString());
        }

        public static void CreateMockConsoleApplications(string directory)
        {
            // Create mock executable files
            var apps = new[] { "ConsoleApp1.exe", "ConsoleApp2.exe", "ConsoleApp3.exe" };
            
            foreach (var app in apps)
            {
                var path = Path.Combine(directory, app);
                File.WriteAllText(path, "Mock executable content");
                
                // Create associated files
                var baseName = Path.GetFileNameWithoutExtension(app);
                File.WriteAllText(Path.Combine(directory, $"{baseName}.dll"), "Mock DLL content");
                File.WriteAllText(Path.Combine(directory, $"{baseName}.deps.json"), "{}");
                File.WriteAllText(Path.Combine(directory, $"{baseName}.runtimeconfig.json"), 
                    @"{
                        ""runtimeOptions"": {
                            ""framework"": {
                                ""name"": ""Microsoft.NETCore.App"",
                                ""version"": ""8.0.0""
                            }
                        }
                    }");
            }
        }

        public static void CleanupDirectory(string directory)
        {
            if (Directory.Exists(directory))
            {
                try
                {
                    Directory.Delete(directory, true);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }

        public static JObject CreateInvalidJson()
        {
            // This creates a JObject that will fail when certain operations are performed
            var json = new JObject();
            json["Invalid"] = new JValue(DateTime.MinValue);
            return json;
        }
    }
}