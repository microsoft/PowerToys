#pragma once
#include <string>

struct LogSettings
{
    // The following strings are not localizable
    inline const static std::wstring defaultLogLevel = L"trace";
    inline const static std::wstring logLevelOption = L"logLevel";
    inline const static std::string runnerLoggerName = "runner";
    inline const static std::wstring logPath = L"Logs\\";
    inline const static std::wstring runnerLogPath = L"RunnerLogs\\runner-log.log";
    inline const static std::string actionRunnerLoggerName = "action-runner";
    inline const static std::wstring actionRunnerLogPath = L"RunnerLogs\\action-runner-log.log";
    inline const static std::string updateLoggerName = "update";
    inline const static std::wstring updateLogPath = L"UpdateLogs\\update-log.log";
    inline const static std::string fileExplorerLoggerName = "FileExplorer";
    inline const static std::wstring fileExplorerLogPath = L"Logs\\file-explorer-log.log";
    inline const static std::string gcodePrevLoggerName = "GcodePrevHandler";
    inline const static std::wstring gcodePrevLogPath = L"logs\\FileExplorer_localLow\\GcodePreviewHandler\\gcode-prev-handler-log.log";
    inline const static std::string gcodeThumbLoggerName = "GcodeThumbnailProvider";
    inline const static std::wstring gcodeThumbLogPath = L"logs\\FileExplorer_localLow\\GcodeThumbnailProvider\\gcode-thumbnail-provider-log.log";
    inline const static std::string bgcodePrevLoggerName = "bgcodePrevHandler";
    inline const static std::wstring bgcodePrevLogPath = L"logs\\FileExplorer_localLow\\BgcodePreviewHandler\\bgcode-prev-handler-log.log";
    inline const static std::string bgcodeThumbLoggerName = "BgcodeThumbnailProvider";
    inline const static std::wstring bgcodeThumbLogPath = L"logs\\FileExplorer_localLow\\BgcodeThumbnailProvider\\bgcode-thumbnail-provider-log.log";
    inline const static std::string mdPrevLoggerName = "MDPrevHandler";
    inline const static std::wstring mdPrevLogPath = L"logs\\FileExplorer_localLow\\MDPrevHandler\\md-prev-handler-log.log";
    inline const static std::string monacoPrevLoggerName = "MonacoPrevHandler";
    inline const static std::wstring monacoPrevLogPath = L"logs\\FileExplorer_localLow\\MonacoPrevHandler\\monaco-prev-handler-log.log";
    inline const static std::string pdfPrevLoggerName = "PdfPrevHandler";
    inline const static std::wstring pdfPrevLogPath = L"logs\\FileExplorer_localLow\\PdfPrevHandler\\pdf-prev-handler-log.log";
    inline const static std::string pdfThumbLoggerName = "PdfThumbnailProvider";
    inline const static std::wstring pdfThumbLogPath = L"logs\\FileExplorer_localLow\\PdfThumbnailProvider\\pdf-thumbnail-provider-log.log";
    inline const static std::string qoiPrevLoggerName = "QoiPrevHandler";
    inline const static std::wstring qoiPrevLogPath = L"logs\\FileExplorer_localLow\\QoiPreviewHandler\\qoi-prev-handler-log.log";
    inline const static std::string qoiThumbLoggerName = "QoiThumbnailProvider";
    inline const static std::wstring qoiThumbLogPath = L"logs\\FileExplorer_localLow\\QoiThumbnailProvider\\qoi-thumbnail-provider-log.log";
    inline const static std::string stlThumbLoggerName = "StlThumbnailProvider";
    inline const static std::wstring stlThumbLogPath = L"logs\\FileExplorer_localLow\\StlThumbnailProvider\\stl-thumbnail-provider-log.log";
    inline const static std::string svgPrevLoggerName = "SvgPrevHandler";
    inline const static std::wstring svgPrevLogPath = L"logs\\FileExplorer_localLow\\SvgPrevHandler\\svg-prev-handler-log.log";
    inline const static std::string svgThumbLoggerName = "SvgThumbnailProvider";
    inline const static std::wstring svgThumbLogPath = L"logs\\FileExplorer_localLow\\SvgThumbnailProvider\\svg-thumbnail-provider-log.log";
    inline const static std::string launcherLoggerName = "launcher";
    inline const static std::wstring launcherLogPath = L"LogsModuleInterface\\launcher-log.log";
    inline const static std::string mouseWithoutBordersLoggerName = "mouseWithoutBorders";
    inline const static std::wstring mouseWithoutBordersLogPath = L"LogsModuleInterface\\mouseWithoutBorders-log.log";
    inline const static std::wstring awakeLogPath = L"Logs\\awake-log.log";
    inline const static std::wstring powerAccentLogPath = L"quick-accent-log.log";
    inline const static std::string fancyZonesLoggerName = "fancyzones";
    inline const static std::wstring fancyZonesLogPath = L"fancyzones-log.log";
    inline const static std::wstring fancyZonesOldLogPath = L"FancyZonesLogs\\"; // needed to clean up old logs
    inline const static std::string shortcutGuideLoggerName = "shortcut-guide";
    inline const static std::wstring shortcutGuideLogPath = L"ShortcutGuideLogs\\shortcut-guide-log.log";
    inline const static std::wstring powerOcrLogPath = L"Logs\\text-extractor-log.log";
    inline const static std::string keyboardManagerLoggerName = "keyboard-manager";
    inline const static std::wstring keyboardManagerLogPath = L"Logs\\keyboard-manager-log.log";
    inline const static std::string findMyMouseLoggerName = "find-my-mouse";
    inline const static std::string mouseHighlighterLoggerName = "mouse-highlighter";
    inline const static std::string mouseJumpLoggerName = "mouse-jump";
    inline const static std::string mousePointerCrosshairsLoggerName = "mouse-pointer-crosshairs";
    inline const static std::string imageResizerLoggerName = "imageresizer";
    inline const static std::string powerRenameLoggerName = "powerrename";
    inline const static std::string alwaysOnTopLoggerName = "always-on-top";
    inline const static std::string powerOcrLoggerName = "TextExtractor";
    inline const static std::string fileLocksmithLoggerName = "FileLocksmith";
    inline const static std::wstring alwaysOnTopLogPath = L"always-on-top-log.log";
    inline const static std::string hostsLoggerName = "hosts";
    inline const static std::wstring hostsLogPath = L"Logs\\hosts-log.log";
    inline const static std::string registryPreviewLoggerName = "registrypreview";
    inline const static std::string cropAndLockLoggerName = "crop-and-lock";
    inline const static std::wstring registryPreviewLogPath = L"Logs\\registryPreview-log.log";
    inline const static std::string environmentVariablesLoggerName = "environment-variables";
    inline const static std::wstring cmdNotFoundLogPath = L"Logs\\cmd-not-found-log.log";
    inline const static std::string cmdNotFoundLoggerName = "cmd-not-found";
    inline const static std::string newLoggerName = "NewPlus";
    inline const static std::string workspacesLauncherLoggerName = "workspaces-launcher";
    inline const static std::wstring workspacesLauncherLogPath = L"workspaces-launcher-log.log";
    inline const static std::string workspacesWindowArrangerLoggerName = "workspaces-window-arranger";
    inline const static std::wstring workspacesWindowArrangerLogPath = L"workspaces-window-arranger-log.log";
    inline const static std::string workspacesSnapshotToolLoggerName = "workspaces-snapshot-tool";
    inline const static std::wstring workspacesSnapshotToolLogPath = L"workspaces-snapshot-tool-log.log";
    inline const static std::string zoomItLoggerName = "zoom-it";
    inline const static std::string lightSwitchLoggerName = "light-switch";
    inline const static int retention = 30;
    std::wstring logLevel;
    LogSettings();
};

// Get log settings from file. File with default options is created if it does not exist
LogSettings get_log_settings(std::wstring_view file_name);
