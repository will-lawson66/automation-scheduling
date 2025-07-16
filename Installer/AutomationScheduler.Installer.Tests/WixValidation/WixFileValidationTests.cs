using System.Xml.Linq;
using Xunit;
using FluentAssertions;
using System.IO;
using System.Linq;

namespace AutomationScheduler.Installer.Tests.WixValidation
{
    public class WixFileValidationTests
    {
        private readonly string _installerProjectPath;
        private readonly XNamespace _wixNamespace = "http://schemas.microsoft.com/wix/2006/wi";

        public WixFileValidationTests()
        {
            // Adjust path as needed based on test execution context
            _installerProjectPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "..", "..", "..", "..",
                "AutomationScheduler.Installer");
        }

        [Theory]
        [InlineData("Product.wxs")]
        [InlineData("Components\\ConsoleApplications.wxs")]
        [InlineData("Components\\Plugins.wxs")]
        [InlineData("Components\\Configuration.wxs")]
        [InlineData("Components\\Shortcuts.wxs")]
        [InlineData("UI\\CustomDialogs.wxs")]
        [InlineData("UI\\CustomUI.wxs")]
        public void WixFile_ShouldBeValidXml(string relativePath)
        {
            // Arrange
            var filePath = Path.Combine(_installerProjectPath, relativePath);
            
            // Skip if file doesn't exist (running in different context)
            if (!File.Exists(filePath))
            {
                return;
            }

            // Act & Assert
            var action = () => XDocument.Load(filePath);
            action.Should().NotThrow<Exception>();
        }

        [Fact]
        public void AllComponents_ShouldHaveUniqueIds()
        {
            // Arrange
            var wxsFiles = GetAllWxsFiles();
            var allIds = new List<string>();

            // Act
            foreach (var file in wxsFiles)
            {
                if (!File.Exists(file)) continue;
                
                var doc = XDocument.Load(file);
                var ids = doc.Descendants()
                    .Where(e => e.Attribute("Id") != null)
                    .Select(e => e.Attribute("Id").Value)
                    .Where(id => !id.StartsWith("*")); // Exclude auto-generated IDs
                
                allIds.AddRange(ids);
            }

            // Assert
            var duplicates = allIds.GroupBy(id => id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            duplicates.Should().BeEmpty($"Found duplicate IDs: {string.Join(", ", duplicates)}");
        }

        [Fact]
        public void AllComponents_ShouldHaveValidGuids()
        {
            // Arrange
            var wxsFiles = GetAllWxsFiles();
            var invalidGuids = new List<string>();

            // Act
            foreach (var file in wxsFiles)
            {
                if (!File.Exists(file)) continue;
                
                var doc = XDocument.Load(file);
                var guids = doc.Descendants()
                    .Where(e => e.Attribute("Guid") != null)
                    .Select(e => e.Attribute("Guid").Value)
                    .Where(guid => !guid.Equals("*")); // Exclude auto-generated GUIDs

                foreach (var guid in guids)
                {
                    if (!Guid.TryParse(guid, out _))
                    {
                        invalidGuids.Add($"{Path.GetFileName(file)}: {guid}");
                    }
                }
            }

            // Assert
            invalidGuids.Should().BeEmpty($"Found invalid GUIDs: {string.Join(", ", invalidGuids)}");
        }

        [Fact]
        public void Product_ShouldHaveRequiredAttributes()
        {
            // Arrange
            var productFile = Path.Combine(_installerProjectPath, "Product.wxs");
            
            if (!File.Exists(productFile)) return;
            
            var doc = XDocument.Load(productFile);
            var product = doc.Descendants(_wixNamespace + "Product").FirstOrDefault();

            // Assert
            product.Should().NotBeNull();
            product.Attribute("Name").Should().NotBeNull();
            product.Attribute("Language").Should().NotBeNull();
            product.Attribute("Version").Should().NotBeNull();
            product.Attribute("Manufacturer").Should().NotBeNull();
            product.Attribute("UpgradeCode").Should().NotBeNull();
        }

        [Fact]
        public void AllFileComponents_ShouldHaveKeyPath()
        {
            // Arrange
            var wxsFiles = GetAllWxsFiles();
            var componentsWithoutKeyPath = new List<string>();

            // Act
            foreach (var file in wxsFiles)
            {
                if (!File.Exists(file)) continue;
                
                var doc = XDocument.Load(file);
                var components = doc.Descendants(_wixNamespace + "Component")
                    .Where(c => c.Descendants(_wixNamespace + "File").Any());

                foreach (var component in components)
                {
                    var hasKeyPath = component.Descendants(_wixNamespace + "File")
                        .Any(f => f.Attribute("KeyPath")?.Value == "yes");
                    
                    if (!hasKeyPath)
                    {
                        var componentId = component.Attribute("Id")?.Value ?? "Unknown";
                        componentsWithoutKeyPath.Add($"{Path.GetFileName(file)}: {componentId}");
                    }
                }
            }

            // Assert
            componentsWithoutKeyPath.Should().BeEmpty(
                $"Components without KeyPath: {string.Join(", ", componentsWithoutKeyPath)}");
        }

        [Fact]
        public void CustomActions_ShouldBeProperlyDefined()
        {
            // Arrange
            var productFile = Path.Combine(_installerProjectPath, "Product.wxs");
            
            if (!File.Exists(productFile)) return;
            
            var doc = XDocument.Load(productFile);
            var customActions = doc.Descendants(_wixNamespace + "CustomAction").ToList();

            // Assert
            customActions.Should().NotBeEmpty();
            
            foreach (var ca in customActions)
            {
                ca.Attribute("Id").Should().NotBeNull();
                
                // Should have either BinaryKey/DllEntry or other valid combination
                var hasBinaryKey = ca.Attribute("BinaryKey") != null;
                var hasDllEntry = ca.Attribute("DllEntry") != null;
                
                if (hasBinaryKey)
                {
                    hasDllEntry.Should().BeTrue($"CustomAction {ca.Attribute("Id")?.Value} with BinaryKey should have DllEntry");
                }
            }
        }

        [Fact]
        public void AllReferencedDirectories_ShouldBeDefined()
        {
            // Arrange
            var wxsFiles = GetAllWxsFiles();
            var definedDirectories = new HashSet<string>();
            var referencedDirectories = new HashSet<string>();

            // Act
            foreach (var file in wxsFiles)
            {
                if (!File.Exists(file)) continue;
                
                var doc = XDocument.Load(file);
                
                // Collect defined directories
                var directories = doc.Descendants(_wixNamespace + "Directory")
                    .Select(d => d.Attribute("Id")?.Value)
                    .Where(id => !string.IsNullOrEmpty(id));
                
                foreach (var dir in directories)
                {
                    definedDirectories.Add(dir);
                }
                
                // Collect referenced directories
                var componentGroups = doc.Descendants(_wixNamespace + "ComponentGroup")
                    .Select(cg => cg.Attribute("Directory")?.Value)
                    .Where(dir => !string.IsNullOrEmpty(dir));
                
                var components = doc.Descendants(_wixNamespace + "Component")
                    .Select(c => c.Attribute("Directory")?.Value)
                    .Where(dir => !string.IsNullOrEmpty(dir));
                
                foreach (var dir in componentGroups.Concat(components))
                {
                    referencedDirectories.Add(dir);
                }
            }

            // Assert
            var undefinedDirectories = referencedDirectories.Except(definedDirectories).ToList();
            undefinedDirectories.Should().BeEmpty(
                $"Referenced but undefined directories: {string.Join(", ", undefinedDirectories)}");
        }

        private List<string> GetAllWxsFiles()
        {
            var files = new List<string>();
            
            if (Directory.Exists(_installerProjectPath))
            {
                files.AddRange(Directory.GetFiles(_installerProjectPath, "*.wxs", SearchOption.AllDirectories));
            }
            
            return files;
        }
    }
}