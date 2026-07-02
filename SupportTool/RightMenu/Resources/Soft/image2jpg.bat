@echo off
set input=%~1
echo [INFO] Đang xử lý file: %input%
magick "%input%" -flatten "%~dpn1.jpg"
echo [DONE] Hoàn tất!
