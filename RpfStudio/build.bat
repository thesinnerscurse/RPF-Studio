@echo off
cd /d "%~dp0"
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /codepage:65001 /out:RpfStudio.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll Program.cs
if %errorlevel%==0 echo Built RpfStudio.exe
