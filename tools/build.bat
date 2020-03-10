@ECHO OFF

call %~dp0\setup.bat %*

%POWERSHELL% %POWERSHELL_CONSOLE% "& '%EASYHOOK_TOOLS%\build.ps1' %*";
