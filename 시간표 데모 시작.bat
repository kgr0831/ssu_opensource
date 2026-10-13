@echo off
setlocal
chcp 65001 >nul
title 시간표 추천 데모
cd /d "%~dp0"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0timetable-demo\start.ps1"
if errorlevel 1 (
  echo.
  echo 실행하지 못했습니다. 위의 안내를 확인해 주세요.
  echo 아무 키나 누르면 이 창을 닫습니다.
  pause >nul
  exit /b 1
)
exit /b 0
