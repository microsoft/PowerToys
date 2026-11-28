// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <filesystem>
#include <string>

// Deletes a file or a folder with everything in it. Returns false (and prints why) if it could not be deleted.
bool DeleteFolder(std::wstring path);

// The report is stopped before it is zipped: deletes the working copy, or says where it was left.
void DeleteReportCopyOrSayWhere(const std::filesystem::path& reportDir);

// Hides the private data in the report's copy of the settings folder (dir), and in the updater's copy in it
// (ConfigBackup). Returns false when private Keyboard Manager data could still be in the report: then the report
// must not be created.
bool HideUserPrivateInfo(const std::filesystem::path& dir);
