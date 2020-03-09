param(
    [ValidateSet("vs2013", "vs2015", "vs2017", "vs2019", "nupkg-only")]
    [Parameter(Position = 0)] 
    [string] $Target = "vs2017",
    [Parameter(Position = 1)]
    [string] $AssemblyVersion = "2.8.0.0" 
)

$ToolsDir = split-path -parent $MyInvocation.MyCommand.Definition
$SetupScript = Join-Path $ToolsDir 'setup.ps1'

. $SetupScript -Target $Target

Nupkg
VSX $Target
