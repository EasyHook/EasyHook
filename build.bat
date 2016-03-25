echo off

call findvs.bat

IF NOT "%vspath%"=="" (
  msbuild build.proj /t:Build /tv:%tv%
)

pause