@echo off

call %~dp0\setup.bat %*

echo MSBuild: %MSBUILD%
echo Build Tool Version: %MSBUILD_TOOL_VERSION%
echo Visual Studio Tool Version: %VISUAL_STUDIO_TOOL_VERSION%

%MSBUILD_EXE% %EASYHOOK_ROOT%\build-package.proj /t:Clean;BeforeBuild
%MSBUILD_EXE% %EASYHOOK_ROOT%\build.proj /t:Build
%MSBUILD_EXE% %EASYHOOK_ROOT%\build-package.proj /t:PreparePackage;Package
