@echo off
cd /d "%~dp0"
py -m pip install -r requirements.txt
if errorlevel 1 (echo Python not found. Install Python 3 from python.org & pause & exit /b 1)
start "" http://localhost:8000
py app.py
pause
