// CursorLog.cpp : Monitors mouse position and logs to file with monitor/DPI info
//

#include <iostream>
#include <fstream>
#include <string>
#include <filesystem>
#include <Windows.h>
#include <ShellScalingApi.h>

#pragma comment(lib, "Shcore.lib")

// Global variables
std::ofstream g_outputFile;
HHOOK g_mouseHook = nullptr;
POINT g_lastPosition = { LONG_MIN, LONG_MIN };
DWORD g_mainThreadId = 0;

// Get monitor information for a given point
std::string GetMonitorInfo(POINT pt, UINT* dpiX, UINT* dpiY)
{
    HMONITOR hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
    if (!hMonitor)
        return "Unknown";

    MONITORINFOEX monitorInfo = {};
    monitorInfo.cbSize = sizeof(MONITORINFOEX);
    GetMonitorInfo(hMonitor, &monitorInfo);

    // Get DPI for this monitor
    if (SUCCEEDED(GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, dpiX, dpiY)))
    {
        // DPI retrieved successfully
    }
    else
    {
        *dpiX = 96;
        *dpiY = 96;
    }

    // Convert device name to string using proper wide-to-narrow conversion
    std::wstring deviceName(monitorInfo.szDevice);
    int sizeNeeded = WideCharToMultiByte(CP_UTF8, 0, deviceName.c_str(), static_cast<int>(deviceName.length()), nullptr, 0, nullptr, nullptr);
    std::string result(sizeNeeded, 0);
    WideCharToMultiByte(CP_UTF8, 0, deviceName.c_str(), static_cast<int>(deviceName.length()), &result[0], sizeNeeded, nullptr, nullptr);
    return result;
}

// Calculate scale factor from DPI
constexpr double GetScaleFactor(UINT dpi)
{
    return static_cast<double>(dpi) / 96.0;
}

// Low-level mouse hook callback
LRESULT CALLBACK LowLevelMouseProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    if (nCode == HC_ACTION && wParam == WM_MOUSEMOVE)
    {
        MSLLHOOKSTRUCT* mouseStruct = reinterpret_cast<MSLLHOOKSTRUCT*>(lParam);
        POINT pt = mouseStruct->pt;

        // Only log if position changed
        if (pt.x != g_lastPosition.x || pt.y != g_lastPosition.y)
        {
            g_lastPosition = pt;

            UINT dpiX = 96, dpiY = 96;
            std::string monitorName = GetMonitorInfo(pt, &dpiX, &dpiY);
            double scale = GetScaleFactor(dpiX);

            if (g_outputFile.is_open())
            {
                g_outputFile << monitorName
                    << "," << pt.x
                    << "," << pt.y
                    << "," << dpiX
                    << "," << static_cast<int>(scale * 100) << "%"
                    << "\n";
                g_outputFile.flush();
            }
        }
    }

    return CallNextHookEx(g_mouseHook, nCode, wParam, lParam);
}

// Console control handler for clean shutdown
BOOL WINAPI ConsoleHandler(DWORD ctrlType)
{
    if (ctrlType == CTRL_C_EVENT || ctrlType == CTRL_CLOSE_EVENT)
    {
        std::cout << "\nShutting down..." << std::endl;

        if (g_mouseHook)
        {
            UnhookWindowsHookEx(g_mouseHook);
            g_mouseHook = nullptr;
        }

        if (g_outputFile.is_open())
        {
            g_outputFile.close();
        }

        // Post quit message to the main thread to exit the message loop
        PostThreadMessage(g_mainThreadId, WM_QUIT, 0, 0);

        return TRUE;
    }
    return FALSE;
}

int main(int argc, char* argv[])
{
    // Set DPI awareness FIRST, before any other Windows API calls
    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

    // Store main thread ID for clean shutdown
    g_mainThreadId = GetCurrentThreadId();

    // Check command line arguments
    if (argc != 2)
    {
        std::cerr << "Usage: CursorLog.exe <output_path_and_filename>" << std::endl;
        return 1;
    }

    std::filesystem::path outputPath(argv[1]);
    std::filesystem::path parentPath = outputPath.parent_path();

    // Validate the directory exists
    if (!parentPath.empty() && !std::filesystem::exists(parentPath))
    {
        std::cerr << "Error: The directory '" << parentPath.string() << "' does not exist." << std::endl;
        return 1;
    }

    // Check if file exists and prompt for overwrite
    if (std::filesystem::exists(outputPath))
    {
        std::cout << "File '" << outputPath.string() << "' already exists. Overwrite? (y/n): ";
        char response;
        std::cin >> response;

        if (response != 'y' && response != 'Y')
        {
            std::cout << "Operation cancelled." << std::endl;
            return 0;
        }
    }

    // Open output file
    g_outputFile.open(outputPath, std::ios::out | std::ios::trunc);
    if (!g_outputFile.is_open())
    {
        std::cerr << "Error: Unable to create or open file '" << outputPath.string() << "'." << std::endl;
        return 1;
    }

    std::cout << "Logging mouse position to: " << outputPath.string() << std::endl;
    std::cout << "Press Ctrl+C to stop..." << std::endl;

    // Set up console control handler
    SetConsoleCtrlHandler(ConsoleHandler, TRUE);

    // Install low-level mouse hook
    g_mouseHook = SetWindowsHookEx(WH_MOUSE_LL, LowLevelMouseProc, nullptr, 0);
    if (!g_mouseHook)
    {
        std::cerr << "Error: Failed to install mouse hook. Error code: " << GetLastError() << std::endl;
        g_outputFile.close();
        return 1;
    }

    // Message loop - required for low-level hooks
    MSG msg;
    while (GetMessage(&msg, nullptr, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }

    // Cleanup
    if (g_mouseHook)
    {
        UnhookWindowsHookEx(g_mouseHook);
    }

    if (g_outputFile.is_open())
    {
        g_outputFile.close();
    }

    return 0;
}
