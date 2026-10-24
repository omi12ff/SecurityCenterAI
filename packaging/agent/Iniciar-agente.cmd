@echo off
setlocal
if exist "%~dp0SecurityCenterAI.Agent.exe" goto ready
echo No se encontro el ejecutable. Extrae el ZIP completo antes de usar el agente.
exit /b 1

:ready
if "%~1"=="" goto help
"%~dp0SecurityCenterAI.Agent.exe" %*
exit /b %ERRORLEVEL%

:help
"%~dp0SecurityCenterAI.Agent.exe" help
set "agent_exit=%ERRORLEVEL%"
echo.
echo Para elegir una operacion, abri una terminal en esta carpeta.
echo Ejemplo: .\SecurityCenterAI.Agent.exe doctor
echo Ejemplo: .\SecurityCenterAI.Agent.exe scan
echo Sin argumentos solo se muestra ayuda. No se analiza ni se envia nada.
echo.
pause
exit /b %agent_exit%
