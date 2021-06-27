@ECHO OFF

SET FFMPEG_OPT=-f jpeg_pipe
SET FFMPEG_META_OPT=-metadata title="%FN%"
SET FN=%~dpn1%
SET FW=%FN%.webm
SET FZ=%FN%.zip

echo %FW%
echo %FZ%

IF EXIST "%FW%" GOTO RUN

:CONVERT
IF EXIST %FZ% (
  ffmpeg %FFMPEG_OPT% -i "%FZ%" %FFMPEG_META_OPT% "%FW%"
)
GOTO RUN

:RUN
IF EXIST "%FW%" (
  "%FW%"
)
GOTO END

:END
REM PAUSE
