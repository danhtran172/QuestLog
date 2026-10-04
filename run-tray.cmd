@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem ============================================================
rem  QuestLog Tray - bat/tat tray bang tay de test
rem
rem  Tray la mot TIEN TRINH RIENG. run-web.cmd chi mo API :5099,
rem  UI :5173 va vo desktop, KHONG mo tray. File nay de chay tray
rem  rieng khi can test ba service cua no:
rem
rem    playtime  dem gio choi, ghi LastPlayedAt va PlayDiary
rem    scanner   quet thu vien game, dong bo DetectedProcesses
rem    backup    sao luu thu muc save khi game tat hoac save doi
rem              (TAM TAT - xem MasterPlay.Core/FeatureFlags.cs)
rem
rem  Cach dung:
rem    run-tray.cmd           bat tray (dung ban build san co)
rem    run-tray.cmd -b        build lai roi bat
rem    run-tray.cmd stop      tat tray
rem    run-tray.cmd restart   tat roi bat lai (de nap ban build moi)
rem    run-tray.cmd status    xem tray co dang chay khong
rem    run-tray.cmd tail      in 25 dong log moi nhat
rem    run-tray.cmd log       mo thu muc log
rem
rem  LUU Y ve "stop": tray khong co cua so nen khong nhan duoc lenh
rem  thoat em tu ben ngoai, o day la kill cung. Tray chi ghi so giay
rem  choi xuong CSDL moi 30 giay, nen kill cung co the mat toi 30
rem  giay cua phien dang choi. Muon thoat sach thi chuot phai vao
rem  icon tray va bam Exit.
rem ============================================================

cd /d "%~dp0"

set "TRAYPROJ=%~dp0MasterPlay.TrayApp\MasterPlay.TrayApp.csproj"
set "TRAYEXE=%~dp0MasterPlay.TrayApp\bin\Debug\net8.0-windows\MasterPlay.TrayApp.exe"
set "TRAYLOG=%~dp0MasterPlay.TrayApp\bin\Debug\net8.0-windows\logs"
set "TRAYIMAGE=MasterPlay.TrayApp.exe"

set "ARG1=%~1"
if /i "%ARG1%"=="stop"    goto :cmd_stop
if /i "%ARG1%"=="restart" goto :cmd_restart
if /i "%ARG1%"=="status"  goto :cmd_status
if /i "%ARG1%"=="tail"    goto :cmd_tail
if /i "%ARG1%"=="log"     goto :cmd_log
if /i "%ARG1%"=="help"    goto :cmd_help
if /i "%ARG1%"=="-h"      goto :cmd_help
if /i "%ARG1%"=="/?"      goto :cmd_help
goto :start


rem ---------------------------------------------------------- bat
:start
set "NEEDBUILD="
if /i "%ARG1%"=="-b" set "NEEDBUILD=1"
if not exist "%TRAYEXE%" set "NEEDBUILD=1"

if defined NEEDBUILD (
    echo [1/2] Build tray...
    dotnet build "%TRAYPROJ%" -c Debug --nologo -v q
    if errorlevel 1 (
        echo.
        echo *** BUILD TRAY THAT BAI ***
        pause
        exit /b 1
    )
) else (
    echo [1/2] Bo qua build ^(da co san^). Dung "run-tray.cmd -b" de build lai.
)

call :isrunning
if not errorlevel 1 (
    call :getpid
    echo [2/2] Tray DA CHAY san ^(PID !TRAYPID!^). Chi mot tray duoc chay mot luc.
    echo       Dung "run-tray.cmd restart" de nap ban build moi.
    exit /b 0
)

echo [2/2] Mo tray...
start "" "%TRAYEXE%"

rem Cho toi 3 giay cho tien trinh len roi kiem chung lai.
set /a _tries=12
:waitup
ping -n 1 -w 250 127.0.0.2 >nul 2>&1
call :isrunning
if not errorlevel 1 goto :upok
set /a _tries-=1
if %_tries% gtr 0 goto :waitup

echo.
echo *** Tray khong len duoc. Xem log: %TRAYLOG% ***
pause
exit /b 1

:upok
call :getpid
echo.
echo Tray dang chay ^(PID !TRAYPID!^). Icon nam o khay he thong.
echo Chuot phai vao icon de bat/tat tung service, hoac bam Exit de thoat sach.
echo Log: %TRAYLOG%
exit /b 0


rem ---------------------------------------------------------- tat
:cmd_stop
call :dostop
exit /b %errorlevel%

:cmd_restart
call :dostop
set "ARG1="
goto :start

:dostop
call :isrunning
if errorlevel 1 (
    echo Tray khong chay san.
    exit /b 0
)

call :getpid
echo Dang tat tray ^(PID !TRAYPID!^)...
taskkill /im "%TRAYIMAGE%" /f >nul 2>&1
ping -n 1 -w 700 127.0.0.2 >nul 2>&1

call :isrunning
if not errorlevel 1 (
    echo *** Con tien trinh tray chua tat. ***
    exit /b 1
)
echo Da tat. Kill cung nen co the mat toi 30 giay cua phien dang choi chua kip ghi.
exit /b 0


rem ------------------------------------------------------- trang thai
:cmd_status
if exist "%TRAYEXE%" (
    for %%f in ("%TRAYEXE%") do echo Ban build: %%~tf
) else (
    echo Ban build: CHUA CO. Chay "run-tray.cmd -b" de build.
)

call :isrunning
if errorlevel 1 (
    echo Tien trinh: khong chay.
) else (
    call :getpid
    echo Tien trinh: dang chay, PID !TRAYPID!.
)

echo Log: %TRAYLOG%
exit /b 0

:cmd_tail
call :lastlog
if not defined LASTLOG (
    echo Chua co file log nao trong %TRAYLOG%
    exit /b 1
)
echo --- 25 dong cuoi cua !LASTLOG! ---
powershell -NoProfile -Command "Get-Content -LiteralPath '!LASTLOG!' -Tail 25"
exit /b 0

:cmd_log
if not exist "%TRAYLOG%" mkdir "%TRAYLOG%" >nul 2>&1
start "" explorer.exe "%TRAYLOG%"
exit /b 0

:cmd_help
echo run-tray.cmd           bat tray
echo run-tray.cmd -b        build lai roi bat
echo run-tray.cmd stop      tat tray
echo run-tray.cmd restart   tat roi bat lai
echo run-tray.cmd status    xem trang thai
echo run-tray.cmd tail      in 25 dong log moi nhat
echo run-tray.cmd log       mo thu muc log
exit /b 0


rem ============ ham ============

rem :isrunning   errorlevel 0 = tray dang chay
:isrunning
tasklist /fi "IMAGENAME eq %TRAYIMAGE%" 2>nul | findstr /i "%TRAYIMAGE%" >nul 2>&1
exit /b %errorlevel%

rem :getpid   dat bien TRAYPID
:getpid
set "TRAYPID="
for /f "tokens=2" %%p in ('tasklist /fi "IMAGENAME eq %TRAYIMAGE%" /fo table /nh 2^>nul ^| findstr /i "%TRAYIMAGE%"') do set "TRAYPID=%%p"
exit /b 0

rem :lastlog   dat bien LASTLOG = file log moi nhat
:lastlog
set "LASTLOG="
if not exist "%TRAYLOG%" exit /b 0
for /f "delims=" %%f in ('dir /b /o-d "%TRAYLOG%\*.txt" 2^>nul') do (
    set "LASTLOG=%TRAYLOG%\%%f"
    goto :lastlogdone
)
:lastlogdone
exit /b 0
