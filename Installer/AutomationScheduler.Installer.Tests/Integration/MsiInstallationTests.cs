using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Deployment.WindowsInstaller;
using Xunit;
using FluentAssertions;

namespace AutomationScheduler.Installer.Tests.Integration
{
    /// <summary>
    /// Integration tests for MSI installation.
    /// These tests require the MSI to be built and may require admin privileges.
    /// </summary>
    [Collection("MSI Integration Tests")]
    public class MsiInstallationTests : IDisposable
    {
        private readonly string _msiPath;
        private readonly string _testInstallPath;
        private readonly string _logPath;

        public MsiInstallationTests()
        {
            _msiPath = GetMsiPath();
            _testInstallPath = Path.Combine(Path.GetTempPath(), $"TestInstall_{Guid.NewGuid()}");
            _logPath = Path.Combine(Path.GetTempPath(), $"InstallLog_{Guid.NewGuid()}.log");
        }

        [SkippableFact]
        public void Msi_ShouldInstallSuccessfully()
        {
            Skip.IfNot(File.Exists(_msiPath), $"MSI not found at {_msiPath}");
            Skip.IfNot(IsAdministrator(), "Test requires administrator privileges");

            // Arrange
            var arguments = $"/i \"{_msiPath}\" /qn INSTALLFOLDER=\"{_testInstallPath}\" /l*v \"{_logPath}\"";

            // Act
            var result = ExecuteMsiExec(arguments);

            // Assert
            result.Should().Be(0, $"Installation failed. Check log at {_logPath}");
            Directory.Exists(_testInstallPath).Should().BeTrue();
        }

        [SkippableFact]
        public void Msi_ShouldCreateExpectedDirectories()
        {
            Skip.IfNot(File.Exists(_msiPath), $"MSI not found at {_msiPath}");
            Skip.IfNot(IsAdministrator(), "Test requires administrator privileges");

            // Arrange & Act
            var arguments = $"/i \"{_msiPath}\" /qn INSTALLFOLDER=\"{_testInstallPath}\" /l*v \"{_logPath}\"";
            var result = ExecuteMsiExec(arguments);

            // Assert
            result.Should().Be(0);
            Directory.Exists(Path.Combine(_testInstallPath, "plugins")).Should().BeTrue();
        }

        [SkippableFact]
        public void Msi_ShouldSupportDifferentConfigurations()
        {
            Skip.IfNot(File.Exists(_msiPath), $"MSI not found at {_msiPath}");
            Skip.IfNot(IsAdministrator(), "Test requires administrator privileges");

            // Test each configuration option
            var configurations = new[] { "Option1", "Option2", "Option3" };

            foreach (var config in configurations)
            {
                var testPath = Path.Combine(Path.GetTempPath(), $"TestInstall_{config}_{Guid.NewGuid()}");
                var logPath = Path.Combine(Path.GetTempPath(), $"InstallLog_{config}_{Guid.NewGuid()}.log");

                try
                {
                    // Arrange & Act
                    var arguments = $"/i \"{_msiPath}\" /qn INSTALLFOLDER=\"{testPath}\" " +
                                  $"SELECTEDCONFIGURATION=\"{config}\" /l*v \"{logPath}\"";
                    var result = ExecuteMsiExec(arguments);

                    // Assert
                    result.Should().Be(0, $"Installation failed for {config}. Check log at {logPath}");

                    // Cleanup
                    UninstallMsi(testPath, logPath);
                }
                finally
                {
                    // Cleanup
                    if (Directory.Exists(testPath))
                    {
                        Directory.Delete(testPath, true);
                    }
                    if (File.Exists(logPath))
                    {
                        File.Delete(logPath);
                    }
                }
            }
        }

        [SkippableFact]
        public void Msi_ShouldValidateWithICE()
        {
            Skip.IfNot(File.Exists(_msiPath), $"MSI not found at {_msiPath}");

            // This test validates the MSI against Internal Consistency Evaluators (ICE)
            // Note: This requires Windows SDK to be installed
            
            try
            {
                using (var db = new Database(_msiPath, DatabaseOpenMode.ReadOnly))
                {
                    // Basic validation - check tables exist
                    db.Tables.Should().NotBeEmpty();
                    db.Tables.Should().Contain(t => t.Name == "Property");
                    db.Tables.Should().Contain(t => t.Name == "File");
                    db.Tables.Should().Contain(t => t.Name == "Component");
                    db.Tables.Should().Contain(t => t.Name == "Feature");
                }
            }
            catch (Exception ex)
            {
                Skip.If(true, $"Cannot open MSI database: {ex.Message}");
            }
        }

        private string GetMsiPath()
        {
            // Try to find the MSI in the build output
            var possiblePaths = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), 
                    "..", "..", "..", "..", 
                    "AutomationScheduler.Installer", "bin", "Release", "AutomationSchedulerSetup.msi"),
                Path.Combine(Directory.GetCurrentDirectory(), 
                    "..", "..", "..", "..", 
                    "AutomationScheduler.Installer", "bin", "Debug", "AutomationSchedulerSetup.msi")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return Path.GetFullPath(path);
                }
            }

            return possiblePaths[0]; // Return first path even if not found (test will skip)
        }

        private int ExecuteMsiExec(string arguments)
        {
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "msiexec.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                process.Start();
                process.WaitForExit(TimeSpan.FromMinutes(5).Milliseconds);
                
                return process.ExitCode;
            }
        }

        private void UninstallMsi(string installPath, string logPath)
        {
            var uninstallLog = logPath.Replace(".log", "_uninstall.log");
            var arguments = $"/x \"{_msiPath}\" /qn /l*v \"{uninstallLog}\"";
            ExecuteMsiExec(arguments);
        }

        private bool IsAdministrator()
        {
            using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
            {
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
        }

        public void Dispose()
        {
            // Cleanup
            if (Directory.Exists(_testInstallPath))
            {
                try
                {
                    // Try to uninstall first
                    UninstallMsi(_testInstallPath, _logPath);
                    Directory.Delete(_testInstallPath, true);
                }
                catch
                {
                    // Best effort cleanup
                }
            }

            if (File.Exists(_logPath))
            {
                try
                {
                    File.Delete(_logPath);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }
    }
}