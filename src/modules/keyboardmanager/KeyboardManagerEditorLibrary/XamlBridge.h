#pragma once

// This class is used for handling XAML Island operations
class XamlBridge
{
public:
    // Function to run the message loop for the xaml island window
    WPARAM MessageLoop();

    // Constructor
    XamlBridge(HWND parent) :
        parentWindow(parent), lastFocusRequestId(winrt::guid()), winxamlmanager(nullptr)
    {
    }

    // Function to initialise the xaml source object
    HWND InitDesktopWindowsXamlSource(winrt::Windows::UI::Xaml::Hosting::DesktopWindowXamlSource);

    // Function to close and delete all the xaml source objects
    void ClearXamlIslands();

    // Message Handler function for Xaml Island windows
    LRESULT MessageHandler(UINT const message, WPARAM const wParam, LPARAM const lParam) noexcept;

private:
    // Stores the last window handle in focus
    HWND m_hwndLastFocus = nullptr;

    // Stores the handle of the parent native window
    HWND parentWindow = nullptr;

    // Window xaml manager for UI thread.
    WindowsXamlManager winxamlmanager;

    // Stores the GUID of the last focus request
    winrt::guid lastFocusRequestId;

    // Function to return the xaml island currently in focus
    winrt::Windows::UI::Xaml::Hosting::DesktopWindowXamlSource GetFocusedIsland();

    // Function to pre process the message on the xaml source object
    bool FilterMessage(const MSG* msg);

    // Event triggered when focus is requested
    void OnTakeFocusRequested(winrt::Windows::UI::Xaml::Hosting::DesktopWindowXamlSource const& sender, winrt::Windows::UI::Xaml::Hosting::DesktopWindowXamlSourceTakeFocusRequestedEventArgs const& args);

    // Function to return the next xaml island in focus
    winrt::Windows::UI::Xaml::Hosting::DesktopWindowXamlSource GetNextFocusedIsland(MSG* msg);

    // Function to handle focus navigation
    bool NavigateFocus(MSG* msg);

    // Stores the focus event objects
    std::vector<winrt::Windows::UI::Xaml::Hosting::DesktopWindowXamlSource::TakeFocusRequested_revoker> m_takeFocusEventRevokers;

    // Stores the xaml source objects
    std::vector<winrt::Windows::UI::Xaml::Hosting::DesktopWindowXamlSource> m_xamlSources;

    // Function invoked when the window is destroyed
    void OnDestroy(HWND);

    // Function invoked when the window is activated
    void OnActivate(HWND, UINT state, HWND hwndActDeact, BOOL fMinimized);

    // Function invoked when the window is set to focus
    void OnSetFocus(HWND, HWND hwndOldFocus);
};
