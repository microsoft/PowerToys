cd /D "%~dp0"

nuget restore ../tools/BugReportTool/BugReportTool.sln || exit /b 1
nuget restore ../tools/WebcamReportTool/WebcamReportTool.sln || exit /b 1
