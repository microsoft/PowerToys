@echo off
set PYTHONHOME=%CD%
set PYTHONPATH = DLLs;Lib\site-packages

python.exe -B %*
