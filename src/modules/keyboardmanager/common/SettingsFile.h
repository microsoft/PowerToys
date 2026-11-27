#pragma once

#include <string>

#include <common/utils/json.h>

// Checked writes of Keyboard Manager's JSON files (a profile's mappings, settings.json).
namespace KeyboardManagerSettingsFile
{
    // Replaces `filePath` with `object` as a whole: the text is written to a temporary file next to
    // it, which is then renamed over it, so a failed write leaves the previous file as it was and a
    // reader never sees a half-written one. A reader that has the file open can block the rename
    // for a moment, so the rename is retried for about half a second. Returns false, with the
    // previous file intact and no temporary file left, when it could not be done.
    bool WriteJsonAtomically(const std::wstring& filePath, const json::JsonObject& object);

    // Sets properties.activeConfiguration.value in the settings.json at `settingsPath` to `profile`
    // and keeps everything else in the file. Returns true only when the new file is in place. A
    // settings.json that cannot be read or parsed is left untouched (writing it back as an empty
    // object would erase the profile list and the other Keyboard Manager settings).
    bool WriteActiveConfiguration(const std::wstring& settingsPath, const std::wstring& profile);
}
