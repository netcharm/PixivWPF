@ECHO OFF
SETLOCAL 

SET FFMPEG=ffmpeg.exe
SET FFMPEG_METHOD="X"
REM SET FFMPEG_OPT=-framerate 30 -f jpeg_pipe
SET FFMPEG_OPT_ZIP=-hide_banner -f jpeg_pipe
SET FFMPEG_OPT_CAT=-hide_banner -f concat
SET FFMPEG_META_OPT=-metadata title="%~n1%"
REM SET FFMPEG_OUT_OPT=-vsync vfr -vf "format=yuv444p" -crf 16 -r 120
REM SET FFMPEG_OUT_OPT=-vsync vfr -vf "format=yuv444p,pad=ceil(iw/2)*2:ceil(ih/2)*2" -crf 16 -r 120
SET FFMPEG_OUT_OPT=-vsync vfr -vf "format=yuv444p,pad=ceil(iw/2)*2:ceil(ih/2)*2"

SET FN=%~dpn1%
SET FT=%~dpn1%.txt
SET FM=%FN%.mp4
SET FW=%FN%.webm
SET FZ=%FN%.zip
SET FO=%FM%

IF EXIST "%FO%" GOTO RUN

:CONVERT
IF EXIST %FZ% (
  IF /I %FFMPEG_METHOD% EQU "X" (
    IF NOT EXIST "%FN%" (MKDIR "%FN%")
    PUSHD "%FN%"
    TAR -xf "%FZ%"
    "%FFMPEG%" %FFMPEG_OPT_CAT% -i "%FT%" %FFMPEG_OUT_OPT% %FFMPEG_META_OPT% "%FO%"
    POPD
    RMDIR /S /Q "%FN%"
  ) ELSE (
    "%FFMPEG%" %FFMPEG_OPT_ZIP% -i "%FZ%" %FFMPEG_OUT_OPT% %FFMPEG_META_OPT% "%FO%"
  )
  TIMEOUT /T 1
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

