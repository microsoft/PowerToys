#include "pch.h"
#include "StylesReportTool.h"

#include <dwmapi.h>
#include <shlobj.h>

#include <filesystem>
#include <fstream>
#include <map>
#include <format>

inline std::optional<std::wstring> get_last_error_message(const DWORD dw)
{
    std::optional<std::wstring> message;
    try
    {
        const auto msg = std::system_category().message(dw);
        message.emplace(begin(msg), end(msg));
    }
    catch (...)
    {
    }
    return message;
}

inline std::wstring get_last_error_or_default(const DWORD dw)
{
    auto message = get_last_error_message(dw);
    return message.has_value() ? message.value() : L"";
}

std::filesystem::path get_desktop_path()
{
    wchar_t* p;
    if (S_OK != SHGetKnownFolderPath(FOLDERID_Desktop, 0, NULL, &p)) return "";

    std::filesystem::path result = p;
    CoTaskMemFree(p);

    return result;
}

// Get the executable path or module name for modern apps
inline std::wstring get_process_path(DWORD pid) noexcept
{
    auto process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, TRUE, pid);
    std::wstring name;
    if (process != INVALID_HANDLE_VALUE)
    {
        name.resize(MAX_PATH);
        DWORD name_length = static_cast<DWORD>(name.length());
        if (QueryFullProcessImageNameW(process, 0, static_cast<LPWSTR>(name.data()), &name_length) == 0)
        {
            name_length = 0;
        }
        name.resize(name_length);
        CloseHandle(process);
    }
    return name;
}

// Get the executable path or module name for modern apps
inline std::wstring get_process_path(HWND window) noexcept
{
    const static std::wstring app_frame_host = L"ApplicationFrameHost.exe";

    DWORD pid{};
    GetWindowThreadProcessId(window, &pid);
    auto name = get_process_path(pid);

    if (name.length() >= app_frame_host.length() &&
        name.compare(name.length() - app_frame_host.length(), app_frame_host.length(), app_frame_host) == 0)
    {
        // It is a UWP app. We will enumerate the windows and look for one created
        // by something with a different PID
        DWORD new_pid = pid;

        EnumChildWindows(
            window, [](HWND hwnd, LPARAM param) -> BOOL {
                auto new_pid_ptr = reinterpret_cast<DWORD*>(param);
                DWORD pid;
                GetWindowThreadProcessId(hwnd, &pid);
                if (pid != *new_pid_ptr)
                {
                    *new_pid_ptr = pid;
                    return FALSE;
                }
                else
                {
                    return TRUE;
                }
            },
            reinterpret_cast<LPARAM>(&new_pid));

        // If we have a new pid, get the new name.
        if (new_pid != pid)
        {
            return get_process_path(new_pid);
        }
    }

    return name;
}

class Logger
{
private:
    inline static std::wofstream logger;

public:
    ~Logger()
    {
        logger.close();
    }

    static void init(std::string loggerName)
    {
        std::filesystem::path rootFolder(get_desktop_path());

        auto logsPath = rootFolder;
        logsPath.append(L"window_styles.txt");

        logger.open(logsPath.string(), std::ios_base::out | std::ios_base::app);
    }

    template<typename FormatString, typename... Args>
    static void log(FormatString fmt, Args&&... args)
    {
        logger << std::vformat(fmt, std::make_wformat_args(args...)) << std::endl;
    }
};

std::map<DWMWINDOWATTRIBUTE, std::wstring> dwmAttributesReadable = {
    {DWMWINDOWATTRIBUTE::DWMWA_NCRENDERING_ENABLED, L"DWMWA_NCRENDERING_ENABLED"},
    {DWMWINDOWATTRIBUTE::DWMWA_CAPTION_BUTTON_BOUNDS, L"DWMWA_CAPTION_BUTTON_BOUNDS"},
    {DWMWINDOWATTRIBUTE::DWMWA_EXTENDED_FRAME_BOUNDS, L"DWMWA_EXTENDED_FRAME_BOUNDS"},
    {DWMWINDOWATTRIBUTE::DWMWA_CLOAKED, L"DWMWA_CLOAKED"},
};

template <typename T>
void LogDwmInfo(HWND window, DWMWINDOWATTRIBUTE attr, T& value)
{
    if (DwmGetWindowAttribute(window, attr, &value, sizeof(value)) == S_OK)
    {
        Logger::log(L"{}: {} ", dwmAttributesReadable[attr], value);
    }
    else
    {
        Logger::log(L"Failed to get {}", dwmAttributesReadable[attr]);
    }
}

void LogDwmRect(HWND window, DWMWINDOWATTRIBUTE attr, RECT& value)
{
    if (DwmGetWindowAttribute(window, attr, &value, sizeof(value)) >= 0)
    {
        Logger::log(L"{}: LT({},{}), RB({},{}), [{} x {}] ", dwmAttributesReadable[attr], value.left, value.top, value.right, value.bottom, value.right - value.left, value.bottom - value.top);
    }
    else
    {
        Logger::log(L"Failed to get {}", dwmAttributesReadable[attr]);
    }
}

void LogStyles(HWND window)
{
    auto style = GetWindowLong(window, GWL_STYLE);

    Logger::log(L"------------------ Style --------------------- ");
    Logger::log(L"");

    Logger::log(L"WS_BORDER           {}", ((style & WS_BORDER) == WS_BORDER));
    Logger::log(L"WS_CAPTION          {}", ((style & WS_CAPTION) == WS_CAPTION));
    Logger::log(L"WS_CHILD            {}", ((style & WS_CHILD) == WS_CHILD));
    Logger::log(L"WS_CHILDWINDOW      {}", ((style & WS_CHILDWINDOW) == WS_CHILDWINDOW));
    Logger::log(L"WS_CLIPCHILDREN     {}", ((style & WS_CLIPCHILDREN) == WS_CLIPCHILDREN));
    Logger::log(L"WS_CLIPSIBLINGS     {}", ((style & WS_CLIPSIBLINGS) == WS_CLIPSIBLINGS));
    Logger::log(L"WS_DISABLED         {}", ((style & WS_DISABLED) == WS_DISABLED));
    Logger::log(L"WS_DLGFRAME         {}", ((style & WS_DLGFRAME) == WS_DLGFRAME));
    Logger::log(L"WS_GROUP            {}", ((style & WS_GROUP) == WS_GROUP));
    Logger::log(L"WS_HSCROLL          {}", ((style & WS_HSCROLL) == WS_HSCROLL));
    Logger::log(L"WS_ICONIC           {}", ((style & WS_ICONIC) == WS_ICONIC));
    Logger::log(L"WS_MAXIMIZE         {}", ((style & WS_MAXIMIZE) == WS_MAXIMIZE));
    Logger::log(L"WS_MAXIMIZEBOX      {}", ((style & WS_MAXIMIZEBOX) == WS_MAXIMIZEBOX));
    Logger::log(L"WS_MINIMIZE         {}", ((style & WS_MINIMIZE) == WS_MINIMIZE));
    Logger::log(L"WS_MINIMIZEBOX      {}", ((style & WS_MINIMIZEBOX) == WS_MINIMIZEBOX));
    Logger::log(L"WS_OVERLAPPED       {}", ((style & WS_OVERLAPPED) == WS_OVERLAPPED));
    Logger::log(L"WS_OVERLAPPEDWINDOW {}", ((style & WS_OVERLAPPEDWINDOW) == WS_OVERLAPPEDWINDOW));
    Logger::log(L"WS_POPUP            {}", ((style & WS_POPUP) == WS_POPUP));
    Logger::log(L"WS_POPUPWINDOW      {}", ((style & WS_POPUPWINDOW) == WS_POPUPWINDOW));
    Logger::log(L"WS_SIZEBOX          {}", ((style & WS_SIZEBOX) == WS_SIZEBOX));
    Logger::log(L"WS_SYSMENU          {}", ((style & WS_SYSMENU) == WS_SYSMENU));
    Logger::log(L"WS_TABSTOP          {}", ((style & WS_TABSTOP) == WS_TABSTOP));
    Logger::log(L"WS_THICKFRAME       {}", ((style & WS_THICKFRAME) == WS_THICKFRAME));
    Logger::log(L"WS_TILED            {}", ((style & WS_TILED) == WS_TILED));
    Logger::log(L"WS_TILEDWINDOW      {}", ((style & WS_TILEDWINDOW) == WS_TILEDWINDOW));
    Logger::log(L"WS_VISIBLE          {}", ((style & WS_VISIBLE) == WS_VISIBLE));
    Logger::log(L"WS_VSCROLL          {}", ((style & WS_VSCROLL) == WS_VSCROLL));

    Logger::log(L"");
}

void LogExStyles(HWND window)
{
    auto exStyle = GetWindowLong(window, GWL_EXSTYLE);
    Logger::log(L"------------------ Exstyle --------------------- ");
    Logger::log(L"");

    Logger::log(L"WS_EX_ACCEPTFILES         {}", (exStyle & WS_EX_ACCEPTFILES) == WS_EX_ACCEPTFILES);
    Logger::log(L"WS_EX_APPWINDOW           {}", (exStyle & WS_EX_APPWINDOW) == WS_EX_APPWINDOW);
    Logger::log(L"WS_EX_CLIENTEDGE          {}", (exStyle & WS_EX_CLIENTEDGE) == WS_EX_CLIENTEDGE);
    Logger::log(L"WS_EX_COMPOSITED          {}", (exStyle & WS_EX_COMPOSITED) == WS_EX_COMPOSITED);
    Logger::log(L"WS_EX_CONTEXTHELP         {}", (exStyle & WS_EX_CONTEXTHELP) == WS_EX_CONTEXTHELP);
    Logger::log(L"WS_EX_CONTROLPARENT       {}", (exStyle & WS_EX_CONTROLPARENT) == WS_EX_CONTROLPARENT);
    Logger::log(L"WS_EX_DLGMODALFRAME       {}", (exStyle & WS_EX_DLGMODALFRAME) == WS_EX_DLGMODALFRAME);
    Logger::log(L"WS_EX_LAYERED             {}", (exStyle & WS_EX_LAYERED) == WS_EX_LAYERED);
    Logger::log(L"WS_EX_LAYOUTRTL           {}", (exStyle & WS_EX_LAYOUTRTL) == WS_EX_LAYOUTRTL);
    Logger::log(L"WS_EX_LEFT                {}", (exStyle & WS_EX_LEFT) == WS_EX_LEFT);
    Logger::log(L"WS_EX_LEFTSCROLLBAR       {}", (exStyle & WS_EX_LEFTSCROLLBAR) == WS_EX_LEFTSCROLLBAR);
    Logger::log(L"WS_EX_LTRREADING          {}", (exStyle & WS_EX_LTRREADING) == WS_EX_LTRREADING);
    Logger::log(L"WS_EX_MDICHILD            {}", (exStyle & WS_EX_MDICHILD) == WS_EX_MDICHILD);
    Logger::log(L"WS_EX_NOACTIVATE          {}", (exStyle & WS_EX_NOACTIVATE) == WS_EX_NOACTIVATE);
    Logger::log(L"WS_EX_NOINHERITLAYOUT     {}", (exStyle & WS_EX_NOINHERITLAYOUT) == WS_EX_NOINHERITLAYOUT);
    Logger::log(L"WS_EX_NOPARENTNOTIFY      {}", (exStyle & WS_EX_NOPARENTNOTIFY) == WS_EX_NOPARENTNOTIFY);
    Logger::log(L"WS_EX_NOREDIRECTIONBITMAP {}", (exStyle & WS_EX_NOREDIRECTIONBITMAP) == WS_EX_NOREDIRECTIONBITMAP);
    Logger::log(L"WS_EX_OVERLAPPEDWINDOW    {}", (exStyle & WS_EX_OVERLAPPEDWINDOW) == WS_EX_OVERLAPPEDWINDOW);
    Logger::log(L"WS_EX_PALETTEWINDOW       {}", (exStyle & WS_EX_PALETTEWINDOW) == WS_EX_PALETTEWINDOW);
    Logger::log(L"WS_EX_RIGHT               {}", (exStyle & WS_EX_RIGHT) == WS_EX_RIGHT);
    Logger::log(L"WS_EX_RIGHTSCROLLBAR      {}", (exStyle & WS_EX_RIGHTSCROLLBAR) == WS_EX_RIGHTSCROLLBAR);
    Logger::log(L"WS_EX_RTLREADING          {}", (exStyle & WS_EX_RTLREADING) == WS_EX_RTLREADING);
    Logger::log(L"WS_EX_STATICEDGE          {}", (exStyle & WS_EX_STATICEDGE) == WS_EX_STATICEDGE);
    Logger::log(L"WS_EX_TOOLWINDOW          {}", (exStyle & WS_EX_TOOLWINDOW) == WS_EX_TOOLWINDOW);
    Logger::log(L"WS_EX_TOPMOST             {}", (exStyle & WS_EX_TOPMOST) == WS_EX_TOPMOST);
    Logger::log(L"WS_EX_TRANSPARENT         {}", (exStyle & WS_EX_TRANSPARENT) == WS_EX_TRANSPARENT);
    Logger::log(L"WS_EX_WINDOWEDGE          {}", (exStyle & WS_EX_WINDOWEDGE) == WS_EX_WINDOWEDGE);

    Logger::log(L"");
}

void LogDwmAttributes(HWND window)
{
    Logger::log(L"------------------ DwmAttributes --------------------- ");
    Logger::log(L"");

    int intValue{};
    unsigned int uintValue{};

    LogDwmInfo(window, DWMWINDOWATTRIBUTE::DWMWA_NCRENDERING_ENABLED, intValue);
    LogDwmInfo(window, DWMWINDOWATTRIBUTE::DWMWA_CLOAKED, intValue);

    RECT rectValue{};
    LogDwmRect(window, DWMWINDOWATTRIBUTE::DWMWA_CAPTION_BUTTON_BOUNDS, rectValue);
    LogDwmRect(window, DWMWINDOWATTRIBUTE::DWMWA_EXTENDED_FRAME_BOUNDS, rectValue);

    Logger::log(L"");
}

void LogVirtualDesktopInfo(HWND window)
{
    Logger::log(L"------------------ VirtualDesktop info --------------------- ");
    Logger::log(L"");

    IVirtualDesktopManager* vdManager = nullptr;
    auto res = CoCreateInstance(CLSID_VirtualDesktopManager, nullptr, CLSCTX_ALL, IID_PPV_ARGS(&vdManager));
    if (FAILED(res))
    {
        Logger::log(L"Failed to create VirtualDesktopManager instance");
        Logger::log(L"");
        return;
    }

    BOOL isWindowOnCurrentDesktop = false;
    if (vdManager->IsWindowOnCurrentVirtualDesktop(window, &isWindowOnCurrentDesktop) == S_OK)
    {
        Logger::log(L"Window is on current virtual desktop: {}", isWindowOnCurrentDesktop);
    }

    GUID id{};
    auto vdIdRes = vdManager->GetWindowDesktopId(window, &id);
    if (vdIdRes == S_OK)
    {
        OLECHAR* guidString;
        if (StringFromCLSID(id, &guidString) == S_OK)
        {
            Logger::log(L"Virtual desktop id: {}", guidString);
        }

        CoTaskMemFree(guidString);
    }
    else
    {
        Logger::log(L"GetWindowDesktopId error: {}", get_last_error_or_default(vdIdRes));
    }

    if (vdManager)
    {
        vdManager->Release();
    }

    Logger::log(L"");
}

void LogInfo(HWND window)
{
    auto processPath = get_process_path(window);
    auto app = processPath;
    auto pos = processPath.find_last_of('\\');
    if (pos != std::string::npos && pos + 1 < processPath.length())
    {
        app = processPath.substr(pos + 1);
    }

    Logger::log(L"Timestamp: {}", std::chrono::system_clock::now());
    Logger::log(L"Window: {}", app);

    WCHAR className[256];
    auto classNameLength = GetClassName(window, className, sizeof(className));
    if (classNameLength > 0)
    {
        Logger::log(L"Class: {}", className);
    }
    else
    {
        Logger::log(L"GetClassName error: {}", get_last_error_or_default(GetLastError()));   
    }
    
    Logger::log(L"");

    LogStyles(window);
    LogExStyles(window);
    LogDwmAttributes(window);
    LogVirtualDesktopInfo(window);

    Logger::log(L"=======================================");
    Logger::log(L"");
}

LRESULT CALLBACK    WndProc(HWND, UINT, WPARAM, LPARAM);

int APIENTRY wWinMain(_In_ HINSTANCE hInstance,
                     _In_opt_ HINSTANCE hPrevInstance,
                     _In_ LPWSTR    lpCmdLine,
                     _In_ int       nCmdShow)
{
    UNREFERENCED_PARAMETER(hPrevInstance);
    UNREFERENCED_PARAMETER(lpCmdLine);

    Logger::init("StylesReportTool");

    WNDCLASSEXW wcex;
    wcex.cbSize = sizeof(WNDCLASSEX);
    wcex.style = {};
    wcex.lpfnWndProc = WndProc;
    wcex.cbClsExtra = 0;
    wcex.cbWndExtra = 0;
    wcex.hInstance = hInstance;
    wcex.hIcon = LoadIcon(hInstance, MAKEINTRESOURCE(IDI_WINDOWSTYLESICON));
    wcex.hCursor = LoadCursor(nullptr, IDC_ARROW);
    wcex.hbrBackground = (HBRUSH)(COLOR_WINDOW + 1);
    wcex.lpszMenuName = L"";
    wcex.lpszClassName = L"StylesReportTool";
    wcex.hIconSm = LoadIcon(wcex.hInstance, MAKEINTRESOURCE(IDI_SMALLICON));

    if (!RegisterClassExW(&wcex))
    {
        Logger::log(L"Register class error: {}", get_last_error_or_default(GetLastError()));
        return FALSE;
    }

    HWND hWnd = CreateWindowW(L"StylesReportTool", L"Window Style Report Tool", WS_OVERLAPPEDWINDOW, CW_USEDEFAULT, 0, 600, 200, nullptr, nullptr, hInstance, nullptr);
    if (!hWnd)
    {
        Logger::log(L"Window creation error: {}", get_last_error_or_default(GetLastError()));
        return FALSE;
    }

    if (!RegisterHotKey(hWnd, 1, MOD_ALT | MOD_CONTROL | MOD_NOREPEAT, 0x53)) // ctrl + alt + s
    {
        Logger::log(L"Failed to register hotkey: {}", get_last_error_or_default(GetLastError()));
        return FALSE;
    }

    ShowWindow(hWnd, nCmdShow);
    UpdateWindow(hWnd);

    MSG msg{};
    while (GetMessage(&msg, nullptr, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }

    return (int) msg.wParam;
}

LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam)
{
    switch (message)
    {
    case WM_HOTKEY:
    {
        LogInfo(GetForegroundWindow());
        PostQuitMessage(0);
    }
    break;
    case WM_PAINT:
        {
            PAINTSTRUCT ps;
            HDC hdc = BeginPaint(hWnd, &ps);

            LPCWSTR text = L"Please select the target window (using a mouse or Alt+Tab), \r\nand press Ctrl+Alt+S to capture its styles. \r\nYou can find the output file \"window_styles.txt\" on your desktop.";
            RECT rc{0,50,600,200};
            DrawText(hdc, text, static_cast<int>(wcslen(text)), &rc, DT_CENTER | DT_WORDBREAK);
            
            EndPaint(hWnd, &ps);
        }
        break;
    case WM_DESTROY:
        PostQuitMessage(0);
        break;
    default:
        return DefWindowProc(hWnd, message, wParam, lParam);
    }
    return 0;
}
