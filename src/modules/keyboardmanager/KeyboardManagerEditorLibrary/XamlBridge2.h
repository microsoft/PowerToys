#pragma once

// This class is used for handling XAML operations
class XamlBridge2
{
public:
    // Function to run the message loop for the xaml window
    void MessageLoop();

    // Constructor
    XamlBridge2(HWND parent) : parentWindow(parent) {}

    // Function to initialize the xaml bridge
    HWND InitBridge();

    // Message Handler function for Xaml windows
    LRESULT MessageHandler(UINT const message, WPARAM const wParam, LPARAM const lParam) noexcept;

private:
    // Defines the window types for core windows
    enum WINDOW_TYPE
    {
        IMMERSIVE_BODY = 0x0,
        IMMERSIVE_DOCK = 0x1,
        IMMERSIVE_HOSTED = 0x2,
        IMMERSIVE_TEST = 0x3,
        IMMERSIVE_BODY_ACTIVE = 0x4,
        IMMERSIVE_DOCK_ACTIVE = 0x5,
        NOT_IMMERSIVE = 0x6,
    };

    // Function signature for PrivateCreateCoreWindow
    typedef HRESULT(CDECL* fnPrivateCreateCoreWindow)(WINDOW_TYPE WindowType, LPCWSTR pWindowTitle, INT X, INT Y, UINT uWidth, UINT uHeight, DWORD dwAttributes, HWND hOwnerWindow, REFIID riid, void** ppv);

    // Stores the handle of the parent native window
    HWND parentWindow = nullptr;

    // Stores the core window for the UI thread
    Core::CoreWindow coreWindow = nullptr;

    // Stores the handle of the core window
    HWND coreWindowHwnd = nullptr;

    // Stores the xaml framework view for the UI thread
    FrameworkView frameworkView;
};
