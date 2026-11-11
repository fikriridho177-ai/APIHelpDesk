@echo off
title Launcher WAD HelpDesk
echo ===================================================
echo   Memulai Backend (.NET) dan Frontend (Vite)
echo ===================================================

echo Menutup proses lama di port 5000 dan 5173 jika ada...
for /f "tokens=5" %%a in ('netstat -aon ^| findstr :5000 ^| findstr LISTENING') do taskkill /f /pid %%a >nul 2>&1
for /f "tokens=5" %%a in ('netstat -aon ^| findstr :5173 ^| findstr LISTENING') do taskkill /f /pid %%a >nul 2>&1

echo.
echo Menjalankan Backend API di port 5000...
start "Backend API - http://localhost:5000" cmd /k "cd /d E:\git-pkb\APIHelpDesk && dotnet run --project src/AuthUtility.Api/AuthUtility.Api.csproj"

echo.
echo Menjalankan Frontend Vite di port 5173...
start "Frontend WAD - http://localhost:5173" cmd /k "cd /d E:\git-pkb\APIHelpDesk\MyHelpDesk && npm run dev"

echo.
echo Menunggu inisialisasi server...
timeout /t 5 >nul

echo Membuka browser ke http://localhost:5173/ecs ...
start http://localhost:5173/ecs

echo ===================================================
echo   Backend aktif:  http://localhost:5000
echo   Frontend aktif: http://localhost:5173
echo   Browser telah dibuka ke http://localhost:5173/ecs
echo ===================================================
