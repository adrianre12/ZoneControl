@REM exit 0
REM exit 0
set MOD_NAME=ZoneControl
set MOD_ID=3616581249

set SE_CONTENT_DIR=N:\SteamLibrary\steamapps\workshop\content\244850
REM set TORCH_CONTENT_DIR=G:\torch-everdawn2\Instance\content\244850
set TORCH_CONTENT_DIR=G:\torch-server\Instance\content\244850


set MOD_DIR=%APPDATA%\SpaceEngineers\Mods

rmdir %SE_CONTENT_DIR%\%MOD_ID% /S /Q 
robocopy.exe %MOD_DIR%\%MOD_NAME%\ %SE_CONTENT_DIR%\%MOD_ID%\ "*.*" /S -xf *.sbmi

rmdir %TORCH_CONTENT_DIR%\%MOD_ID% /S /Q 
robocopy.exe %MOD_DIR%\%MOD_NAME%\ %TORCH_CONTENT_DIR%\%MOD_ID%\ "*.*" /S -xf *.sbmi

echo Done