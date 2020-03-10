echo off

call %~dp0\Tools\findvs.bat

IF NOT "%vspath%"=="" (
  msbuild %~dp0..\build-package.proj /t:Clean /tv:%tv%
)

pause