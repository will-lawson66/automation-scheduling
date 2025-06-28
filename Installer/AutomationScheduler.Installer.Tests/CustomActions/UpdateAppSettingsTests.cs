using Xunit;
using FluentAssertions;
using Microsoft.Deployment.WindowsInstaller;
using Moq;
using System.IO;
using Newtonsoft.Json.Linq;
using AutomationScheduler.CustomActions;

namespace AutomationScheduler.Installer.Tests.CustomActions
{
    public class UpdateAppSettingsTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly Mock<Session> _mockSession;
        private readonly Dictionary<string, string> _sessionProperties;

        public UpdateAppSettingsTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"InstallerTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);
            
            _sessionProperties = new Dictionary<string, string>
            {
                ["INSTALLFOLDER"] = _testDirectory
            };
            
            _mockSession = new Mock<Session>();
            _mockSession.Setup(s => s[It.IsAny<string>()])
                .Returns<string>(key => _sessionProperties.ContainsKey(key) ? _sessionProperties[key] : null);
            _mockSession.Setup(s => s.Log(It.IsAny<string>()));
        }

        [Fact]
        public void UpdateAppSettings_ShouldUpdatePluginPath_WhenAppSettingsExists()
        {
            // Arrange
            var appSettingsPath = Path.Combine(_testDirectory, "appsettings.json");
            var initialSettings = new JObject
            {
                ["ConnectionStrings"] = new JObject
                {
                    ["DefaultConnection"] = "Server=localhost;Database=test;"
                },
                ["Logging"] = new JObject
                {
                    ["LogLevel"] = new JObject
                    {
                        ["Default"] = "Information"
                    }
                }
            };
            File.WriteAllText(appSettingsPath, initialSettings.ToString());

            // Act
            var result = CustomActions.UpdateAppSettings(_mockSession.Object);

            // Assert
            result.Should().Be(ActionResult.Success);
            
            var updatedSettings = JObject.Parse(File.ReadAllText(appSettingsPath));
            updatedSettings["PluginSettings"].Should().NotBeNull();
            updatedSettings["PluginSettings"]["PluginPath"].ToString().Should().Be(Path.Combine(_testDirectory, "plugins"));
            
            // Verify original settings are preserved
            updatedSettings["ConnectionStrings"]["DefaultConnection"].ToString().Should().Be("Server=localhost;Database=test;");
        }

        [Fact]
        public void UpdateAppSettings_ShouldReturnFailure_WhenAppSettingsDoesNotExist()
        {
            // Act
            var result = CustomActions.UpdateAppSettings(_mockSession.Object);

            // Assert
            result.Should().Be(ActionResult.Failure);
            _mockSession.Verify(s => s.Log(It.Is<string>(msg => msg.Contains("not found"))), Times.Once);
        }

        [Fact]
        public void UpdateAppSettings_ShouldHandleInvalidJson()
        {
            // Arrange
            var appSettingsPath = Path.Combine(_testDirectory, "appsettings.json");
            File.WriteAllText(appSettingsPath, "{ invalid json }");

            // Act
            var result = CustomActions.UpdateAppSettings(_mockSession.Object);

            // Assert
            result.Should().Be(ActionResult.Failure);
            _mockSession.Verify(s => s.Log(It.Is<string>(msg => msg.Contains("Error"))), Times.AtLeastOnce);
        }

        [Fact]
        public void UpdateAppSettings_ShouldOverwriteExistingPluginPath()
        {
            // Arrange
            var appSettingsPath = Path.Combine(_testDirectory, "appsettings.json");
            var initialSettings = new JObject
            {
                ["PluginSettings"] = new JObject
                {
                    ["PluginPath"] = "C:\\old\\path"
                }
            };
            File.WriteAllText(appSettingsPath, initialSettings.ToString());

            // Act
            var result = CustomActions.UpdateAppSettings(_mockSession.Object);

            // Assert
            result.Should().Be(ActionResult.Success);
            
            var updatedSettings = JObject.Parse(File.ReadAllText(appSettingsPath));
            updatedSettings["PluginSettings"]["PluginPath"].ToString().Should().Be(Path.Combine(_testDirectory, "plugins"));
            updatedSettings["PluginSettings"]["PluginPath"].ToString().Should().NotBe("C:\\old\\path");
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
    }
}