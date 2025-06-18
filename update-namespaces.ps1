
# Define the root directory of the solution
$solutionDir = "C:\Users\willl_pmx92pt\source\repos\automation\scheduler-Data"

# Define namespace patterns to replace
$oldNamespacePattern = "Instrument\.Scheduling\.Data"
$newNamespacePattern = "Instrument\.Data"

# Process all .cs files
$csFiles = Get-ChildItem -Path $solutionDir -Filter "*.cs" -Recurse -File

Write-Host "Found $($csFiles.Count) .cs files to process"

$fileCount = 0
$replacementCount = 0

foreach ($file in $csFiles) {
    $content = Get-Content -Path $file.FullName -Raw
    $originalContent = $content
    
    # Replace namespace declarations
    $content = $content -replace "namespace\s+$oldNamespacePattern([\.\w]*);", "namespace $newNamespacePattern`$1;"
    
    # Replace using statements
    $content = $content -replace "using\s+$oldNamespacePattern([\.\w]*);", "using $newNamespacePattern`$1;"
    
    # Replace type references
    $content = $content -replace "$oldNamespacePattern([\.\w]*)\.", "$newNamespacePattern`$1."
    
    # Save file if changes were made
    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        $fileCount++
        $replacementCount += ($originalContent -split $oldNamespacePattern).Count - 1
        Write-Host "Updated: $($file.FullName)"
    }
}

# Process all .xaml files
$xamlFiles = Get-ChildItem -Path $solutionDir -Filter "*.xaml" -Recurse -File

Write-Host "Found $($xamlFiles.Count) .xaml files to process"

foreach ($file in $xamlFiles) {
    $content = Get-Content -Path $file.FullName -Raw
    $originalContent = $content
    
    # Replace x:Class attributes
    $content = $content -replace "x:Class=""$oldNamespacePattern([\.\w]*)""", "x:Class=""$newNamespacePattern`$1"""
    
    # Replace clr-namespace references
    $content = $content -replace "clr-namespace:$oldNamespacePattern([\.\w]*)", "clr-namespace:$newNamespacePattern`$1"
    
    # Save file if changes were made
    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        $fileCount++
        $replacementCount += ($originalContent -split $oldNamespacePattern).Count - 1
        Write-Host "Updated: $($file.FullName)"
    }
}

# Process all .csproj files for assembly names and root namespaces
$projFiles = Get-ChildItem -Path $solutionDir -Filter "*.csproj" -Recurse -File

Write-Host "Found $($projFiles.Count) .csproj files to process"

foreach ($file in $projFiles) {
    $content = Get-Content -Path $file.FullName -Raw
    $originalContent = $content
    
    # Replace RootNamespace and AssemblyName
    $content = $content -replace "<RootNamespace>$oldNamespacePattern([\.\w]*)</RootNamespace>", "<RootNamespace>$newNamespacePattern`$1</RootNamespace>"
    $content = $content -replace "<AssemblyName>$oldNamespacePattern([\.\w]*)</AssemblyName>", "<AssemblyName>$newNamespacePattern`$1</AssemblyName>"
    
    # Save file if changes were made
    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        $fileCount++
        $replacementCount += ($originalContent -split $oldNamespacePattern).Count - 1
        Write-Host "Updated: $($file.FullName)"
    }
}

Write-Host "Namespace refactoring complete. Updated $fileCount files with $replacementCount replacements."
