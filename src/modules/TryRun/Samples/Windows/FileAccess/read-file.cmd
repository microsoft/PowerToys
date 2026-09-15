@echo off
rem Copyright (c) Microsoft Corporation
rem Licensed under the MIT license. See LICENSE in the project root.
if "%~1"=="" (
    echo Supply the absolute path to a harmless text file as the first argument.
    exit /b 2
)
rem Print the lines in sorted order without modifying the input file.
sort "%~1"
