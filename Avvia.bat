@echo off
title Avvio Personal Automation Tool
echo Avvio del programma in corso...
dotnet run --project PersonalAutomationTool\PersonalAutomationTool.csproj
if %errorlevel% neq 0 (
    echo.
    echo Si è verificato un errore durante l'avvio o l'esecuzione.
    pause
)
