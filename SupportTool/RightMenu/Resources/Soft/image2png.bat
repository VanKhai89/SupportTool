@echo off
set input=%~1
echo [INFO] Đang xử lý file: %input%
magick "%input%" ^
  -define psd:alpha-unblend=true ^
  -alpha set ^
  -background none ^
  -layers flatten ^ "%~dpn1.png"
echo [DONE] Hoàn tất!
