[CmdletBinding()]
param
(
    # Path to solution file. Defaults to $pwd.
    [ValidateNotNullOrEmpty()]
    [string] $Project = "$pwd"
)

if (!(Get-Command msbuild -ErrorAction SilentlyContinue)) {
    Import-Module $PSScriptRoot\FindVS.psm1
    $msbuildPath = Find-VisualStudio -Locate 'MSBuild'
}
else {
    $msbuildPath = (Get-Command msbuild).Path
}

$Project = (Resolve-Path -Path $Project -ErrorAction Stop).ProviderPath
if (!$Project.EndsWith('proj')) {
    $previousProject = $Project
    $Project = Get-ChildItem -Path $Project -Filter '*.*proj' | Select-Object -First 1 | ForEach-Object { $_.FullName }
    if (!$Project) {
        throw "No *.*proj was found in $previousProject"
    }
}

$msbuildArgs = @(
    '-restore',
    '-t:SlnGen',
    $Project
)

Write-Verbose "Executing: & $msbuildPath $($msbuildArgs -join ' ')"
& $msbuildPath $msbuildArgs
if ($LASTEXITCODE -ne 0) {
    throw "Failed to load VS IDE for: $Project"
}