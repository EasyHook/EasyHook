<#
Helper functions and setup scripts all stored here and used
by other scripts/tools here. 
#>

param(
    [Parameter(Position = 0)] 
    [string] $Target = "vs2017",
    [switch] $Initialize = $false
)

$script:ToolsDir = split-path -parent $MyInvocation.MyCommand.Definition

$script:EasyHookRootDir = (Get-Item $ToolsDir).Parent
$script:EasyHookRoot = $EasyHookRootDir.FullName
$script:EasyHookSln = Join-Path $EasyHookRoot 'EasyHook.sln'
$script:EasyHookPackages = Join-Path $EasyHookRoot 'Packages'
$script:EasyHookBin = Join-Path $EasyHookRoot 'Bin'

$script:Nuget = Join-Path $EasyHookBin nuget.exe
$script:VSWhere = [IO.Path]::Combine($EasyHookPackages, 'vswhere.2.8.4', 'tools', 'vswhere.exe')

$script:VSInstallationPath = $null

function Write-Diagnostic {
    param(
        [Parameter(Position = 0, Mandatory = $true, ValueFromPipeline = $true)]
        [string] $Message
    )

    Write-Host $Message -ForegroundColor Green
}

function DownloadNuget() {
    New-Item -ItemType Directory -Force -Path $EasyHookBin | Out-Null
    Set-Alias nuget $script:Nuget -Scope Global
    if (-not (Test-Path $script:Nuget)) {
        $Client = New-Object System.Net.WebClient;
        $Client.DownloadFile('http://nuget.org/nuget.exe', $script:Nuget);
    }
}

function RestoreNugetPackages() {
    . $script:Nuget restore -OutputDirectory $EasyHookPackages $EasyHookSln
    . $script:Nuget install -OutputDirectory $EasyHookPackages MSBuildTasks -Version 1.5.0.196 | Out-Null
    . $script:Nuget install -OutputDirectory $EasyHookPackages Microsoft.Build -Version 16.4.0 | Out-Null
    . $script:Nuget install -OutputDirectory $EasyHookPackages vswhere -Version 2.8.4 | Out-Null
    . $script:Nuget install -OutputDirectory $EasyHookPackages Microsoft.TestPlatform -Version 16.5.0 | Out-Null
    . $script:Nuget install -OutputDirectory $EasyHookPackages Appveyor.TestLogger -Version 2.0.0 | Out-Null
}

# https://github.com/jbake/Powershell_scripts/blob/master/Invoke-BatchFile.ps1
function Invoke-BatchFile {
    param(
        [Parameter(Position = 0, Mandatory = $true, ValueFromPipeline = $true)]
        [string]$Path, 
        [Parameter(Position = 1, Mandatory = $true, ValueFromPipeline = $true)]
        [string]$Parameters
    )

    $tempFile = [IO.Path]::GetTempFileName()  

    cmd.exe /c " `"$Path`" $Parameters && set > `"$tempFile`" " 

    Get-Content $tempFile | Foreach-Object {   
        if ($_ -match "^(.*?)=(.*)$") { 
            Set-Content "env:\$($matches[1])" $matches[2]  
        } 
    }  

    Remove-Item $tempFile
}

function Die {
    param(
        [Parameter(Position = 0, ValueFromPipeline = $true)]
        [string] $Message
    )

    Write-Host
    Write-Error $Message 
    exit 1
}

function Warn {
    param(
        [Parameter(Position = 0, ValueFromPipeline = $true)]
        [string] $Message
    )

    Write-Host
    Write-Host $Message -ForegroundColor Yellow
    Write-Host
}

function TernaryReturn {
    param(
        [Parameter(Position = 0, ValueFromPipeline = $true)]
        [bool] $Yes,
        [Parameter(Position = 1, ValueFromPipeline = $true)]
        $Value,
        [Parameter(Position = 2, ValueFromPipeline = $true)]
        $Value2
    )

    if ($Yes) {
        return $Value
    }
    
    $Value2
}

function Msvs {
    param(
        [Parameter(Position = 0, ValueFromPipeline = $true)]
        [string] $Sln = $null,
        [ValidateSet('v120', 'v140', 'v141', 'v142')]
        [Parameter(Position = 1, ValueFromPipeline = $true)]
        [string] $Toolchain, 

        [Parameter(Position = 2, ValueFromPipeline = $true)]
        [ValidateSet('netfx3.5-Debug', 'netfx3.5-Release', 'netfx4-Debug', 'netfx4-Release')]
        [string] $Configuration, 

        [Parameter(Position = 3, ValueFromPipeline = $true)]
        [ValidateSet('Win32', 'x64')]
        [string] $Platform
    )

    Write-Diagnostic "Targeting $Toolchain using configuration $Configuration on platform $Platform"

    $VisualStudioToolVersion = $null
    $MSBuildToolVersion = $null
    $VXXCommonTools = $null

    switch -Exact ($Toolchain) {
        'v120' {
            $VisualStudioToolVersion = '12.0'
            $MSBuildToolVersion = '12.0'
            $VXXCommonTools = Join-Path $VSInstallationPath '.\vc'
        }
        'v140' {
            $VisualStudioToolVersion = '14.0'
            $MSBuildToolVersion = '14.0'
            $VXXCommonTools = Join-Path $VSInstallationPath '.\vc'
        }
        'v141' {
            $VisualStudioToolVersion = '15.0'
            $MSBuildToolVersion = '15.0'
            $VXXCommonTools = Join-Path $VSInstallationPath '.\vc\auxiliary\build'
        }
        'v142' {
            $VisualStudioToolVersion = '16.0'
            $MSBuildToolVersion = 'Current'
            $VXXCommonTools = Join-Path $VSInstallationPath '.\vc\auxiliary\build'
        }
    }

    if ($null -eq $VXXCommonTools -or (-not (Test-Path($VXXCommonTools)))) {
        Die 'Error unable to find any visual studio environment'
    }

    $VCVarsAll = Join-Path $VXXCommonTools vcvarsall.bat
    if (-not (Test-Path $VCVarsAll)) {
        Die "Unable to find $VCVarsAll"
    }

    # Only configure build environment once
    if ($null -eq $env:EASYHOOK_BUILD_IS_BOOTSTRAPPED) {
        Invoke-BatchFile $VCVarsAll $Platform
        $env:EASYHOOK_BUILD_IS_BOOTSTRAPPED = $true
    }

    $Arguments = @(
        "$Sln",
        "/t:rebuild",
        "/tv:$MSBuildToolVersion",
        "/p:VisualStudioVersion=$VisualStudioToolVersion",
        "/p:Configuration=$Configuration",
        "/p:Platform=$Platform",
        "/p:PlatformToolset=$Toolchain",
        "/p:PackageDir=.\Deploy;FX35BuildDir=.\Build\netfx3.5-Release\x64;FX4BuildDir=.\Build\netfx4-Release\x64",
        "/verbosity:quiet"
    )

    Write-Diagnostic "MSBuild Executable: $MSBuildExe $Arguments"

    $StartInfo = New-Object System.Diagnostics.ProcessStartInfo
    $StartInfo.FileName = $MSBuildExe
    $StartInfo.Arguments = $Arguments

    $StartInfo.EnvironmentVariables.Clear()

    Get-ChildItem -Path env:* | ForEach-Object {
        $StartInfo.EnvironmentVariables.Add($_.Name, $_.Value)
    }

    $StartInfo.UseShellExecute = $false
    $StartInfo.CreateNoWindow = $false
    $StartInfo.RedirectStandardError = $true
    $StartInfo.RedirectStandardOutput = $true

    $Process = New-Object System.Diagnostics.Process
    $Process.StartInfo = $startInfo
    $ProcessCreated = $Process.Start()
    
    if (!$ProcessCreated) {
        Die "Failed to create process"
    }

    $stdout = $Process.StandardOutput.ReadToEnd()
    $stderr = $Process.StandardError.ReadToEnd()
    
    $Process.WaitForExit()

    if ($Process.ExitCode -ne 0) {
        Write-Host "stdout: $stdout"
        Write-Host "stderr: $stderr"
        Die "Build failed"
    }
}

function Get-ProcessOutput {
    Param (
        [Parameter(Mandatory = $true)]$FileName,
        $Args
    )
    
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo.UseShellExecute = $false
    $process.StartInfo.RedirectStandardOutput = $true
    $process.StartInfo.RedirectStandardError = $true
    $process.StartInfo.FileName = $FileName
    if ($Args) { $process.StartInfo.Arguments = $Args }
    $_ = $process.Start()
    
    $StandardError = $process.StandardError.ReadToEnd()
    $StandardOutput = $process.StandardOutput.ReadToEnd()
    
    $output = New-Object PSObject
    $output | Add-Member -type NoteProperty -name StandardOutput -Value $StandardOutput
    $output | Add-Member -type NoteProperty -name StandardError -Value $StandardError
    return $output
}

function FindVisualStudio {
    param(
        [Parameter(Position = 0, ValueFromPipeline = $true)]
        [string] $Toolchain
    )

    Write-Diagnostic $script:VSWhere

    $args = ""

    # VS2013
    if ($Toolchain -eq 'v120') {
        $args += '-legacy -version "[12.0,14.0)"'
    }
    
    # VS2015
    if ($Toolchain -eq 'v140') {
        $args += '-legacy -version "[14.0,15.0)"'
    }
    
    # VS2017
    if ($Toolchain -eq 'v141') {
        $args += '-version "[15.0,16.0)"'
    }
    
    # VS2019
    if ($Toolchain -eq 'v142') {
        $args += '-version "[16.0,17.0)"'
    }
    
    $output = Get-ProcessOutput -FileName $script:VSWhere -Args ($args + ' -property installationPath')
    $script:VSInstallationPath = $output.StandardOutput.Trim()

    $output = Get-ProcessOutput -FileName $script:VSWhere -Args ($args + ' -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe')
    $script:MSBuildExe = ($output.StandardOutput -split '\n')[0].Trim()
}

function VSX {
    param(
        [Parameter(Position = 0, ValueFromPipeline = $true)]
        [string] $Target
    )

    $Toolchain = Get-Toolchain $Target
    if ([string]::IsNullOrEmpty($Toolchain)) {
        Write-Diagnostic "Requires a target to be specified."
        return
    }

    FindVisualStudio "$Toolchain"
    
    if ($null -eq $script:VSInstallationPath) {
        Warn "Toolchain $Toolchain is not installed on your development machine, skipping build."
        Return
    }
    
    Write-Diagnostic "Visual Studio Installation path: $script:VSInstallationPath"
    Write-Diagnostic "Starting build targeting toolchain $Toolchain"

    Msvs "$EasyHookSln" "$Toolchain" 'netfx3.5-Release' 'x64'
    Msvs "$EasyHookSln" "$Toolchain" 'netfx3.5-Release' 'Win32'
    Msvs "$EasyHookSln" "$Toolchain" 'netfx4-Release' 'x64'
    Msvs "$EasyHookSln" "$Toolchain" 'netfx4-Release' 'Win32'
    
    # <MSBuild Projects="EasyHook.sln" Properties="Configuration=netfx3.5-Debug;Platform=x64" />
    # <MSBuild Projects="EasyHook.sln" Properties="Configuration=netfx3.5-Debug;Platform=Win32" />
    # <MSBuild Projects="EasyHook.sln" Properties="Configuration=netfx3.5-Release;Platform=x64" />
    # <MSBuild Projects="EasyHook.sln" Properties="Configuration=netfx3.5-Release;Platform=Win32" />
    # <MSBuild Projects="EasyHook.sln" Properties="Configuration=netfx4-Debug;Platform=x64" />
    # <MSBuild Projects="EasyHook.sln" Properties="Configuration=netfx4-Debug;Platform=Win32" />
    # <MSBuild Projects="EasyHook.sln" Properties="Configuration=netfx4-Release;Platform=x64" />
    # <MSBuild Projects="EasyHook.sln" Properties="Configuration=netfx4-Release;Platform=Win32" />

    Write-Diagnostic "Finished build targeting toolchain $Toolchain"
}

function WriteAssemblyVersionForFile {
    param(
        [Parameter(Position = 0, ValueFromPipeline = $true)]
        [string] $File
    )
    
    #$Regex = 'public const string AssemblyVersion = "(.*)"';
    $Regex = '\[assembly: AssemblyVersion\("(.*)"\)\]'
    
    $AssemblyInfo = Get-Content $File
    $NewString = $AssemblyInfo -replace $Regex, "[assembly: AssemblyVersion(""$AssemblyVersion"")]"
    
    $NewString | Set-Content $File -Encoding UTF8
}

function WriteAssemblyVersion {
    param()

    $Filename = Join-Path $EasyHookRoot EasyHook\Properties\AssemblyInfo.cs
    Write-Diagnostic "$Filename"
    WriteAssemblyVersionForFile "$Filename"
    $Filename = Join-Path $EasyHookRoot EasyLoad\Properties\AssemblyInfo.cs
    WriteAssemblyVersionForFile "$Filename"
}

function Nupkg {
}

Function Add-PathVariable {
    param (
        [string]$addPath
    )
    if (Test-Path $addPath) {
        $regexAddPath = [regex]::Escape($addPath)
        $arrPath = $env:Path -split ';' | Where-Object { $_ -notMatch 
            "^$regexAddPath\\?" }
        $env:Path = ($arrPath + $addPath) -join ';'
    }
    else {
        Throw "'$addPath' is not a valid path."
    }
}

Function Get-Toolchain {
    param(
        [string] $Target
    )
    
    switch -Exact ($Target) {
        "nupkg-only" {
            $toolchain = ""
        }
        "vs2013" {
            $toolchain = "v120"
        }
        "vs2015" {
            $toolchain = "v140"
        }
        "vs2017" {
            $toolchain = "v141"
        }
        "vs2019" {
            $toolchain = "v142"
        }
    }

    return $toolchain
}
Function Initialize-Environment {
    param(
        [string] $Target = "vs2017",
        [string] $AssemblyVersion = "2.8.0.0" 
    )

    Write-Diagnostic "Initializing EasyHook build environment."
    
    $ContinuousIntegration = ![string]::IsNullOrEmpty($env:CI)
    if ($ContinuousIntegration) {
        Write-Diagnostic "Continuous Integration build."
    }
    else {
        Write-Diagnostic "Local (non-CI) build."
    }

    Write-Diagnostic "Downloading latest version of NuGet and installing packages."

    # Download local version of NuGet
    DownloadNuget

    # Restore solution and download pakcages needed
    RestoreNugetPackages

    if ($env:APPVEYOR_BUILD_WORKER_IMAGE -eq "Visual Studio 2013") {
        $Target = "vs2013"
    }
    if ($env:APPVEYOR_BUILD_WORKER_IMAGE -eq "Visual Studio 2015") {
        $Target = "vs2015"
    }
    if ($env:APPVEYOR_BUILD_WORKER_IMAGE -eq "Visual Studio 2017") { 
        $Target = "vs2017"
    }
    if ($env:APPVEYOR_BUILD_WORKER_IMAGE -eq "Visual Studio 2019") {
        $Target = "vs2019"
    }

    $Toolchain = Get-Toolchain $Target

    FindVisualStudio $Toolchain

    $Configuration = $env:Configuration
    if ([string]::IsNullOrEmpty($Configuration)) {
        $Configuration = "netfx3.5-Debug"
    } 

    $Platform = $env:Platform
    if ([string]::IsNullOrEmpty($Platform)) {
        $Platform = "x64"
    }

    if ($Platform -eq "Win32") {
        $BuildPlatform = "x86"
    }
    else {
        $BuildPlatform = "x64"
    }

    switch -Exact ($Target) {
        "vs2013" {
            $script:VisualStudioToolVersion = "12.0"
            $script:MSBuildToolVersion = "12.0"
            $script:VXXCommonTools = Join-Path $script:VSInstallationPath '.\vc'
        }
        "vs2015" {
            $script:VisualStudioToolVersion = "14.0"
            $script:MSBuildToolVersion = "14.0"
            $script:VXXCommonTools = Join-Path $script:VSInstallationPath '.\vc'
        }
        "vs2017" {
            $script:VisualStudioToolVersion = "15.0"
            $script:MSBuildToolVersion = "14.0"
            $script:VXXCommonTools = Join-Path $script:VSInstallationPath '.\vc\auxiliary\build'
        }
        "vs2019" {
            $script:VisualStudioToolVersion = "16.0"
            $script:MSBuildToolVersion = "14.0"
            $script:VXXCommonTools = Join-Path $script:VSInstallationPath '.\vc\auxiliary\build'
        }
    }

    $VCVarsAll = Join-Path $script:VXXCommonTools vcvarsall.bat

    # Refers to the framework version to use

    if ([string]::IsNullOrEmpty($script:VXXCommonTools) -or (-not (Test-Path($script:VXXCommonTools)))) {
        Die 'Error unable to find any visual studio environment'
    }

    if (-not (Test-Path $VCVarsAll)) {
        Die "Unable to find $VCVarsAll"
    }

    if ([string]::IsNullOrEmpty($script:VSInstallationPath)) {
        Warn "Toolchain $Toolchain is not installed on your development machine, skipping build."
        Return
    }
    
    Write-Diagnostic "EasyHook version: $AssemblyVersion"
    Write-Diagnostic "Target: $Target"
    Write-Diagnostic "Toolchain: $Toolchain"
    Write-Diagnostic "Visual Studio Tool Version: $script:VisualStudioToolVersion"
    Write-Diagnostic "Visual Studio Installation Path: $script:VSInstallationPath"
    Write-Diagnostic "MSBuild: $MSBuildExe"
    Write-Diagnostic "Platform: $Platform"

    # Update assembly C# files with correct version  
    WriteAssemblyVersion

    $BatchEnvironment = Join-Path $EasyHookBin "setup_environment.bat"
    Set-Content -Path $BatchEnvironment -Value "" -Force
    Add-Content $BatchEnvironment "set VISUAL_STUDIO_NAME=$Target"
    Add-Content $BatchEnvironment "set VISUAL_STUDIO_PATH=$VSInstallationPath"
    Add-Content $BatchEnvironment "set VISUAL_STUDIO_VARS=$VCVarsAll"
    Add-Content $BatchEnvironment "set VISUAL_STUDIO_VARS_ARCH=$BuildPlatform"
    Add-Content $BatchEnvironment "set VISUAL_STUDIO_TOOL_VERSION=$script:VisualStudioToolVersion"
    Add-Content $BatchEnvironment "set TOOLCHAIN_VERSION=$Toolchain"
    Add-Content $BatchEnvironment "set EASYHOOK_TOOLS=$ToolsDir"
    Add-Content $BatchEnvironment "set EASYHOOK_ROOT=$EasyHookRoot"
    Add-Content $BatchEnvironment "set BUILD_PLATFORM=$BuildPlatform"

    Add-Content $BatchEnvironment "if ""[%APPVEYOR_BUILD_ID%]"" NEQ ""[]"" SET LOGGER=/logger:""C:\Program Files\AppVeyor\BuildAgent\Appveyor.MSBuildLogger.dll"""
    Add-Content $BatchEnvironment "if ""[%APPVEYOR_BUILD_ID%]"" == ""[]"" SET LOGGER="

    Add-Content $BatchEnvironment "set MSBUILD_TOOL_VERSION=$MSBuildToolVersion"
    Add-Content $BatchEnvironment "set MSBUILD_ARGS=/p:VisualStudioVersion=%VISUAL_STUDIO_TOOL_VERSION% %LOGGER%"
    Add-Content $BatchEnvironment "set MSBUILD_EXE=""$MSBuildExe"""
    Add-Content $BatchEnvironment "set MSBUILD=%MSBUILD_EXE% %MSBUILD_ARGS%"

    Add-Content $BatchEnvironment "if ""%VSCMD_VER%%__VCVARSALL_TARGET_ARCH%"" == """" echo Calling Visual Studio setup script: ""%VISUAL_STUDIO_VARS%"" %VISUAL_STUDIO_VARS_ARCH%"
    Add-Content $BatchEnvironment "if ""%VSCMD_VER%%__VCVARSALL_TARGET_ARCH%"" == """" call ""%VISUAL_STUDIO_VARS%"" %VISUAL_STUDIO_VARS_ARCH%"

    # We set these back in case Visual Studio or something else modified the setting
    Add-Content $BatchEnvironment "set Configuration=$Configuration"
    Add-Content $BatchEnvironment "set Platform=$Platform"

    Write-Diagnostic "Installing CoApp."
    $coAppModulePath = "C:\Program Files (x86)\Outercurve Foundation\Modules"
    $msiPath = Join-Path $script:EasyHookBin "CoApp.Tools.Powershell.msi"    
    (New-Object Net.WebClient).DownloadFile('https://easyhook.github.io/downloads/CoApp.Tools.Powershell.msi', $msiPath)
    $_ = Get-ProcessOutput -FileName "c:\windows\system32\cmd.exe" -Args "/c start /wait msiexec /i ""$msiPath"" /quiet"
    
    # Update environment path
    Add-Content $BatchEnvironment "set PSModulePath=%PSModulePath%;$coAppModulePath"
    $env:PSModulePath = $env:PSModulePath + ";$coAppModulePath"
    
    # Import CoApp module (for packaging native NuGet)
    Import-Module CoApp

    Add-Content $BatchEnvironment "echo Generated environment batch complete."

    Write-Diagnostic "Environment setup complete."
}

if ($Initialize) {
    Initialize-Environment -Target $Target
}
