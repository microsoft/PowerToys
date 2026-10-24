#pragma once

#include <Windows.h>

#include <cwchar>
#include <cwctype>
#include <filesystem>
#include <string>
#include <system_error>
#include <utility>
#include <vector>

#include <common/utils/json.h>

namespace TextExpanderPrivacy {
// The snippet file's names: the engine (SnippetStore.FileName and
// LegacyFileName) and Settings (TextExpanderSnippetFileNames) use the same two.
// A folder can hold either, so both are excluded.
inline constexpr wchar_t SnippetFileName[] =
    L"PowerToysTextExpanderSnippets.txt";
inline constexpr wchar_t LegacySnippetFileName[] = L"snippets.txt";

// Whether a file is a snippet file, or a copy Text Expander makes of one, for a
// snippet file named <base>.txt: backups ("snippets.backup.txt",
// "snippets.backup (2).txt"), keep-both copies ("snippets (LAPTOP).txt") and
// temporary files (".snippets.txt.<guid>.tmp", "snippets.txt.tmp-1a2b3c4d").
// All start with the base, maybe after a dot, and the base ends there, so
// "snippetsmith.txt" doesn't count.
inline bool IsSnippetFileFamily(const std::wstring &fileName,
                                const std::vector<std::wstring> &bases) {
  std::wstring name = !fileName.empty() && fileName.front() == L'.'
                          ? fileName.substr(1)
                          : fileName;
  for (const std::wstring &base : bases) {
    if (base.empty() || name.size() < base.size() ||
        _wcsnicmp(name.c_str(), base.c_str(), base.size()) != 0) {
      continue;
    }

    if (name.size() == base.size() || name[base.size()] == L'.' ||
        name[base.size()] == L' ') {
      return true;
    }
  }

  return false;
}

// Copies the settings tree for a report without going through directory links
// (symbolic links and junctions). The exclusions below walk the tree the same
// way, so a link can't bring a snippet folder into the report under a path they
// never look at. A link to a file is copied as the file it points to, which the
// exclusions recognize by identity. Fails on any error, so a report is never
// made from a partial copy.
inline bool
CopyWithoutDirectoryLinks(const std::filesystem::path &source,
                          const std::filesystem::path &destination) {
  namespace fs = std::filesystem;

  std::error_code error;
  fs::create_directories(destination, error);
  if (error) {
    return false;
  }

  fs::directory_iterator entry(source, error);
  if (error) {
    return false;
  }

  const fs::directory_iterator end;
  for (; entry != end; entry.increment(error)) {
    if (error) {
      return false;
    }

    // Runner holds this synchronization file exclusively for its lifetime.
    if (_wcsicmp(entry->path().filename().c_str(),
                 L"settings.json.cli-lock") == 0) {
      continue;
    }

    const fs::path target = destination / entry->path().filename();
    fs::file_status link = entry->symlink_status(error);
    if (error) {
      return false;
    }

    if (fs::is_symlink(link) || link.type() == fs::file_type::junction) {
      // Only a link to a file is copied; a link to a folder, or to nothing, is
      // left out.
      std::error_code targetError;
      if (fs::is_regular_file(fs::status(entry->path(), targetError)) &&
          !targetError) {
        fs::copy_file(entry->path(), target,
                      fs::copy_options::overwrite_existing, error);
        if (error) {
          return false;
        }
      }

      continue;
    }

    if (fs::is_directory(link)) {
      if (!CopyWithoutDirectoryLinks(entry->path(), target)) {
        return false;
      }

      continue;
    }

    fs::copy_file(entry->path(), target, fs::copy_options::overwrite_existing,
                  error);
    if (error) {
      return false;
    }
  }

  return !error;
}

inline bool ExcludeCopiedSnippetFile(const std::filesystem::path &settingsRoot,
                                     const std::filesystem::path &reportRoot,
                                     const std::filesystem::path &sourceFile) {
  namespace fs = std::filesystem;

  std::error_code error;
  if (!fs::exists(sourceFile, error)) {
    return !error;
  }

  fs::path source = fs::canonical(sourceFile, error);
  if (error) {
    return false;
  }

  fs::path root = fs::canonical(settingsRoot, error);
  if (error) {
    return false;
  }

  fs::recursive_directory_iterator entry(root, error);
  if (error) {
    return false;
  }

  const fs::recursive_directory_iterator end;
  for (; entry != end; entry.increment(error)) {
    if (error) {
      return false;
    }

    bool regularFile = entry->is_regular_file(error);
    if (error) {
      return false;
    }
    if (!regularFile) {
      continue;
    }

    bool sameFile = fs::equivalent(source, entry->path(), error);
    if (error) {
      return false;
    }
    if (!sameFile) {
      continue;
    }

    fs::path relative = entry->path().lexically_relative(root);
    if (relative.empty() || relative.is_absolute() ||
        *relative.begin() == L"..") {
      return false;
    }

    fs::remove_all(reportRoot / relative, error);
    if (error) {
      return false;
    }
  }

  return !error;
}

// Both names in a folder, since the engine reads whichever exists.
inline std::vector<std::filesystem::path>
SnippetFilesIn(const std::filesystem::path &folder) {
  return {folder / SnippetFileName, folder / LegacySnippetFileName};
}

// Reads a location setting the way the engine reads snippets_path: a string,
// bare or wrapped as
// {"value": ...}, trimmed of spaces and quotes, with environment variables
// expanded. Leaves location empty when the setting is missing or empty, or not
// fully qualified, which the engine ignores. Returns false when the value can't
// be read, so callers can fail closed.
inline bool
ReadLocationSetting(const winrt::Windows::Data::Json::JsonObject &properties,
                    const wchar_t *key, std::filesystem::path &location) {
  using namespace winrt::Windows::Data::Json;

  location.clear();
  if (!properties.HasKey(key)) {
    return true;
  }

  JsonValue pathValue = properties.GetNamedValue(key);
  if (pathValue.ValueType() == JsonValueType::Object) {
    JsonObject wrappedValue = pathValue.GetObjectW();
    if (!wrappedValue.HasKey(L"value")) {
      return false;
    }

    pathValue = wrappedValue.GetNamedValue(L"value");
  }

  if (pathValue.ValueType() != JsonValueType::String) {
    return false;
  }

  // Same normalization as the engine's Trim().Trim('"').
  std::wstring configured{pathValue.GetString().c_str()};
  while (!configured.empty() && std::iswspace(configured.front())) {
    configured.erase(configured.begin());
  }
  while (!configured.empty() && std::iswspace(configured.back())) {
    configured.pop_back();
  }
  while (!configured.empty() && configured.front() == L'"') {
    configured.erase(configured.begin());
  }
  while (!configured.empty() && configured.back() == L'"') {
    configured.pop_back();
  }

  if (configured.empty()) {
    return true;
  }

  DWORD expandedSize =
      ExpandEnvironmentStringsW(configured.c_str(), nullptr, 0);
  if (expandedSize == 0) {
    return false;
  }

  std::wstring expanded(expandedSize, L'\0');
  DWORD written = ExpandEnvironmentStringsW(configured.c_str(), expanded.data(),
                                            expandedSize);
  if (written == 0 || written > expandedSize) {
    return false;
  }

  expanded.resize(written - 1);
  std::filesystem::path candidate(expanded);

  // The engine ignores locations that are not fully qualified and uses its
  // default file.
  if (candidate.is_absolute()) {
    location = candidate;
  }

  return true;
}

// The snippet files at a configured location: both names in a folder, or a file
// together with both names beside it.
inline bool SnippetFilesAt(const std::filesystem::path &location,
                           std::vector<std::filesystem::path> &snippetFiles) {
  namespace fs = std::filesystem;

  std::error_code error;
  bool exists = fs::exists(location, error);
  if (error) {
    return false;
  }

  bool isDirectory = exists && fs::is_directory(location, error);
  if (error) {
    return false;
  }

  if (exists && !isDirectory) {
    snippetFiles = SnippetFilesIn(location.parent_path());
    snippetFiles.insert(snippetFiles.begin(), location);
  } else {
    snippetFiles = SnippetFilesIn(location);
  }

  return true;
}

// Resolves the snippet files using the engine's
// SnippetStore.ResolveConfiguredPath rules: both names in the configured or
// default folder, or a configured file together with both names beside it. The
// OneDrive folder Settings remembers for turning sync back on
// (last_sync_folder) holds the same snippets, so its files are added the same
// way. Returns false when a location cannot be determined, so callers can
// refuse to create a report instead of risking that it contains the user's
// snippets.
inline bool
ResolveSnippetFiles(const std::filesystem::path &settingsRoot,
                    std::vector<std::filesystem::path> &snippetFiles) {
  namespace fs = std::filesystem;
  using namespace winrt::Windows::Data::Json;

  snippetFiles = SnippetFilesIn(settingsRoot / L"TextExpander");
  fs::path settingsFile = settingsRoot / L"TextExpander" / L"settings.json";

  std::error_code error;
  bool settingsFileExists = fs::exists(settingsFile, error);
  if (error) {
    return false;
  }

  if (!settingsFileExists) {
    return true;
  }

  // The engine reads this file with a different JSON parser, so a file rejected
  // here may still configure a custom snippet location for the engine.
  auto settings = json::from_file(settingsFile.wstring());
  if (!settings) {
    return false;
  }

  if (!settings->HasKey(L"properties")) {
    return true;
  }

  JsonValue propertiesValue = settings->GetNamedValue(L"properties");
  if (propertiesValue.ValueType() != JsonValueType::Object) {
    return false;
  }

  JsonObject properties = propertiesValue.GetObjectW();
  fs::path configured;
  if (!ReadLocationSetting(properties, L"snippets_path", configured) ||
      (!configured.empty() && !SnippetFilesAt(configured, snippetFiles))) {
    return false;
  }

  fs::path lastSyncFolder;
  if (!ReadLocationSetting(properties, L"last_sync_folder", lastSyncFolder)) {
    return false;
  }

  if (!lastSyncFolder.empty()) {
    std::vector<fs::path> lastSyncFiles;
    if (!SnippetFilesAt(lastSyncFolder, lastSyncFiles)) {
      return false;
    }

    snippetFiles.insert(snippetFiles.end(), lastSyncFiles.begin(),
                        lastSyncFiles.end());
  }

  return true;
}

// Removes snippet files and the copies Text Expander makes of them
// (IsSnippetFileFamily) from the report: in its TextExpander folder, where Stop
// syncing keeps backups, and in any snippet folder that is inside the copied
// settings. A configured file with a name of its own also gets copies named
// after it, but only in its own folder.
inline bool ExcludeSnippetFileFamilies(
    const std::filesystem::path &settingsRoot,
    const std::filesystem::path &reportRoot,
    const std::vector<std::filesystem::path> &snippetFiles) {
  namespace fs = std::filesystem;

  const std::vector<std::wstring> standardBases{
      fs::path(SnippetFileName).stem().wstring(),
      fs::path(LegacySnippetFileName).stem().wstring()};
  std::vector<std::pair<fs::path, std::vector<std::wstring>>> folders{
      {reportRoot / L"TextExpander", standardBases}};

  std::error_code error;
  const fs::path root = fs::weakly_canonical(settingsRoot, error);
  if (error) {
    return false;
  }

  for (const fs::path &snippetFile : snippetFiles) {
    fs::path folder = fs::weakly_canonical(snippetFile.parent_path(), error);
    if (error) {
      return false;
    }

    fs::path relative = folder.lexically_relative(root);
    if (relative.empty() || relative.is_absolute() ||
        *relative.begin() == L"..") {
      continue;
    }

    std::vector<std::wstring> bases = standardBases;
    bases.push_back(snippetFile.stem().wstring());
    folders.emplace_back(reportRoot / relative, bases);
  }

  for (const auto &[reportFolder, bases] : folders) {
    bool exists = fs::is_directory(reportFolder, error);
    if (error) {
      return false;
    }
    if (!exists) {
      continue;
    }

    std::vector<fs::path> family;
    fs::directory_iterator entry(reportFolder, error);
    if (error) {
      return false;
    }

    const fs::directory_iterator end;
    for (; entry != end; entry.increment(error)) {
      if (error) {
        return false;
      }

      if (IsSnippetFileFamily(entry->path().filename().wstring(), bases)) {
        family.push_back(entry->path());
      }
    }

    if (error) {
      return false;
    }

    for (const fs::path &file : family) {
      fs::remove_all(file, error);
      if (error) {
        return false;
      }
    }
  }

  return true;
}

inline bool
ExcludeConfiguredSnippetFile(const std::filesystem::path &settingsRoot,
                             const std::filesystem::path &reportRoot) {
  std::vector<std::filesystem::path> snippetFiles;
  if (!ResolveSnippetFiles(settingsRoot, snippetFiles)) {
    return false;
  }

  for (const std::filesystem::path &snippetFile : snippetFiles) {
    if (!ExcludeCopiedSnippetFile(settingsRoot, reportRoot, snippetFile)) {
      return false;
    }
  }

  return ExcludeSnippetFileFamilies(settingsRoot, reportRoot, snippetFiles);
}
} // namespace TextExpanderPrivacy
