@REM set current working directory
cd /d %~dp0
set ASPNETCORE_ENVIRONMENT=Development
@REM unset GOOGLE_CLOUD_STORAGE_BUCKET_NAME
set GOOGLE_CLOUD_STORAGE_BUCKET_NAME=
@REM unset GOOGLE_APPLICATION_CREDENTIALS
set GOOGLE_APPLICATION_CREDENTIALS=

@REM run the application tinderserver.exe
tinderserver.exe
pause
