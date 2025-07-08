using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Deployment.WindowsInstaller;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AutomationScheduler.CustomActions
{
    public class CustomActions
    {
        [WixToolset.Dtf.WindowsInstaller.CustomActionAttribute]
        public static WixToolset.Dtf.WindowsInstaller.ActionResult UpdateAppSettings(WixToolset.Dtf.WindowsInstaller.Session session)
        {
            session.Log("Begin UpdateAppSettings");

            try
            {
                string installFolder = session["INSTALLFOLDER"];
                string pluginPath = Path.Combine(installFolder, "plugins");
                string appSettingsPath = Path.Combine(installFolder, "appsettings.json");

                session.Log($"Updating appsettings.json at: {appSettingsPath}");
                session.Log($"Plugin path: {pluginPath}");

                if (File.Exists(appSettingsPath))
                {
                    // Read the existing appsettings.json
                    string json = File.ReadAllText(appSettingsPath);
                    JObject appSettings = JObject.Parse(json);

                    // Update or add the plugin path
                    appSettings["PluginSettings"] = appSettings["PluginSettings"] ?? new JObject();
                    appSettings["PluginSettings"]["PluginPath"] = pluginPath;

                    // Write back the updated settings
                    File.WriteAllText(appSettingsPath, appSettings.ToString(Formatting.Indented));
                    
                    session.Log("Successfully updated appsettings.json");
                }
                else
                {
                    session.Log($"appsettings.json not found at: {appSettingsPath}");
                    return WixToolset.Dtf.WindowsInstaller.ActionResult.Failure;
                }

                return WixToolset.Dtf.WindowsInstaller.ActionResult.Success;
            }
            catch (Exception ex)
            {
                session.Log($"Error in UpdateAppSettings: {ex.Message}");
                return WixToolset.Dtf.WindowsInstaller.ActionResult.Failure;
            }
        }

        [WixToolset.Dtf.WindowsInstaller.CustomActionAttribute]
        public static WixToolset.Dtf.WindowsInstaller.ActionResult DownloadArtifacts(WixToolset.Dtf.WindowsInstaller.Session session)
        {
            session.Log("Begin DownloadArtifacts");

            try
            {
                string selectedConfig = session["SELECTEDCONFIGURATION"];
                string installFolder = session["INSTALLFOLDER"];
                string pluginFolder = Path.Combine(installFolder, "plugins");

                session.Log($"Selected configuration: {selectedConfig}");
                session.Log($"Plugin folder: {pluginFolder}");

                // Azure DevOps connection settings (these should be configured appropriately)
                string azureDevOpsUrl = session["AZUREDEVOPS_URL"] ?? "https://dev.azure.com/yourorganization";
                string personalAccessToken = session["AZUREDEVOPS_PAT"] ?? "";
                string feedName = session["AZUREDEVOPS_FEED"] ?? "YourFeedName";

                if (string.IsNullOrEmpty(personalAccessToken))
                {
                    session.Log("Azure DevOps PAT not provided. Skipping artifact download.");
                    return WixToolset.Dtf.WindowsInstaller.ActionResult.Success;
                }

                // Download artifacts based on configuration
                Task.Run(async () =>
                {
                    await DownloadNuGetPackagesAsync(session, azureDevOpsUrl, personalAccessToken, 
                        feedName, selectedConfig, pluginFolder);
                }).Wait();

                return WixToolset.Dtf.WindowsInstaller.ActionResult.Success;
            }
            catch (Exception ex)
            {
                session.Log($"Error in DownloadArtifacts: {ex.Message}");
                return WixToolset.Dtf.WindowsInstaller.ActionResult.Failure;
            }
        }

        private static async Task DownloadNuGetPackagesAsync(WixToolset.Dtf.WindowsInstaller.Session session, string azureDevOpsUrl, 
            string pat, string feedName, string configuration, string targetFolder)
        {
            try
            {
                // Create NuGet source with Azure DevOps feed
                var packageSource = new PackageSource($"{azureDevOpsUrl}/_packaging/{feedName}/nuget/v3/index.json")
                {
                    Credentials = new PackageSourceCredential(
                        source: $"{azureDevOpsUrl}/_packaging/{feedName}/nuget/v3/index.json",
                        username: "VssSessionToken",
                        passwordText: pat,
                        isPasswordClearText: true,
                        validAuthenticationTypesText: null)
                };

                var repository = Repository.Factory.GetCoreV3(packageSource);
                var resource = await repository.GetResourceAsync<PackageSearchResource>();

                // Define packages to download based on configuration
                string[] packageIds = configuration switch
                {
                    "Option1" => new[] { "Plugin.Option1", "Plugin.Common" },
                    "Option2" => new[] { "Plugin.Option2", "Plugin.Common" },
                    "Option3" => new[] { "Plugin.Option3", "Plugin.Advanced", "Plugin.Common" },
                    _ => new[] { "Plugin.Default", "Plugin.Common" }
                };

                session.Log($"Downloading packages for configuration '{configuration}': {string.Join(", ", packageIds)}");

                // Download each package
                foreach (var packageId in packageIds)
                {
                    session.Log($"Downloading package: {packageId}");
                    // Implementation for downloading and extracting NuGet packages
                    // This would involve using NuGet.Protocol to download and extract the packages
                }
            }
            catch (Exception ex)
            {
                session.Log($"Error downloading NuGet packages: {ex.Message}");
                throw;
            }
        }

        [WixToolset.Dtf.WindowsInstaller.CustomActionAttribute]
        public static WixToolset.Dtf.WindowsInstaller.ActionResult UpdateConfigurationFiles(WixToolset.Dtf.WindowsInstaller.Session session)
        {
            session.Log("Begin UpdateConfigurationFiles");

            try
            {
                string selectedConfig = session["SELECTEDCONFIGURATION"];
                string installFolder = session["INSTALLFOLDER"];
                
                session.Log($"Updating configuration files for: {selectedConfig}");

                // Update config1.json
                string config1Path = Path.Combine(installFolder, "config1.json");
                if (File.Exists(config1Path))
                {
                    var config1 = JObject.Parse(File.ReadAllText(config1Path));
                    
                    // Update configuration based on selection
                    config1["ConfigurationMode"] = selectedConfig;
                    config1["LastUpdated"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    
                    // Add configuration-specific settings
                    switch (selectedConfig)
                    {
                        case "Option1":
                            config1["Features"]["AdvancedLogging"] = false;
                            config1["Features"]["BasicMode"] = true;
                            break;
                        case "Option2":
                            config1["Features"]["AdvancedLogging"] = true;
                            config1["Features"]["BasicMode"] = false;
                            break;
                        case "Option3":
                            config1["Features"]["AdvancedLogging"] = true;
                            config1["Features"]["BasicMode"] = false;
                            config1["Features"]["EnterpriseFeatures"] = true;
                            break;
                    }
                    
                    File.WriteAllText(config1Path, config1.ToString(Formatting.Indented));
                    session.Log("Updated config1.json");
                }

                // Update config2.json
                string config2Path = Path.Combine(installFolder, "config2.json");
                if (File.Exists(config2Path))
                {
                    var config2 = JObject.Parse(File.ReadAllText(config2Path));
                    
                    // Update configuration based on selection
                    config2["Profile"] = selectedConfig;
                    config2["UpdatedBy"] = "Installer";
                    config2["UpdatedAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    
                    File.WriteAllText(config2Path, config2.ToString(Formatting.Indented));
                    session.Log("Updated config2.json");
                }

                return WixToolset.Dtf.WindowsInstaller.ActionResult.Success;
            }
            catch (Exception ex)
            {
                session.Log($"Error in UpdateConfigurationFiles: {ex.Message}");
                return WixToolset.Dtf.WindowsInstaller.ActionResult.Failure;
            }
        }
    }
}
