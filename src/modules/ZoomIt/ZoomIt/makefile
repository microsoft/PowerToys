 # Microsoft Developer Studio Generated NMAKE File, Based on procmon.dsp
!IF "$(CFG)" == ""
CFG=release
!MESSAGE No configuration specified. Defaulting to procmon - Release.
!ENDIF 

!IF "$(CFG)" != "release" && "$(CFG)" != "debug"
!MESSAGE Invalid configuration "$(CFG)" specified.
!MESSAGE You can specify a configuration when running NMAKE
!MESSAGE by defining the macro CFG on the command line. For example:
!MESSAGE 
!MESSAGE NMAKE CFG="release"
!MESSAGE 
!MESSAGE Possible choices for configuration are:
!MESSAGE 
!MESSAGE "release" 
!MESSAGE "debug" 
!MESSAGE 
!ERROR An invalid configuration is specified.
!ENDIF 

!IF "$(OS)" == "Windows_NT"
NULL=
!ELSE 
NULL=nul
!ENDIF 


!IF  "$(CFG)" == "release"

ALL : SUBMODULEUPDATE ZOOMITX64 ZOOMIT64A ZOOMIT32

ZOOMITX64:
	msbuild.exe -m ZoomIt.sln /p:Configuration=Release /p:Platform=x64

ZOOMIT64A:
	msbuild.exe -m ZoomIt.sln /p:Configuration=Release /p:Platform=ARM64

ZOOMIT32:
	msbuild.exe -m ZoomIt.sln /p:Configuration=Release /p:Platform=Win32

!ELSEIF  "$(CFG)" == "debug"

ALL : SUBMODULEUPDATE ZOOMITX64 ZOOMIT64A ZOOMIT32

ZOOMITX64:
	msbuild.exe -m ZoomIt.sln /p:Configuration=Debug /p:Platform=x64

ZOOMIT64A:
	msbuild.exe -m ZoomIt.sln /p:Configuration=Debug /p:Platform=ARM64

ZOOMIT32:
	msbuild.exe -m ZoomIt.sln /p:Configuration=Debug /p:Platform=Win32

!ENDIF 

SUBMODULEUPDATE:
	if not exist modules\Build\.git call modules\update.cmd
