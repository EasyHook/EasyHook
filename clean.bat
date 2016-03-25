echo off

call findvs.bat

IF NOT "%vspath%"=="" (
  msbuild build-package.proj /t:Clean /tv:%tv%
)

pause