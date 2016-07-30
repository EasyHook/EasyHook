echo off

call findvs.bat

IF NOT "%vspath%"=="" (
  nuget install MSBuildTasks -Version 1.5.0.196
  msbuild build-package.proj /t:Clean;BeforeBuild /tv:%tv%
  msbuild build.proj /t:Build /tv:%tv%
  msbuild build-package.proj /t:PreparePackage;Package /tv:%tv%
)
pause