[CmdletBinding()]
param (
    [switch] $VSPreview
)

if ($env:CentralPackageManagementMigratorInitLoaded) {
    return
}

$scriptsDir = (Get-Item "$PSScriptRoot\..\scripts").FullName

if($env:Path -split ';' -inotcontains $scriptsDir) {
    Write-Verbose "Adding '$scriptsDir' to Path environment variable..."
    $env:Path = $env:Path.TrimEnd(';')
    $env:Path += ";$scriptsDir"
}

$scripts = Get-ChildItem -Path $scriptsDir -Filter *.ps1
foreach ($script in $scripts) {
    $scriptName = $script.BaseName
    Set-Alias -Name $scriptName -Value $script.FullName -Scope Global -Option AllScope -ErrorAction SilentlyContinue
}

Write-Host "Any script (*.ps1) in $scriptsDir can be called with just name of script" -ForegroundColor Gray

# Load VS CMD
LoadVSDevConsole -VSPreview:($VSPreview.IsPresent)

Write-Host "Welcome to CentralPackageManagementMigrator repository!`n" -ForegroundColor Green
$env:CentralPackageManagementMigratorInitLoaded = $true