
# Overview

Telemetry from the PowerToys provider can be captured using the PowerToys.wprp file and WPR.

## Starting trace capture

To capture a trace for the PowerToys provider, run the following:

`wpr.exe -start "PowerToys.wprp"`

## Stopping trace capture

To capture a trace for the PowerToys provider, run the following:

`wpr.exe -Stop "Trace.etl"`

## Viewing Events

Open the trace.etl file in WPA.

## Additional Resources
[Tracelogging on MSDN](https://docs.microsoft.com/en-us/windows/win32/tracelogging/trace-logging-portal)

[Recording and Viewing Events](https://docs.microsoft.com/en-us/windows/win32/tracelogging/tracelogging-record-and-display-tracelogging-events)