# WiX v4 Bootstrapper Build Fix

## The Problem
You're seeing an error about `<Import Project="$(WixTargetsPath)" />` which is from WiX v3, not v4.

## Quick Test
Try building this minimal bootstrapper first:

```bash
cd C:\Users\willl_pmx92pt\source\repos\automation-scheduling\Installer\MinimalBootstrapper
dotnet build
```

If this works, the issue is with the main project. If it doesn't work, WiX v4 isn't installed correctly.

## Common Issues and Fixes

### 1. WiX v4 Not Installed
```bash
# Check if WiX is installed
dotnet tool list -g

# If not listed, install it
dotnet tool install --global wix --version 4.0.5
```

### 2. Old Project File Format
Make sure your .wixproj looks like this (NO Import statements):
```xml
<Project Sdk="WixToolset.Sdk/4.0.5">
  <PropertyGroup>
    <OutputType>Bundle</OutputType>
  </PropertyGroup>
</Project>
```

### 3. Visual Studio Cache Issues
1. Close Visual Studio
2. Delete `.vs` folder in solution directory
3. Delete `bin` and `obj` folders
4. Reopen and rebuild

### 4. Wrong Namespace in Bundle.wxs
Make sure Bundle.wxs uses the correct namespace:
```xml
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs/bundle">
```
NOT the old v3 namespace!

### 5. MSBuild Looking for Old Format
Try building from command line instead of VS:
```bash
dotnet build AutomationScheduler.Bootstrapper.wixproj
```

## If Still Having Issues

1. **Check for old .wixproj.user files**:
   ```bash
   del *.wixproj.user /s
   ```

2. **Create fresh bootstrapper project**:
   ```bash
   dotnet new classlib -n TestBootstrapper
   cd TestBootstrapper
   # Delete the .cs file and .csproj
   # Create new .wixproj with content above
   ```

3. **Check global.json**:
   Make sure it specifies WiX SDK:
   ```json
   {
     "msbuild-sdks": {
       "WixToolset.Sdk": "4.0.5"
     }
   }
   ```

## Nuclear Option - Start Fresh
```bash
# Create brand new WiX v4 bootstrapper
mkdir FreshBootstrapper
cd FreshBootstrapper

# Create .wixproj
echo ^<Project Sdk="WixToolset.Sdk/4.0.5"^>^<PropertyGroup^>^<OutputType^>Bundle^</OutputType^>^</PropertyGroup^>^</Project^> > FreshBootstrapper.wixproj

# Create Bundle.wxs (copy from MinimalBootstrapper)
# Then build
dotnet build
```

## The Real Issue?
If you're seeing Import errors, you might be:
1. Opening an old WiX v3 project by mistake
2. VS is using cached project information
3. There's a .props or .targets file somewhere importing old stuff

Let me know which specific file is showing the error and I'll help fix it!
