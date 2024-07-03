@echo off
echo Ensure Python is installed
timeout 2 >nul
echo Checking Python...

: check python existence
set python=py;python3;python
for %%i in (%python%) do (
    %%i --version >nul 2>&1
    if %errorlevel% equ 0 (
        set python=%%i
        goto :found
    )
)
echo Python is not installed
pause
exit /b

:found
echo configuring project dependencies...
timeout 2 >nul

: run netcorehost config script
py "%~dp0\src\NetCoreHost\config.py"

: check errorlevel
if %errorlevel% neq 0 (
    echo Error configuring project dependencies
    echo Ensure you have the python dependencies in the requirements.txt installed
    pause
    exit /b
)
echo Project dependencies configured
timeout 5 >nul