# Watson queries [msft only]

## Catch all

- [Catch all](https://watsonportal.microsoft.com/CabSearch?=&DateTimeFormat=UTC&MaxRows=1000&AppScope_AppVersion=0.100.2.0&Process=*powertoys*)

> [!NOTE]
> Update the `AppScope_AppVersion` (and the installer file names below) to the current release version when triaging.

## EXE name

- [Machine installer (x64)](https://watsonportal.microsoft.com/Application?DateRange=Last%2014%20Days&MaxRows=100&AppScope_AppName=PowerToysSetup-0.100.2-x64.exe)
- [Machine installer (arm64)](https://watsonportal.microsoft.com/Application?DateRange=Last%2014%20Days&MaxRows=100&AppScope_AppName=PowerToysSetup-0.100.2-arm64.exe)
- [User installer (x64)](https://watsonportal.microsoft.com/Application?DateRange=Last%2014%20Days&MaxRows=100&AppScope_AppName=PowerToysUserSetup-0.100.2-x64.exe)
- [User installer (arm64)](https://watsonportal.microsoft.com/Application?DateRange=Last%2014%20Days&MaxRows=100&AppScope_AppName=PowerToysUserSetup-0.100.2-arm64.exe)
- [Main exe](https://watsonportal.microsoft.com/Application?DateRange=Last%2014%20Days&MaxRows=100&AppScope_AppVersion=0.100.2&AppScope_AppName=PowerToys.exe)
- [PT Run / example](https://watsonportal.microsoft.com/Application?DateRange=Last%2014%20Days&MaxRows=100&AppScope_AppVersion=0.100.2&AppScope_AppName=PowerToys.PowerLauncher.exe)

## DLL based

- [KBM](https://watsonportal.microsoft.com/Failure/ModuleSearch?AppScope_AppVersion=0.100.2.0&FailureSearchText=keyboardmanager.dll)
- [Power Preview](https://watsonportal.microsoft.com/Failure/ModuleSearch?AppScope_AppVersion=0.100.2.0&FailureSearchText=powerpreview.dll)
- [SVG Thumbnail](https://watsonportal.microsoft.com/Failure/ModuleSearch?AppScope_AppVersion=0.100.2.0&FailureSearchText=SvgThumbnailProvider.dll)
- [SVG Preview Pane](https://watsonportal.microsoft.com/Failure/ModuleSearch?AppScope_AppVersion=0.100.2.0&FailureSearchText=SvgPreviewHandler.dll)
- [Markdown Preview Pane](https://watsonportal.microsoft.com/Failure/ModuleSearch?AppScope_AppVersion=0.100.2.0&FailureSearchText=MarkdownPreviewHandler.dll)
