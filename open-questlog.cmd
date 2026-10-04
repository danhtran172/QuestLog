@echo off
setlocal EnableExtensions

rem Shortcut QuestLog goi file nay de luon khoi dong Desktop Shell,
rem khong mo URL trong trinh duyet mac dinh.
call "%~dp0run-web.cmd" %*
exit /b %errorlevel%
