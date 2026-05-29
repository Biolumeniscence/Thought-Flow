@echo off
setlocal

set "ROOT=%~dp0"
set "SLN=%ROOT%ThoughtFlow.sln"
set "APP=%ROOT%ThoughtFlow\bin\Debug\net10.0-windows\ThoughtFlow.exe"
set "DLL=%ROOT%ThoughtFlow\bin\Debug\net10.0-windows\ThoughtFlow.dll"

if not exist "%APP%" (
    echo ThoughtFlow.exe was not found. Building the app first...
    dotnet build "%SLN%"
    if errorlevel 1 (
        echo.
        echo Build failed. Press any key to close this window.
        pause >nul
        exit /b 1
    )
)

echo Starting Thought Flow...
dotnet "%DLL%"
if errorlevel 1 (
    echo.
    echo Thought Flow closed with an error. Press any key to close this window.
    pause >nul
)
endlocal