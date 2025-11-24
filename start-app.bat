@echo off
echo ================================================
echo Starting Finance API Full Stack Application
echo ================================================
echo.

REM Start backend in a new window
echo [1/2] Starting Backend API...
start "Finance API Backend" /D "c:\Users\Charan\source\repos\FinanceApi" cmd /k "dotnet run --launch-profile http && pause"

REM Wait a few seconds for backend to start
echo Waiting for backend to start...
timeout /t 8 /nobreak

REM Start frontend in a new window
echo [2/2] Starting React Frontend...
start "Finance API Frontend" /D "c:\Users\Charan\source\repos\FinanceApi\frontend" cmd /k "npm start && pause"

echo.
echo ================================================
echo Both applications are starting!
echo ================================================
echo Backend API:     http://localhost:5199
echo Backend Swagger: http://localhost:5199/swagger
echo Frontend App:    http://localhost:3000
echo.
echo Close the separate windows to stop each service.
echo ================================================
