[CmdletBinding()]
param (
    [Parameter()]
    [string]
    $Version,

    [Parameter()]
    [switch] 
    $VSPreview
)

# https://github.com/microsoft/vswhere/wiki/Start-Developer-Command-Prompt

if ($env:VSCMD_VER) {
    Write-Verbose "Already loaded VS CMD $($env:VSCMD_VER)"
    return
}

Import-Module $PSScriptRoot\FindVS.psm1
$vsInstallPath = Find-VisualStudio -Locate 'InstallPath' -Version:$Version -Prerelease:$VSPreview
if ($vsInstallPath -and (Test-Path -Path "$vsInstallPath\Common7\Tools\vsdevcmd.bat")) {
    & "${env:COMSPEC}" /s /c "`"$vsInstallPath\Common7\Tools\vsdevcmd.bat`" -no_logo && set" | ForEach-Object {
      $name, $value = $_ -split '=', 2
      Set-Content env:\"$name" $value
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to load VS CMD, exit code $LASTEXITCODE"
    }

    Write-Host "VS $($env:VSCMD_VER) CMD loaded" -ForegroundColor Gray
}
else {
    throw "Failed to load VS CMD as it wasn't found"
}