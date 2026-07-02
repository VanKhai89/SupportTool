@echo off
set input=%~1
echo [INFO] Đang xử lý file: %input%
magick "%input%" -resize 256x256 "%~dpn1.ico"
echo [DONE] Hoàn tất!
