#pragma once

namespace EditorConstants
{
    // Default window sizes
    inline const int DefaultEditKeyboardWindowWidth = 960;
    inline const int DefaultEditKeyboardWindowHeight = 600;
    inline const int MinimumEditKeyboardWindowWidth = 500;
    inline const int MinimumEditKeyboardWindowHeight = 450;
    inline const int EditKeyboardTableMinWidth = 700;
    inline const int DefaultEditShortcutsWindowWidth = 1410;
    inline const int DefaultEditShortcutsWindowHeight = 600;
    inline const int DefaultEditSingleShortcutsWindowWidth = 1080;
    inline const int DefaultEditSingleShortcutsWindowHeight = 400;
    inline const int MinimumEditShortcutsWindowWidth = 500;
    inline const int MinimumEditShortcutsWindowHeight = 500;
    inline const int MinimumEditSingleShortcutsWindowWidth = 500;
    inline const int MinimumEditSingleShortcutsWindowHeight = 600;

    inline const int EditShortcutsTableMinWidth = 1000;

    // Key Remap table constants
    inline const long RemapTableColCount = 4;
    inline const long RemapTableHeaderCount = 2;
    inline const long RemapTableOriginalColIndex = 0;
    inline const long RemapTableArrowColIndex = 1;
    inline const long RemapTableNewColIndex = 2;
    inline const long RemapTableRemoveColIndex = 3;
    inline const DWORD64 RemapTableDropDownWidth = 160;
    inline const DWORD64 RemapTableDropDownSpacing = 10;
    inline const long RemapTargetColumnWidth = 3 * RemapTableDropDownWidth + 3 * RemapTableDropDownSpacing + 65;

    // Shortcut table constants
    inline const long ShortcutTableColCount = 5;
    inline const long ShortcutTableHeaderCount = 3;
    inline const long ShortcutTableOriginalColIndex = 0;
    inline const long ShortcutTableArrowColIndex = 1;
    inline const long ShortcutTableNewColIndex = 2;
    inline const long ShortcutTableTargetAppColIndex = 3;
    inline const long ShortcutTableRemoveColIndex = 4;
    inline const long ShortcutArrowColumnWidth = 90;
    inline const DWORD64 ShortcutTableDropDownWidth = 160;
    inline const long ShortcutTableDropDownSpacing = 10;
    inline const long ShortcutOriginColumnWidth = 3 * ShortcutTableDropDownWidth + 3 * ShortcutTableDropDownSpacing;
    inline const long ShortcutTargetColumnWidth = 3 * ShortcutTableDropDownWidth + 3 * ShortcutTableDropDownSpacing + 15;

    // Drop down height used for both Edit Keyboard and Edit Shortcuts
    inline const DWORD64 TableDropDownHeight = 200;
    inline const DWORD64 TableArrowColWidth = 130;
    inline const DWORD64 TableRemoveColWidth = 20;
    inline const DWORD64 TableWarningColWidth = 20;
    inline const DWORD64 TableTargetAppColWidth = ShortcutTableDropDownWidth + TableRemoveColWidth * 2;

    // Shared style constants for both Remap Table and Shortcut Table
    inline const DWORD64 HeaderButtonWidth = 100;

    // Minimum and maximum size of a shortcut
    inline const long MinShortcutSize = 2; // 1 modifier key
    inline const long MaxShortcutSize = 5; // 4 modifier keys
}