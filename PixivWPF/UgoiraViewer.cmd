@ECHO OFF

SETLOCAL 

REM SET EXIF=exiftool.exe

SET FFMPEG=ffmpeg.exe
REM SET FFMPEG_OPT=-framerate 30 -f jpeg_pipe
SET FFMPEG_OPT=-f jpeg_pipe
SET FFMPEG_META_OPT=-metadata title="%~n1%"
SET FFMPEG_OUT_OPT=-crf 16 -r 60

SET FN=%~dpn1%
SET FM=%FN%.mp4
SET FW=%FN%.webm
SET FZ=%FN%.zip
SET FO=%FM%

IF EXIST "%FO%" GOTO RUN

:CONVERT
IF EXIST %FZ% (
  "%FFMPEG%" %FFMPEG_OPT% -i "%FZ%" %FFMPEG_OUT_OPT% %FFMPEG_META_OPT% "%FO%"
  REM "%EXIF%" -time:all -s "%FZ%" "%FO%"
)
GOTO RUN

:RUN
IF EXIST "%FO%" (
  Start "Play Video file" "%FO%"
)
GOTO END

:END
ENDLOCAL 
REM PAUSE

