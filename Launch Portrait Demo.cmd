@echo off
pushd "%~dp0"
if not exist "TypePressure\bin\Debug\net10.0-desktop\TypePressure.exe" (
    dotnet build TypePressure\TypePressure.csproj -f net10.0-desktop -p:TargetFrameworks=net10.0-desktop
    if errorlevel 1 (
        pause
        popd
        exit /b 1
    )
)
start "" "%~dp0TypePressure\bin\Debug\net10.0-desktop\TypePressure.exe" --portrait-demo
popd
