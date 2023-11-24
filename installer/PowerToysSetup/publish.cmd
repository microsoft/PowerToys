setlocal enableDelayedExpansion

IF NOT DEFINED PTRoot (SET PTRoot=..\..)

SET PlatformArg=%1
IF NOT DEFINED PlatformArg (SET PlatformArg=x64)
SET VCToolsVersion=!VCToolsVersion!
SET ClearDevCommandPromptEnvVars=false
