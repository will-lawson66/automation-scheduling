# Automation Scheduler Installer

This is a WiX-based installer solution for the Automation Scheduler application, built with Visual Studio and WiX Toolset.

## Project Structure

- **AutomationScheduler.Installer** - Main MSI installer project
- **AutomationScheduler.Bootstrapper** - Burn bootstrapper for handling prerequisites (.NET runtimes)
- **AutomationScheduler.CustomActions** - C# custom actions for Azure DevOps integration and configuration

## Prerequisites

1. **Visual Studio 2022** with the following workloads:
   - .NET desktop development
   - .NET 8.0 SDK

2. **WiX Toolset v3.11 or later**
   - Download from: https://wixtoolset.org/releases/
   - Install the WiX Toolset Visual Studio Extension

3. **Azure DevOps Access** (for artifact downloads)
   - Personal Access Token (PAT) with package read permissions
   - Access to the NuGet feed containing your artifacts

## Building the Installer

1. Open `AutomationSchedulerInstaller.sln` in Visual Studio

2. Configure build properties:
   - Right-click the solution and select "Properties"
   - Set the build order: CustomActions → Installer → Bootstrapper

3. Build the solution:
   - Select Release configuration
   - Build → Build Solution (Ctrl+Shift+B)

4. The output will be in:
   - `AutomationScheduler.Bootstrapper\bin\Release\AutomationSchedulerBootstrapper.exe`

## Configuration

### Azure DevOps Integration

Set these environment variables or modify the installer properties:
- `AZUREDEVOPS_URL`: Your Azure DevOps organization URL
- `AZUREDEVOPS_PAT`: Personal Access Token
- `AZUREDEVOPS_FEED`: NuGet feed name

### Adding Resources

1. **Corporate Logo**: Replace `Resources\corporate-logo.jpg`
2. **Banner Images**: Replace `Resources\banner.jpg` and `Resources\dialog.jpg`
3. **License Agreement**: Update `Resources\license.rtf`
4. **Application Icon**: Add `Resources\app.ico`

### Source Files

Place your application files in a staging directory and update the MSBuild variable:
```xml
<DefineConstants>SourcePath=C:\Path\To\Your\Build\Output</DefineConstants>
```

## Features

- ✅ .NET 8 Runtime detection and installation
- ✅ Three console applications with desktop shortcuts
- ✅ Plugin directory structure
- ✅ Configuration dropdown for different deployment options
- ✅ Azure DevOps artifact integration
- ✅ Dynamic configuration file updates
- ✅ Corporate branding support

## Customization

### Adding New Configuration Options

1. Edit `UI\CustomDialogs.wxs` to add new dropdown items
2. Update `CustomActions.cs` to handle the new configuration
3. Modify `Product.wxs` to add corresponding features

### Modifying Prerequisites

Edit `Prerequisites.wxs` to:
- Change .NET versions
- Add additional prerequisites
- Modify download URLs

## Testing

1. Build in Debug configuration
2. Enable installer logging:
   ```
   msiexec /i AutomationSchedulerSetup.msi /l*v install.log
   ```

3. Test different configuration options
4. Verify file placement and registry entries

## Deployment

The bootstrapper exe is self-contained and can be distributed to end users. It will:
1. Check for .NET prerequisites
2. Download and install missing components
3. Run the main installer with your corporate branding
4. Download artifacts from Azure DevOps based on user selection
5. Configure the application appropriately

## Troubleshooting

- **WiX not found**: Ensure WiX Toolset is installed and VS extension is active
- **Build errors**: Check that all project references are resolved
- **Custom action failures**: Review the MSI log for detailed error messages
- **Azure DevOps connection**: Verify PAT has correct permissions

## Next Steps

1. Add actual application files to the build
2. Configure Azure DevOps connection settings
3. Customize branding images
4. Test with real artifacts
5. Set up CI/CD pipeline for automated builds
