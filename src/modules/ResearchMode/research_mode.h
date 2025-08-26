#pragma once

#include <string>

namespace ResearchMode
{
    // Sets the directory where research log files will be stored.
    void SetResearchFolder(const std::wstring& folder);

    // Appends the given content to the current research log file.
     AppendToLog(const std::wstring& content);

    // Handler for clipboard update events. Should be called when clipboard content changes.
    void OnClipboardUpdate();

    // Starts listening for clipboard changes and logging them.
    void Start();

    // Stops listening for clipboard changes.
    void Stop();
}
