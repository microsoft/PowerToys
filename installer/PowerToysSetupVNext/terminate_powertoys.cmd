@echo off
setlocal ENABLEDELAYEDEXPANSION

@REM Check if PowerToys.exe is running before trying to kill it.
@REM This avoids hanging if taskkill behaves unexpectedly when the process doesn't exist.
tasklist /FI "IMAGENAME eq PowerToys.exe" 2>NUL | find /I "PowerToys.exe" >NUL
if errorlevel 1 exit /b 0

@REM We loop here until taskkill cannot find a PowerToys process. We can't use /F flag, because it
@REM doesn't give application an opportunity to cleanup. Thus we send WM_CLOSE which is being caught
@REM by multiple windows running a msg loop in PowerToys.exe process, which we close one by one.
@REM A small delay between attempts prevents tight spinning and gives the app time to shut down.
for /l %%x in (1, 1, 100) do (
    taskkill /IM PowerToys.exe 1>NUL 2>NUL
    if !ERRORLEVEL! NEQ 0 exit /b 0
    ping -n 1 127.0.0.1 >NUL 2>NUL
)

@REM Force kill if graceful close failed after all attempts
taskkill /F /IM PowerToys.exe 1>NUL 2>NUL
exit /b 0