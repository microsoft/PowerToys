cd /D "%~dp0"

nuget restore ../tools/BugReportTool/BugReportTool.sln || exit /b 1
