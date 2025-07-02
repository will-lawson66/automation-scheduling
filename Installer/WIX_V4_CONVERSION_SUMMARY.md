# WiX v4 Conversion Summary

## What I've Updated for WiX v4:

### 1. Project Files (.wixproj)
- ✅ Converted to SDK-style projects
- ✅ Added PackageReferences for extensions
- ✅ Updated to use `WixToolset.Sdk/4.0.5`

### 2. Namespace Changes
- ✅ Updated from `http://schemas.microsoft.com/wix/2006/wi` 
- ✅ To `http://wixtoolset.org/schemas/v4/wxs`

### 3. Key Element Changes
- ✅ `<Product>` → `<Package>`
- ✅ `BinaryKey` → `BinaryRef`
- ✅ Updated extension namespaces

### 4. Extension References
- ✅ `WixUIExtension` → `WixToolset.UI.wixext`
- ✅ `WixUtilExtension` → `WixToolset.Util.wixext`
- ✅ `WixBalExtension` → `WixToolset.Bal.wixext`

## To Build with WiX v4:

```bash
# First, ensure WiX v4 is installed
dotnet tool install --global wix

# Then build
dotnet build AutomationSchedulerInstaller.sln

# Or build individual projects
dotnet build AutomationScheduler.Installer\AutomationScheduler.Installer.wixproj
dotnet build AutomationScheduler.Bootstrapper\AutomationScheduler.Bootstrapper.wixproj
```

## What Still Needs Your Attention:

1. **Source Path**: Update the `SourcePath` property in the .wixproj files to point to your actual build output

2. **Resources**: Add these files to the Resources folders:
   - `app.ico` - Application icon
   - `corporate-logo.jpg` - Your company logo
   - `banner.jpg` - Installer banner (493x58 pixels)
   - `dialog.jpg` - Installer dialog background (493x312 pixels)

3. **Azure DevOps Settings**: Configure these in your build or as environment variables:
   - `AZUREDEVOPS_URL`
   - `AZUREDEVOPS_PAT`
   - `AZUREDEVOPS_FEED`

4. **Company Information**: Update "Your Company Name" throughout the files

## Benefits of WiX v4:

- 🚀 Faster builds
- 📦 Better NuGet integration
- 🛠️ Simplified project files
- 🔧 Improved tooling
- 📚 Better documentation
- 🎯 .NET SDK-style projects

## Notes:

- The custom actions project remains unchanged (still .NET 8)
- Test project remains unchanged
- All functionality from v3 is preserved
