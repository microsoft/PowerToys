#pragma once

class Trace
{
public:
    static void RegisterProvider();
    static void UnregisterProvider();
    static void FilePreviewerIsEnabled();
    static void FilePreviewerIsDisabled();
    static void ExplorerSVGRenderEnabled();
    static void ExplorerSVGRenderDisabled();
    static void PreviewPaneSVGRenderEnabled();
    static void PreviewPaneSVGRenderDisabled();
    static void PreviewPaneMarkDownRenderDisabled();
    static void PreviewPaneMarkDownRenderEnabled();
    static void SetConfigInvalidJSON(const char* _exceptionMessage);
    static void InitSetErrorLoadingFile(const char* _exceptionMessage);
    static void Debug();
};
