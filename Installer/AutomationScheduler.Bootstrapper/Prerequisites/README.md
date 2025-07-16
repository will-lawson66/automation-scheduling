# Prerequisites Folder

This folder should contain the .NET runtime installers for offline installation scenarios.

## Required Files

1. **dotnet-runtime-8.0.19-win-x64.exe**
   - Download from: https://dotnet.microsoft.com/download/dotnet/8.0
   - Required for .NET Core Runtime 8.0.19

2. **windowsdesktop-runtime-8.0.10-win-x64.exe**
   - Download from: https://dotnet.microsoft.com/download/dotnet/8.0
   - Required for .NET Desktop Runtime 8.0.10

## Note

These files are not included in source control due to their size. The bootstrapper can download them automatically during installation if they're not present, but including them here enables offline installation.

To prepare for offline installation:
1. Download the runtime installers from the links above
2. Place them in this directory
3. Build the bootstrapper project

The bootstrapper will embed or reference these files based on the configuration.
