SET "%vspath="
SET "tv="

if NOT "%VS140COMNTOOLS%"=="" (
  REM Visual Studio 2015
  SET "vspath=%VS140COMNTOOLS%"
  SET "tv=14.0"
) else (
  if NOT "%VS120COMNTOOLS%"=="" (
    REM Visual Studio 2013
    SET "vspath=%VS120COMNTOOLS%"
    SET "tv=12.0"
  )
)

IF "%vspath%"=="" (
  ECHO Could not find Visual Studio path for supported toolset versions 12.0 or 14.0
) ELSE (
  call "%vspath%\..\..\VC\vcvarsall.bat" x86_amd64
)