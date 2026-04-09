#pragma once

// TSF4 (Text Services Framework 4) text expansion utilities.
// Uses Windows.UI.Input.Preview.Text APIs to read/replace text in the
// currently focused text box for the Expand (abbreviation expansion) feature.

namespace Tsf4TextReplacer
{
    // Initialize the TSF4 provider on the current thread.
    // Must be called on a thread that has a message pump (typically the main thread).
    void Initialize() noexcept;

    // Whether TSF4 was initialized and is available.
    bool IsAvailable() noexcept;

    // Try to expand an abbreviation in the focused text box.
    // Reads the last |abbreviation.length()| characters before the cursor.
    // If they match |abbreviation| (case-insensitive), replaces them with |expandedText|.
    // Returns true if expansion occurred, false otherwise.
    bool TryExpand(const std::wstring& abbreviation, const std::wstring& expandedText) noexcept;

    // Release TSF4 resources. Called on shutdown.
    void Shutdown() noexcept;
}
