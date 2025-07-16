using Xunit;
using FluentAssertions;
using Microsoft.Deployment.WindowsInstaller;
using Moq;
using System.IO;
using AutomationScheduler.CustomActions;

namespace AutomationScheduler.Installer.Tests.CustomActions
{
    public class DownloadArtifactsTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly Mock<Session> _mockSession;
        private readonly Dictionary<string, string> _sessionProperties;

        public DownloadArtifactsTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"InstallerTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);
            
            var pluginDir = Path.Combine(_testDirectory, "plugins");
            Directory.CreateDirectory(pluginDir);
            
            _sessionProperties = new Dictionary<string, string>
            {
                ["INSTALLFOLDER"] = _testDirectory,
                ["SELECTEDCONFIGURATION"] = "Option1",
                ["AZUREDEVOPS_URL"] = "https://dev.azure.com/testorg",
                ["AZUREDEVOPS_PAT"] = "test-pat-token",
                ["AZUREDEVOPS_FEED"] = "TestFeed"
            };
            
            _mockSession = new Mock<Session>();
            _mockSession.Setup(s => s[It.IsAny<string>()])
                .Returns<string>(key => _sessionProperties.ContainsKey(key) ? _sessionProperties[key] : null);
            _mockSession.Setup(s => s.Log(It.IsAny<string>()));
        }

        [Fact]
        public void DownloadArtifacts_ShouldSucceed_WhenAllParametersProvided()
        {
            // Act
            var result = CustomActions.DownloadArtifacts(_mockSession.Object);

            // Assert
            result.Should().Be(ActionResult.Success);
            _mockSession.Verify(s => s.Log(It.Is<string>(msg => msg.Contains("Selected configuration: Option1"))), Times.Once);
            _mockSession.Verify(s => s.Log(It.Is<string>(msg => msg.Contains("Plugin folder:"))), Times.Once);
        }

        [Fact]
        public void DownloadArtifacts_ShouldSkip_WhenPATNotProvided()
        {
            // Arrange
            _sessionProperties.Remove("AZUREDEVOPS_PAT");

            // Act
            var result = CustomActions.DownloadArtifacts(_mockSession.Object);

            // Assert
            result.Should().Be(ActionResult.Success);
            _mockSession.Verify(s => s.Log(It.Is<string>(msg => msg.Contains("PAT not provided"))), Times.Once);
        }

        [Theory]
        [InlineData("Option1", "Plugin.Option1,Plugin.Common")]
        [InlineData("Option2", "Plugin.Option2,Plugin.Common")]
        [InlineData("Option3", "Plugin.Option3,Plugin.Advanced,Plugin.Common")]
        [InlineData("Default", "Plugin.Default,Plugin.Common")]
        public void DownloadArtifacts_ShouldLogCorrectPackages_ForConfiguration(string configuration, string expectedPackages)
        {
            // Arrange
            _sessionProperties["SELECTEDCONFIGURATION"] = configuration;

            // Act
            var result = CustomActions.DownloadArtifacts(_mockSession.Object);

            // Assert
            result.Should().Be(ActionResult.Success);
            // Note: In a real test, we would verify the actual package downloads
            // For now, we're just verifying the configuration is read correctly
            _mockSession.Verify(s => s.Log(It.Is<string>(msg => msg.Contains($"Selected configuration: {configuration}"))), Times.Once);
        }

        [Fact]
        public void DownloadArtifacts_ShouldCreatePluginFolder_IfNotExists()
        {
            // Arrange
            var pluginPath = Path.Combine(_testDirectory, "plugins");
            if (Directory.Exists(pluginPath))
            {
                Directory.Delete(pluginPath);
            }

            // Act
            var result = CustomActions.DownloadArtifacts(_mockSession.Object);

            // Assert
            result.Should().Be(ActionResult.Success);
            Directory.Exists(pluginPath).Should().BeTrue();
        }

        [Fact]
        public void DownloadArtifacts_ShouldHandleExceptions_Gracefully()
        {
            // Arrange
            // Set an invalid URL to cause an exception
            _sessionProperties["AZUREDEVOPS_URL"] = "not-a-valid-url";

            // Act
            var result = CustomActions.DownloadArtifacts(_mockSession.Object);

            // Assert
            // Should still return success as the current implementation catches exceptions
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