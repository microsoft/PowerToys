#include "pch.h"
#include <windows.h>
#include <string>
#include <fstream>
#include <filesystem>
#include <chrono>
#include <iomanip>
#include <sstream>

// Research Mode stub implementation
// This file provides a basic skeleton for the Research Mode module. It listens for clipboard updates
// and appends copied text to a Markdown file in a user-configurable folder. Full functionality,
// including configuration UI and robust parsing, should be implemented in future commits.

namespace ResearchMode
{
    static std::wstring g_researchFolder;

    // Sets the folder where research logs will be stored. Should be called during initialization.
    void SetResearchFolder(const std::wstring &folder)
    {
        g_researchFolder = folder;
    }

    // Helper function to append text to the daily research log file.
    void AppendToLog(const std::wstring &text)
    {
        using namespace std::chrono;
        auto now   = system_clock::now();
        auto t     = system_clock::to_time_t(now);
        std::tm tm = {};
        localtime_s(&tm, &t);

        wchar_t filename[64];
        wcsftime(filename, 64, L"%Y-%m-%d Research.md", &tm);
        std::filesystem::path logPath = std::filesystem::path(g_researchFolder) / filename;

        // Open file for append. Use wide char stream to support Unicode.
        std::wofstream ofs(logPath, std::ios::app);
        if (!ofs.is_open())
        {
            return;
        }

        wchar_t timeBuf[64];
        wcsftime(timeBuf, 64, L"%Y-%m-%d %H:%M:%S", &tm);

        // Write formatted entry to the log
        ofs << L"### " << timeBuf << L"\n\n";
        ofs << text << L"\n\n";
        ofs << L"---\n";
    }

    // Called when the clipboard updates. Reads text and writes to log. For images and other
    // formats, additional handling would be required.
    void OnClipboardUpdate()
    {
        if (!OpenClipboard(nullptr))
        {
            return;
        }

        HANDLE hData = GetClipboardData(CF_UNICODETEXT);
        if (hData)
        {
            LPCWSTR clipText = static_cast<LPCWSTR>(GlobalLock(hData));
            if (clipText)
            {
                AppendToLog(clipText);
                GlobalUnlock(hData);
            }
        }

        CloseClipboard();
    }

    // Initialization function to set up clipboard listener. In a full implementation, this
    // would register a hidden window and call AddClipboardFormatListener.
    void Start()
    {
        // Placeholder: Add clipboard hook or listener here.
    }

    // Cleanup function to remove clipboard listener.
    void Stop()
    {
        // Placeholder: Remove clipboard hook or listener here.
    }
} // namespace ResearchMode
