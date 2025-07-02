# Creating a WiX Bootstrapper Project Manually in VS 2022

Since the Bootstrapper template is missing, here's how to create one manually:

## Step 1: Create a Basic WiX Project
1. Right-click Solution → Add → New Project
2. Choose "WiX Toolset MSI Package" (or just "WiX Project")
3. Name it "YourApp.Bootstrapper"

## Step 2: Modify the .wixproj File
Replace the contents with:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="4.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">x86</Platform>
    <ProductVersion>3.10</ProductVersion>
    <ProjectGuid>{YOUR-GUID-HERE}</ProjectGuid>
    <SchemaVersion>2.0</SchemaVersion>
    <OutputName>YourAppBootstrapper</OutputName>
    <OutputType>Bundle</OutputType>  <!-- This makes it a bootstrapper -->
  </PropertyGroup>
  
  <!-- Rest of configuration -->
  <ItemGroup>
    <Compile Include="Bundle.wxs" />
  </ItemGroup>
  
  <ItemGroup>
    <WixExtension Include="WixBalExtension">
      <HintPath>$(WixExtDir)\WixBalExtension.dll</HintPath>
      <Name>WixBalExtension</Name>
    </WixExtension>
  </ItemGroup>
  
  <Import Project="$(WixTargetsPath)" />
</Project>
```

## Step 3: Delete Product.wxs and Create Bundle.wxs
Delete the default Product.wxs and create Bundle.wxs:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://schemas.microsoft.com/wix/2006/wi"
     xmlns:bal="http://schemas.microsoft.com/wix/BalExtension">
  
  <Bundle Name="Your Application Setup"
          Version="1.0.0.0"
          Manufacturer="Your Company"
          UpgradeCode="{NEW-GUID-HERE}">

    <BootstrapperApplicationRef Id="WixStandardBootstrapperApplication.RtfLicense">
      <bal:WixStandardBootstrapperApplication 
        LicenseFile="License.rtf"
        ShowVersion="yes" />
    </BootstrapperApplicationRef>

    <Chain>
      <!-- Add prerequisites here -->
      <MsiPackage SourceFile="path\to\your.msi" />
    </Chain>
  </Bundle>
</Wix>
```

## Key Differences from MSI Project:
1. **OutputType**: Must be "Bundle" not "Package"
2. **Root element**: Use `<Bundle>` not `<Product>`
3. **Extension**: Requires WixBalExtension
4. **File name**: Use Bundle.wxs not Product.wxs
