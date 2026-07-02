@echo off
set input=%~1

if "%input%"=="" (
    echo [ERROR] Vui long keo tha file .otf vao file .bat
    pause
    exit /b
)

echo [INFO] Dang xu ly file: %input%

set fontforge="C:\Program Files\FontForgeBuilds\bin\fontforge.exe"

%fontforge% -lang=ff -c "Open($1); Generate($2)" "%input%" "%~dpn1.ttf"

if exist "%~dpn1.ttf" (
    echo [DONE] Hoan tat! File da tao: %~dpn1.ttf
) else (
    echo [ERROR] Convert that bai!
)