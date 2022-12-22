function Find-VisualStudio
{
    [CmdletBinding()]
    [OutputType([string])]
    param
    (
        [Parameter()]
        [string]
        [ValidateSet('MSBuild', 'InstallPath')]
        $Locate = 'MSBuild',

        [Parameter()]
        [string]
        $Version,

        [Parameter()]
        [switch] 
        $Prerelease
    )

    # https://github.com/Microsoft/vswhere
    $vsWhereExe = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if(!(Test-Path -Path $vsWhereExe)) {
        throw "Unable to find vswhere, confirm Visual Studio is installed: $vsWhereExe"
    }

    $vsWhereArgs = @()
    if ($Version) {
        $vsWhereArgs+= @('-version', $Version)
    }
    else {
        $vsWhereArgs += '-latest'
    }

    if($Prerelease) {
        $vsWhereArgs += '-prerelease'
    }

    if ($Locate -eq 'MSBuild') {
        $vsWhereArgs += @('-requires', 'Microsoft.Component.MSBuild', '-find', 'MSBuild\**\Bin\MSBuild.exe')
    }
    elseif ($Locate -eq 'InstallPath') {
        $vsWhereArgs += @('-property', 'installationPath')
    }
    else {
        throw "Nonsupported -Locate $Locate"
    }

    # https://github.com/microsoft/vswhere/wiki/Find-MSBuild#powershell
    Write-Verbose "Executing: & $vsWhereExe $($vsWhereArgs -join ' ')"
    $result = & $vsWhereExe $vsWhereArgs | select-object -first 1
    if($LASTEXITCODE -ne 0 -or !$result) {
        throw "vswhere.exe didn't return a result with args (exit code: $LASTEXITCODE): $($vsWhereArgs -join ' ')"
    }

    return $result
}