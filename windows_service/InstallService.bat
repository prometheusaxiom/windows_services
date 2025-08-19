@echo off
setlocal

echo Installing File Monitor Service...

REM === Config ===
set "SERVICE_NAME=FileMonitorService"
set "DISPLAY_NAME=File Monitor Service"
REM Fix the path: %SystemRoot% points to C:\Windows; use %SystemDrive% or absolute path
set "EXE_PATH=%SystemDrive%\Users\Test\Personal_Projects\windows_services\windows_service\windows_service\bin\Debug\windows_service.exe"

REM Ensure script is run as Administrator
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo This script must be run as Administrator!
    echo Right-click and select "Run as administrator".
    pause
    exit /b 1
)

REM Validate the service executable exists
if not exist "%EXE_PATH%" (
    echo ERROR: Service executable not found:
    echo   "%EXE_PATH%"
    echo Update EXE_PATH above, then run again.
    pause
    exit /b 1
)

REM Create Event Log source (idempotent)
echo Creating Event Log source...
powershell -NoProfile -Command ^
  "if (-not [System.Diagnostics.EventLog]::SourceExists('%SERVICE_NAME%')) { [System.Diagnostics.EventLog]::CreateEventSource('%SERVICE_NAME%', 'Application'); Write-Host 'Event source created.' } else { Write-Host 'Event source already exists.' }"

REM If service exists: stop and delete to avoid 'already exists' error
sc query "%SERVICE_NAME%" >nul 2>&1
if %errorlevel% equ 0 (
    echo Stopping existing service...
    net stop "%SERVICE_NAME%" >nul 2>&1

    echo Deleting existing service...
    sc delete "%SERVICE_NAME%" >nul
    REM brief wait to let SCM flush the delete
    timeout /t 2 >nul
)

REM Create (install) the service
echo Installing service...
REM Note: binPath= value itself must be quoted; keep the space after '='
sc create "%SERVICE_NAME%" binPath= "\"%EXE_PATH%\"" start= auto DisplayName= "%DISPLAY_NAME%"
if %errorlevel% neq 0 (
    echo Failed to install service!
    pause
    exit /b 1
)

REM Optional: ensure start mode (defensive in case of edits)
sc config "%SERVICE_NAME%" start= auto >nul

REM Start the service
echo Starting service...
net start "%SERVICE_NAME%"
if %errorlevel% equ 0 (
    echo Service started successfully!
    echo.
    echo You can check the service status in:
    echo - services.msc (look for "%DISPLAY_NAME%")
    echo - Event Viewer ^> Windows Logs ^> Application
) else (
    echo Failed to start service. Check Event Viewer for details.
)

echo.
pause
