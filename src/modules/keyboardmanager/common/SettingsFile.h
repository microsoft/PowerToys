#pragma once

#include <string>

#include <common/utils/json.h>

// Checked writes of Keyboard Manager's JSON files (a profile's mappings, settings.json).
namespace KeyboardManagerSettingsFile
{
    // Replaces `filePath` with `object` as a whole: the text is written to a temporary file next to
    // it, which is then renamed over it, so a failed write leaves the previous file as it was and a
    // reader never sees a half-written one. A reader that has the file open can block the rename
    // for a moment, so the rename is retried for about half a second (not for a read-only file,
    // which no retry can replace). Returns false, with the previous file intact and no temporary
    // file left, when it could not be done.
    bool WriteJsonAtomically(const std::wstring& filePath, const json::JsonObject& object);

    // Sets properties.activeConfiguration.value in the settings.json at `settingsPath` to `profile`
    // and keeps everything else in the file. Returns true only when the new file is in place. A
    // settings.json that cannot be read or parsed is left untouched (writing it back as an empty
    // object would erase the profile list and the other Keyboard Manager settings). When the file is
    // busy, the whole step is tried again from the read, so that a change another program (the
    // editor) saved in the meantime is not written over with an older copy.
    bool WriteActiveConfiguration(const std::wstring& settingsPath, const std::wstring& profile);

    // Whether two profile names are the same profile. A profile's name is the name of its file, and
    // the file system does not distinguish letter case, so neither does this.
    bool IsSameProfile(const std::wstring& left, const std::wstring& right) noexcept;
}
