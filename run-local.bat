@echo off
setlocal
title Launcher HelpDesk (API + Frontend)
set "ROOT=%~dp0"
set "FRONTEND=%ROOT%..\MyHelpDesk"

echo ===================================================
echo   Memulai Backend (.NET) dan Frontend (Vite)
echo ===================================================

if not exist "%FRONTEND%\package.json" (
  echo Folder frontend tidak ditemukan: "%FRONTEND%"
  echo Pastikan repo MyHelpDesk berada di samping folder APIHelpDesk.
  pause
  exit /b 1
)

echo Menutup proses lama di port 5000 dan 5173 jika ada...
for /f "tokens=5" %%a in ('netstat -aon ^| findstr /C:":5000 " ^| findstr LISTENING') do taskkill /f /pid %%a >nul 2>&1
for /f "tokens=5" %%a in ('netstat -aon ^| findstr /C:":5173 " ^| findstr LISTENING') do taskkill /f /pid %%a >nul 2>&1

echo.
echo Menjalankan Backend API di port 5000...
start "Backend API - http://localhost:5000" /D "%ROOT%" cmd /k dotnet run --project src\AuthUtility.Api\AuthUtility.Api.csproj

echo.
echo Menjalankan Frontend Vite di port 5173...
start "Frontend HelpDesk - http://localhost:5173" /D "%FRONTEND%" cmd /k npm run dev

echo.
echo Menunggu inisialisasi server...
timeout /t 8 /nobreak >nul

echo Membuka browser ke http://localhost:5173/ ...
start "" http://localhost:5173/

echo ===================================================
echo   Backend aktif:  http://localhost:5000
echo   Frontend aktif: http://localhost:5173
echo   Swagger:        http://localhost:5000/swagger
echo ===================================================
