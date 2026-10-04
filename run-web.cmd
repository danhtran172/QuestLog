@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem ============================================================
rem  QuestLog - mo ung dung desktop (WPF + WebView2)
rem
rem    API :5099  MasterPlay.Host  -> doc DB THAT o %APPDATA%\MasterPlay
rem    UI  :5173  Vite dev server  -> proxy /api sang :5099
rem
rem  Toi uu toc do so voi ban truoc:
rem    - Bo lop tu an qua wscript (bot 1 chang khoi dong)
rem    - Bo powershell Test-NetConnection (moi lan goi ton ~1s chi de khoi dong
rem      PowerShell) -> thay bang netstat + findstr, native, ~30ms
rem    - Chay THANG MasterPlay.Host.exe da build thay vi `dotnet run`
rem      (bo qua toan bo buoc danh gia MSBuild, tiet kiem ~3-5s)
rem    - Khoi dong API va UI SONG SONG thay vi tuan tu
rem
rem  Cach dung:
rem    run-web.cmd        chay nhanh (dung ban build san co)
rem    run-web.cmd -b     build lai backend truoc khi chay
rem    run-web.cmd stop   tat ca hai tien trinh
rem ============================================================

cd /d "%~dp0"

set "HOSTPROJ=%~dp0MasterPlay.Host\MasterPlay.Host.csproj"
set "HOSTEXE=%~dp0MasterPlay.Host\bin\Debug\net8.0-windows\MasterPlay.Host.exe"
set "SHELLPROJ=%~dp0MasterPlay.Shell\MasterPlay.Shell.csproj"
set "SHELLEXE=%~dp0MasterPlay.Shell\bin\Debug\net8.0-windows\QuestLog.exe"
set "WEBDIR=%~dp0MasterPlay.Web"
rem Luon dua vo desktop vao Local UI. Khong de QUESTORY_URL cua mot terminal
rem khac chuyen Shell sang URL hosted/trinh duyet.
set "URL=http://127.0.0.1:5173/"
set "QUESTORY_URL=%URL%"

if /i "%~1"=="stop" goto :stopall

rem ---------- build backend neu can ----------
set "NEEDBUILD="
if /i "%~1"=="-b" set "NEEDBUILD=1"
if not exist "%HOSTEXE%" set "NEEDBUILD=1"

if not exist "%SHELLEXE%" set "NEEDBUILD=1"

if defined NEEDBUILD (
    echo [1/4] Build backend + vo app...
    dotnet build "%HOSTPROJ%" -c Debug --nologo -v q
    if errorlevel 1 (
        echo.
        echo *** BUILD BACKEND THAT BAI ***
        pause
        exit /b 1
    )
    dotnet build "%SHELLPROJ%" -c Debug --nologo -v q
    if errorlevel 1 (
        echo.
        echo *** BUILD VO APP THAT BAI ***
        pause
        exit /b 1
    )
) else (
    echo [1/4] Bo qua build ^(da co san^). Dung "run-web.cmd -b" de build lai.
)

rem ---------- dependency frontend ----------
if not exist "%WEBDIR%\node_modules" (
    echo [2/4] npm install...
    pushd "%WEBDIR%"
    call npm install --no-audit --no-fund
    popd
) else (
    echo [2/4] node_modules OK.
)

rem ---------- khoi dong SONG SONG ----------
echo [3/4] Khoi dong API :5099 va UI :5173...

rem Token ngau nhien chi duoc truyen cho Local Host va WebView2 shell. Trinh
rem duyet mo localhost:5173 khong co token nay, nen khong the vao guest mode.
for /f "usebackq delims=" %%t in (`powershell -NoProfile -Command "[guid]::NewGuid().ToString('N')"`) do set "QUESTLOG_DESKTOP_SESSION_TOKEN=%%t"
if not defined QUESTLOG_DESKTOP_SESSION_TOKEN (
    echo *** Khong tao duoc desktop session token. ***
    exit /b 1
)

call :isup 5099
if not errorlevel 1 (
    rem Host cu khong biet token moi. Khoi dong lai dung tien trinh dang giu
    rem cong Local API de guest token chi co hieu luc trong lan chay hien tai.
    for /f "tokens=5" %%p in ('netstat -ano ^| findstr /r /c:"TCP.*:5099 .*LISTENING"') do taskkill /pid %%p /t /f >nul 2>&1
)
start "QuestLog API :5099" cmd /c ""%HOSTEXE%" --urls http://127.0.0.1:5099"

call :isup 5173
if errorlevel 1 (
    start "QuestLog WEB :5173" cmd /c "cd /d "%WEBDIR%" && npm run dev"
) else (
    echo       UI da chay san.
)

rem ---------- mo APP desktop (khong phai trinh duyet) ----------
rem Vo WebView2 tu doi dev server len (toi 30s) nen khong can cho o day.
echo [4/4] Mo app QuestLog...
start "QuestLog Desktop" "%SHELLEXE%"
echo.
echo Da mo cua so QuestLog Desktop. Khong mo trinh duyet.
echo Tat bang: run-web.cmd stop
exit /b 0


rem ============ ham ============

rem :isup <port>   errorlevel 0 = dang lang nghe
:isup
netstat -an | findstr /r /c:"TCP.*:%1 .*LISTENING" >nul 2>&1
exit /b %errorlevel%

rem :waitfor <port> <so lan thu>  (moi lan ~250ms)
:waitfor
set /a _tries=%2
:waitloop
call :isup %1
if not errorlevel 1 exit /b 0
set /a _tries-=1
if %_tries% leq 0 exit /b 1
rem ping loopback = cach ngu ~250ms khong can PowerShell
ping -n 1 -w 250 127.0.0.2 >nul 2>&1
goto :waitloop

:stopall
echo Dang tat QuestLog...

rem Kill theo PID dang GIU CONG truoc — dang tin cay nhat.
rem (Chi dua vao taskkill /im hay /fi WINDOWTITLE la khong du: tien trinh con
rem  do "start cmd /c" sinh ra co the khong khop bo loc, de sot lai process
rem  giu file .exe khien lan build sau that bai.)
for /f "tokens=5" %%p in ('netstat -ano ^| findstr /r /c:"TCP.*:5099 .*LISTENING"') do taskkill /pid %%p /t /f >nul 2>&1
for /f "tokens=5" %%p in ('netstat -ano ^| findstr /r /c:"TCP.*:5173 .*LISTENING"') do taskkill /pid %%p /t /f >nul 2>&1

rem Quet not theo ten anh, phong khi tien trinh da nha cong nhung chua thoat.
taskkill /im MasterPlay.Host.exe /f >nul 2>&1
taskkill /im QuestLog.exe /f >nul 2>&1
taskkill /fi "WINDOWTITLE eq QuestLog API*" /t /f >nul 2>&1
taskkill /fi "WINDOWTITLE eq QuestLog WEB*" /t /f >nul 2>&1

rem Kiem chung lai — bao that neu con sot.
set "_left="
tasklist /fi "IMAGENAME eq MasterPlay.Host.exe" 2>nul | findstr /i "MasterPlay.Host.exe" >nul 2>&1 && set "_left=1"
if defined _left (
    echo *** Con tien trinh MasterPlay.Host chua tat. Build lai co the that bai. ***
    exit /b 1
)
echo Da tat.
exit /b 0
