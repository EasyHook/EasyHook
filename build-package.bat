echo off

call findvs.bat

IF NOT "%vspath%"=="" (
  msbuild build-package.proj /t:Clean;BeforeBuild /tv:%tv%
  msbuild build.proj /t:Build /tv:%tv%
  msbuild build-package.proj /t:PreparePackage;Package /tv:%tv%
)
pause