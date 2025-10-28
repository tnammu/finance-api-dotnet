@echo off
echo ================================================
echo Finance API - Setup and Start
echo ================================================
echo.

echo [1/3] Checking frontend dependencies...
if not exist "frontend\node_modules\" (
    echo Installing frontend dependencies (this may take 1-2 minutes)...
    cd frontend
    call npm install
    cd ..
    echo.
    echo Dependencies installed!
    echo.
) else (
    echo Frontend dependencies already installed.
    echo.
)

echo [2/3] Starting Backend API...
start "Finance API Backend" cmd /k "dotnet run"

echo Waiting for backend to start...
timeout /t 8 /nobreak >nul

echo [3/3] Starting React Frontend...
start "Finance API Frontend" cmd /k "cd frontend && npm start"

echo.
echo ================================================
echo Applications are starting!
echo ================================================
echo Backend API:  http://localhost:5000
echo Frontend App: http://localhost:3000 (will open automatically)
echo.
echo Close the separate windows to stop each service.
echo ================================================
pause
