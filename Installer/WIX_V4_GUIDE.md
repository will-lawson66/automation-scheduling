# WiX v4 Setup and Usage Guide

## Installing WiX v4

### 1. Install WiX as a .NET Tool
```bash
# Install globally
dotnet tool install --global wix

# Verify installation
dotnet tool list -g
dotnet wix --version
```

### 2. Update PATH (if needed)
If `wix` command doesn't work directly, add to PATH:
- Windows: Add `%USERPROFILE%\.dotnet\tools` to system PATH
- Restart terminal/VS after adding to PATH

## Building the Installer

### Using Command Line
```bash
# From the Installer directory
cd C:\Users\willl_pmx92pt\source\repos\automation-scheduling\Installer

# Build MSI
dotnet build AutomationScheduler.Installer\AutomationScheduler.Installer.wixproj

# Build Bootstrapper
dotnet build AutomationScheduler.Bootstrapper\AutomationScheduler.Bootstrapper.wixproj

# Build entire solution
dotnet build AutomationSchedulerInstaller.sln
```

### Using Visual Studio 2022
1. Install HeatWave extension for IntelliSense support
2. Open `AutomationSchedulerInstaller.sln`
3. Build → Build Solution

## Key Changes from WiX v3 to v4

### 1. Project Files
- Now use SDK-style projects: `<Project Sdk="WixToolset.Sdk/4.0.5">`
- Extensions are PackageReferences instead of WixExtension items

### 2. Namespaces
- v3: `http://schemas.microsoft.com/wix/2006/wi`
- v4: `http://wixtoolset.org/schemas/v4/wxs`

### 3. Elements
- `<Product>` → `<Package>`
- `BinaryKey` → `BinaryRef`
- Some attributes renamed

### 4. Extensions
Installed via PackageReference:
```xml
<PackageReference Include="WixToolset.UI.wixext" Version="4.0.5" />
<PackageReference Include="WixToolset.Util.wixext" Version="4.0.5" />
<PackageReference Include="WixToolset.Bal.wixext" Version="4.0.5" />
```

## Common WiX v4 Commands

```bash
# Create new project
dotnet new wixmsi -n MyInstaller
dotnet new wixbundle -n MyBootstrapper

# Build
dotnet build
wix build

# Build with verbose output
dotnet build -v detailed

# Clean
dotnet clean

# Restore packages
dotnet restore
```

## Troubleshooting

### "wix not found"
```bash
# Use dotnet prefix
dotnet wix build

# Or reinstall
dotnet tool uninstall --global wix
dotnet tool install --global wix
```

### Build Errors
1. Ensure all PackageReferences are v4.0.5+
2. Update namespaces in .wxs files
3. Check for v3-specific attributes

### VS IntelliSense Not Working
- Install latest HeatWave extension
- Restart VS after installing
- Ensure project uses WiX SDK format

## Next Steps

1. **Update Source Path**: Edit .wixproj files to set correct SourcePath
2. **Add Resources**: Place logo, banner images in Resources folder
3. **Configure Azure DevOps**: Set PAT and feed information
4. **Test Build**: Run `dotnet build` to verify setup

## Useful Resources
- [WiX v4 Documentation](https://wixtoolset.org/docs/fourthree/)
- [Migration Guide](https://wixtoolset.org/docs/fourthree/migration/)
- [WiX v4 Examples](https://github.com/wixtoolset/wix4)
