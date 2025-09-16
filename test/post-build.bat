@echo off
REM Usage: post-build.bat <source_json_dir> <output_dir>

set "SRC=%~1"
set "DEST=%~2"

if not exist "%SRC%" (
    echo [Warning] Source folder not found: %SRC%
    exit /b 1
)

REM Remove destination if exists
if exist "%DEST%\json" (
    rmdir /s /q "%DEST%\json"
)

REM Copy source to destination
xcopy "%SRC%" "%DEST%\json\" /s /e /i /y

exit /b 0
