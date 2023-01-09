[CmdletBinding()]
param (
    [switch] $VSPreview
)

if ($env:CentralPackageManagementMigratorInitLoaded) {
    return
}

# Load Scripts directory
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

# Load Dotnet tool
$dotnetToolConfigPath = (Get-Item "$PSScriptRoot\.config\dotnet-tools.json").FullName
$dotnetToolConfig = Get-Content -Path $dotnetToolConfigPath | ConvertFrom-Json -Depth 100
$dotnetToolNames = $dotnetToolConfig.tools.psobject.Members | Where-Object MemberType -eq 'NoteProperty'

Write-Host "Restoring dotnot tools"
dotnet tool restore

foreach ($toolName in $dotnetToolNames) {
    $toolCommands = Invoke-Expression "`$dotnetToolConfig.tools.$($toolName.Name).commands"
    foreach ($commandName in $toolCommands) {
        Invoke-Expression -Command "function global::DOTNETTOOL_$commandName { param([Parameter(ValueFromRemainingArguments)] `$args) & dotnet tool run $commandName $args }" 
        Set-Alias -Name $commandName -Value "global::DOTNETTOOL_$commandName" -Scope Global -Option AllScope -ErrorAction SilentlyContinue
    }
}


# Load VS CMD
LoadVSDevConsole -VSPreview:($VSPreview.IsPresent)

Write-Host "Welcome to CentralPackageManagementMigrator repository!`n" -ForegroundColor Green
$env:CentralPackageManagementMigratorInitLoaded = $true