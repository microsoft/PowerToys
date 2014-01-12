:: Call with buildclrmodule.bat <AnyCPU|x64> <INPUT_DIRECTORY> <OUTPUT_PATH>

@echo off

set TARGET_PLATFORM=%1
set INPUT_DIRECTORY=%~2
set INPUT_PATH="%INPUT_DIRECTORY%\clrmodule.il"
set OUTPUT_PATH=%3

if %TARGET_PLATFORM%==AnyCPU goto SETUP32
if %TARGET_PLATFORM%==x64 goto SETUP64
goto ERROR_BAD_PLATFORM

:SETUP32
set INCLUDE_PATH="%INPUT_DIRECTORY%\x86"
goto BUILD_CLR_MODULE

:SETUP64
set INCLUDE_PATH="%INPUT_DIRECTORY%\x64"
set ILASM_EXTRA_ARGS=/pe64 /x64
goto BUILD_CLR_MODULE

:ERROR_BAD_PLATFORM
echo Unknown target platform: %TARGET_PLATFORM%
exit /b 1

:ERROR_MISSING_INPUT
echo Can't find input file: %INPUT_PATH%
exit /b 1

:BUILD_CLR_MODULE
if not exist %INPUT_PATH% goto ERROR_MISSING_INPUT
%windir%\Microsoft.NET\Framework\v4.0.30319\ilasm /nologo /quiet /dll %ILASM_EXTRA_ARGS% /include=%INCLUDE_PATH% /output=%OUTPUT_PATH% %INPUT_PATH%

::: 2.0
:::%windir%\Microsoft.NET\Framework\v2.0.50727\ilasm /nologo /quiet /dll %ILASM_EXTRA_ARGS% /include=%INCLUDE_PATH% /output=%OUTPUT_PATH% %INPUT_PATH%
