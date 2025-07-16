using Xunit;
using FluentAssertions;
using Microsoft.Deployment.WindowsInstaller;
using Moq;
using System.IO;
using Newtonsoft.Json.Linq;
using AutomationScheduler.CustomActions;

namespace AutomationScheduler.Installer.Tests.CustomActions
{
    public class UpdateConfigurationFilesTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly Mock<Session> _mockSession;
        private readonly Dictionary<string, string> _sessionProperties;

        public UpdateConfigurationFilesTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"InstallerTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);
            
            _sessionProperties = new Dictionary<string, string>
            {
                ["INSTALLFOLDER"] = _testDirectory,
                ["SELECTEDCONFIGURATION"] = "Option1"
            };
            
            _mockSession = new Mock<Session>();
            _mockSession.Setup(s => s[It.IsAny<string>()])
                .Returns<string>(key => _sessionProperties.ContainsKey(key) ? _sessionProperties[key] : null);
            _mockSession.Setup(s => s.Log(It.IsAny<string>()));
        }

        [Theory]
        [InlineData("Option1", false, true)]
        [InlineData("Option2", true, false)]
        [InlineData("Option3", true, false)]
        public void UpdateConfigurationFiles_ShouldUpdateConfig1_BasedOnSelection(
            string selectedConfig, bool expectedAdvancedLogging, bool expectedBasicMode)
        {
            // Arrange
            _sessionProperties["SELECTEDCONFIGURATION"] = selectedConfig;
            
            var config1Path = Path.Combine(_testDirectory, "config1.json");
            var initialConfig = new JObject
            {
                ["Features"] = new JObject
                {
                    ["AdvancedLogging"] = false,
                    ["BasicMode"] = false
                }
            };
            File.WriteAllText(config1Path, initialConfig.ToString());

            // Act
            var result = CustomActions.UpdateConfigurationFiles(_mockSession.Object);

            // Assert
            result.Should().Be(ActionResult.Success);
            
            var updatedConfig = JObject.Parse(File.ReadAllText(config1Path));
            updatedConfig["ConfigurationMode"].ToString().Should().Be(selectedConfig);
            updatedConfig["Features"]["AdvancedLogging"].Value<bool>().Should().Be(expectedAdvancedLogging);
            updatedConfig["Features"]["BasicMode"].Value<bool>().Should().Be(expectedBasicMode);
            updatedConfig["LastUpdated"].Should().NotBeNull();
        }

        [Fact]
        public void UpdateConfigurationFiles_ShouldAddEnterpriseFeatures_ForOption3()
        {
            // Arrange
            _sessionProperties["SELECTEDCONFIGURATION"] = "Option3";
            
            var config1Path = Path.Combine(_testDirectory, "config1.json");
            var initialConfig = new JObject
            {
                ["Features"] = new JObject()
            };
            File.WriteAllText(config1Path, initialConfig.ToString());

            // Act
            var result = CustomActions.UpdateConfigurationFiles(_mockSession.Object);

            // Assert
            result.Should().Be(ActionResult.Success);
            
            var updatedConfig = JObject.Parse(File.ReadAllText(config1Path));
            updatedConfig["Features"]["EnterpriseFeatures"].Value<bool>().Should().BeTrue();
        }

        [Fact]
        public void UpdateConfigurationFiles_ShouldUpdateConfig2_WithProfile()
        {
            // Arrange
            var config2Path = Path.Combine(_testDirectory, "config2.json");
            var initialConfig = new JObject
            {
                ["Profile"] = "Default",
                ["Settings"] = new JObject()
            };
            File.WriteAllText(config2Path, initialConfig.ToString());

            // Act
            var result = CustomActions.UpdateConfigurationFiles(_mockSession.Object);

            // Assert
            result.Should().Be(ActionResult.Success);
            
            var updatedConfig = JObject.Parse(File.ReadAllText(config2Path));
            updatedConfig["Profile"].ToString().Should().Be("Option1");
            updatedConfig["UpdatedBy"].ToString().Should().Be("Installer");
            updatedConfig["UpdatedAt"].Should().NotBeNull();
            // Verify original settings are preserved
            updatedConfig["Settings"].Should().NotBeNull();
        }

        [Fact]
        public void UpdateConfigurationFiles_ShouldHandleMissingFiles_Gracefully()
        {
            // Act - with no config files present
            var result = CustomActions.UpdateConfigurationFiles(_mockSession.Object);

            // Assert
            result.Should().Be(ActionResult.Success);
        }

        [Fact]
        public void UpdateConfigurationFiles_ShouldHandleCorruptedJson()
        {
            // Arrange
            var config1Path = Path.Combine(_testDirectory, "config1.json");
            File.WriteAllText(config1Path, "{ corrupted json ");

            // Act
            var result = CustomActions.UpdateConfigurationFiles(_mockSession.Object);

            // Assert
            result.Should().Be(ActionResult.Failure);
            _mockSession.Verify(s => s.Log(It.Is<string>(msg => msg.Contains("Error"))), Times.AtLeastOnce);
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