# Quick Start Checklist for WiX v4 Installer

## ✅ Completed (What I've Done)
- [x] Converted all project files to WiX v4 SDK-style
- [x] Updated all namespaces from v3 to v4
- [x] Updated element names (Product → Package, etc.)
- [x] Converted extension references to PackageReferences
- [x] Updated build scripts for WiX v4
- [x] Created comprehensive test suite
- [x] Added global.json for SDK version management

## 📋 TODO (What You Need to Do)

### 1. Install WiX v4
```bash
dotnet tool install --global wix
```

### 2. Update Source Path
Edit both .wixproj files and replace:
```
C:\Path\To\Your\Build\Output
```
With your actual application build output path.

### 3. Add Required Resources
Place these files in `AutomationScheduler.Installer\Resources\`:
- [ ] `app.ico` - Your application icon
- [ ] `corporate-logo.jpg` - Company logo (recommended: 493x58 pixels)
- [ ] `banner.jpg` - Installer banner (493x58 pixels)
- [ ] `dialog.jpg` - Installer dialog background (493x312 pixels)

### 4. Update Company Information
Search and replace throughout the solution:
- [ ] "Your Company Name" → Your actual company name
- [ ] "YourCompany" → Your company folder name
- [ ] Update GUIDs in Product.wxs and Bundle.wxs

### 5. Configure Azure DevOps (Optional)
If using Azure DevOps artifact downloads:
- [ ] Set `AZUREDEVOPS_URL` environment variable
- [ ] Set `AZUREDEVOPS_PAT` environment variable
- [ ] Set `AZUREDEVOPS_FEED` environment variable

### 6. Place Application Files
In your source directory, ensure you have:
- [ ] ConsoleApp1.exe (and related files)
- [ ] ConsoleApp2.exe (and related files)
- [ ] ConsoleApp3.exe (and related files)
- [ ] appsettings.json
- [ ] config1.json
- [ ] config2.json

### 7. Build and Test
```bash
# Build everything
dotnet build AutomationSchedulerInstaller.sln

# Run tests
dotnet test

# Or use the build script
build.cmd
```

## 🚀 First Build Command
Once you've completed the checklist:
```bash
cd C:\Users\willl_pmx92pt\source\repos\automation-scheduling\Installer
dotnet build -c Release /p:SourcePath="C:\Your\Actual\Path"
```

## 📦 Output Locations
- MSI: `AutomationScheduler.Installer\bin\x64\Release\en-US\AutomationSchedulerSetup.msi`
- Bootstrapper: `AutomationScheduler.Bootstrapper\bin\x64\Release\AutomationSchedulerBootstrapper.exe`

## ❓ Need Help?
- WiX v4 Docs: https://wixtoolset.org/docs/
- Check `WIX_V4_GUIDE.md` for detailed commands
- Review test project for examples
