using System;
using System.Diagnostics;
using System.IO;
using Xunit;
using FluentAssertions;
using AutomationScheduler.CustomActions;
using AutomationScheduler.Installer.Tests.Utilities;
using Newtonsoft.Json.Linq;

namespace AutomationScheduler.Installer.Tests.Performance
{
    public class CustomActionPerformanceTests : IDisposable
    {
        private readonly string _testDirectory;

        public CustomActionPerformanceTests()
        {
            _testDirectory = TestDataHelper.CreateTempDirectory();
        }

        [Fact]
        public void UpdateAppSettings_ShouldCompleteWithinTimeout()
        {
            // Arrange
            var largeAppSettings = new JObject();
            
            // Create a large appsettings file with many nested properties
            for (int i = 0; i < 100; i++)
            {
                largeAppSettings[$"Section{i}"] = new JObject();
                for (int j = 0; j < 50; j++)
                {
                    largeAppSettings[$"Section{i}"][$"Property{j}"] = $"Value{j}";
                }
            }
            
            TestDataHelper.CreateAppSettingsFile(_testDirectory, largeAppSettings);
            var mockSession = MockSessionFactory.CreateMockSessionWithInstallFolder(_testDirectory);

            // Act
            var stopwatch = Stopwatch.StartNew();
            var result = CustomActions.UpdateAppSettings(mockSession.Object);
            stopwatch.Stop();

            // Assert
            result.Should().Be(Microsoft.Deployment.WindowsInstaller.ActionResult.Success);
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, 
                "UpdateAppSettings should complete within 5 seconds even for large files");
        }

        [Fact]
        public void UpdateConfigurationFiles_ShouldHandleMultipleFilesEfficiently()
        {
            // Arrange
            // Create multiple configuration files
            for (int i = 1; i <= 10; i++)
            {
                var config = new JObject
                {
                    ["ConfigId"] = i,
                    ["Features"] = new JObject(),
                    ["Settings"] = new JObject()
                };
                
                for (int j = 0; j < 20; j++)
                {
                    config["Features"][$"Feature{j}"] = j % 2 == 0;
                    config["Settings"][$"Setting{j}"] = $"Value{j}";
                }
                
                File.WriteAllText(Path.Combine(_testDirectory, $"config{i}.json"), config.ToString());
            }
            
            var mockSession = MockSessionFactory.CreateMockSession(new Dictionary<string, string>
            {
                ["INSTALLFOLDER"] = _testDirectory,
                ["SELECTEDCONFIGURATION"] = "Option2"
            });

            // Act
            var stopwatch = Stopwatch.StartNew();
            var result = CustomActions.UpdateConfigurationFiles(mockSession.Object);
            stopwatch.Stop();

            // Assert
            result.Should().Be(Microsoft.Deployment.WindowsInstaller.ActionResult.Success);
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(3000,
                "UpdateConfigurationFiles should handle multiple files within 3 seconds");
        }

        [Fact]
        public void DownloadArtifacts_ShouldTimeoutGracefully()
        {
            // Arrange
            var mockSession = MockSessionFactory.CreateMockSessionForAzureDevOps(
                _testDirectory,
                azureDevOpsUrl: "https://invalid.url.that.will.timeout",
                pat: "invalid-pat"
            );

            // Act
            var stopwatch = Stopwatch.StartNew();
            var result = CustomActions.DownloadArtifacts(mockSession.Object);
            stopwatch.Stop();

            // Assert
            // Should fail but not hang indefinitely
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(30000,
                "DownloadArtifacts should timeout within 30 seconds for invalid URLs");
        }

        [Fact]
        public void AllCustomActions_ShouldHandleConcurrentExecution()
        {
            // This tests that custom actions don't have race conditions
            // when multiple instances might run (shouldn't happen in normal MSI, but good to test)
            
            // Arrange
            TestDataHelper.CreateAppSettingsFile(_testDirectory);
            TestDataHelper.CreateConfigurationFiles(_testDirectory);
            
            var tasks = new System.Threading.Tasks.Task<Microsoft.Deployment.WindowsInstaller.ActionResult>[10];
            
            // Act
            for (int i = 0; i < tasks.Length; i++)
            {
                var index = i;
                tasks[i] = System.Threading.Tasks.Task.Run(() =>
                {
                    var session = MockSessionFactory.CreateMockSession(new Dictionary<string, string>
                    {
                        ["INSTALLFOLDER"] = _testDirectory,
                        ["SELECTEDCONFIGURATION"] = index % 2 == 0 ? "Option1" : "Option2"
                    });
                    
                    return CustomActions.UpdateAppSettings(session.Object);
                });
            }
            
            var stopwatch = Stopwatch.StartNew();
            System.Threading.Tasks.Task.WaitAll(tasks);
            stopwatch.Stop();

            // Assert
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000,
                "Concurrent execution should complete within 10 seconds");
            
            tasks.Should().AllSatisfy(t => 
                t.Result.Should().Be(Microsoft.Deployment.WindowsInstaller.ActionResult.Success));
        }

        public void Dispose()
        {
            TestDataHelper.CleanupDirectory(_testDirectory);
        }
    }
}