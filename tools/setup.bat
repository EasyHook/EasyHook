@echo off

set POWERSHELL=%SystemRoot%\SysWOW64\WindowsPowerShell\v1.0\powershell.exe
set POWERSHELL_CONSOLE=-NoProfile -ExecutionPolicy Bypass -Command
set POWERSHELL_NEW=-NoProfile -ExecutionPolicy Bypass -Command "& {Start-Process PowerShell -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File ""%_BUILD_SCRIPT%""'}";
set POWERSHELL_ADMIN=-NoProfile -ExecutionPolicy Bypass -Command "& {Start-Process PowerShell -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File ""%_BUILD_SCRIPT%""' -Verb RunAs}";

if "[%1]" NEQ "[]" SET TARGET_ARG=-Target %1
if "[%1]" == "[]" SET TARGET_ARG=

%POWERSHELL% %POWERSHELL_CONSOLE% "& '%~dp0\setup.ps1'" %TARGET_ARG% -Initialize

:: PowerShell script generates this batch script that sets up the environment
call %~dp0..\Bin\setup_environment.bat

set ADAPTER_PATH=%~dp0..\Packages\Appveyor.TestLogger.2.0.0\build\_common
set TEST_PLATFORM_ROOT=%~dp0..\Packages\Microsoft.TestPlatform.16.5.0\tools\net451\Common7\IDE\Extensions\TestPlatform

set APPVEYOR_TEST_ADAPTER_DLL=Microsoft.VisualStudio.TestPlatform.Extension.Appveyor.TestAdapter.dll
set APPVEYOR_TEST_LOGGER_DLL=Microsoft.VisualStudio.TestPlatform.Extension.Appveyor.TestLogger.dll

set VSTEST=%TEST_PLATFORM_ROOT%\vstest.console.exe
set VSTEST_ARGS=/TestAdapterPath:%ADAPTER_PATH% /TestAdapterPath:%VSTEST_EXTENSIONS% /Logger:Appveyor /Parallel /Platform:%BUILD_PLATFORM% "%EASYHOOK_ROOT%\Build\%CONFIGURATION%\%BUILD_PLATFORM%\EasyHook.Tests.dll"

:: Printing this out to make sure the tool is findable
ildasm.exe /? > nul 2>&1
if "%ERRORLEVEL%" NEQ "0" goto :ERROR
echo Found 'ildasm.exe'
goto :EOF

:ERROR
echo Failed to find 'ildasm.exe'