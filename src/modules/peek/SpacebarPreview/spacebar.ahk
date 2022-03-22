#NoEnv  ; Recommended for performance and compatibility with future AutoHotkey releases.
; #Warn  ; Enable warnings to assist with detecting common errors.
SendMode Input  ; Recommended for new scripts due to its superior speed and reliability.
SetWorkingDir %A_ScriptDir%  ; Ensures a consistent starting directory.

#Include, getfiles.ahk

GroupAdd, FileListers, ahk_class CabinetWClass
GroupAdd, FileListers, ahk_class WorkerW
GroupAdd, FileListers, ahk_class #32770, ShellView

#IfWinActive ahk_group FileListers
Space::
    OpenSelectedFiles() {
        files := Explorer_GetSelected()
        OutputDebug, %files%
        Run, "SPACEBAR_EXE_PATH" "%files%"
    }
