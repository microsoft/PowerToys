@echo off
setlocal ENABLEDELAYEDEXPANSION

@REM We loop here until taskkill cannot find a PowerToys process. We can't use /F flag, because it
@REM doesn't give application an opportunity to cleanup. Thus we send WM_CLOSE which is being caught
@REM by multiple windows running a msg loop in PowerToys.exe process, which we close one by one.
for /l %%x in (1, 1, 100) do (
    taskkill /IM PowerToys.exe 1>NUL 2>NUL
    if !ERRORLEVEL! NEQ 0 goto quit
)

:quit