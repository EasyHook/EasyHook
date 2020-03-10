::
:: Helper script that just executes PowerShell install script.
::

@echo off

echo.
echo ===============================
echo. EasyHook CI Install Script 
echo ===============================
echo.

ECHO Platform: %Platform%
ECHO Configuration: %Configuration%

call %~dp0\setup.bat

echo.
echo ===============================
echo.

ECHO Platform: %Platform%
ECHO Configuration: %Configuration%
echo AppVeyor Build Worker Image: %APPVEYOR_BUILD_WORKER_IMAGE%
ECHO Visual Studio Name: %VISUAL_STUDIO_NAME%
ECHO Visual Studio Path: %VISUAL_STUDIO_PATH%
ECHO Toolchain Version: %TOOLCHAIN_VERSION%
ECHO MSBuild Tool Version: %MSBUILD_TOOL_VERSION%

echo.
echo ===============================
