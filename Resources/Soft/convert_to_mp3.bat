@echo off
set input=%1
set output=%~dpn1.mp3
"D:\Tool\Soft\ffmpeg\bin\ffmpeg.exe" -i "%input%" "%output%"