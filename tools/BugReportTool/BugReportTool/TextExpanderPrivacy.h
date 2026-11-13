#pragma once

#include <Windows.h>

#include <cctype>
#include <cstdint>
#include <cwchar>
#include <cwctype>
#include <filesystem>
#include <fstream>
#include <string>
#include <string_view>
#include <system_error>
#include <vector>

#include <common/utils/json.h>

namespace TextExpanderPrivacy {
// The snippets file's name, which the engine (SnippetStore.FileName) and the
// editor (TextExpanderSnippetFileNames.Current) use.
inline constexpr wchar_t SnippetFileName[] =
    L"PowerToysTextExpanderSnippets.json";

// Whether a file is a snippets file, or a copy Text Expander makes of one, for
// a snippets file named <base>.json: backups
// ("snippets.json.backup-20261029-145200"), keep-both copies
// ("snippets (LAPTOP).json") and temporary files
// ("snippets.json.tmp-1a2b3c4d", ".snippets.json.<guid>.tmp"). All start with
// the base, maybe after a dot, and the base ends there, so "snippetsmith.json"
// doesn't count. The base can start with a dot itself (".private").
inline bool IsSnippetFileFamily(const std::wstring &fileName,
                                const std::wstring &base) {
  const auto startsWithBase = [&base](std::wstring_view name) {
    return !base.empty() && name.size() >= base.size() &&
           _wcsnicmp(name.data(), base.c_str(), base.size()) == 0 &&
           (name.size() == base.size() || name[base.size()] == L'.' ||
            name[base.size()] == L' ');
  };

  return startsWithBase(fileName) ||
         (!fileName.empty() && fileName.front() == L'.' &&
          startsWithBase(std::wstring_view(fileName).substr(1)));
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

// The snippets files at a configured location: as in the engine and the
// editor, the path is the snippets file, whatever its name and whether or not
// it's there, so one that was moved, renamed or deleted still has its backups
// and temporary files beside it left out; the default-named file beside it goes
// too. A folder that earlier builds kept in the setting has that file in it.
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

  if (isDirectory) {
    snippetFiles = {location / SnippetFileName};
  } else {
    snippetFiles = {location, location.parent_path() / SnippetFileName};
  }

  return true;
}

// Resolves the snippets files using the engine's
// SnippetStore.ResolveConfiguredPathInfo rules: the configured file, with the
// default-named file beside it, or the file in the default folder. Returns
// false when a location cannot be determined, so callers can refuse to create a
// report instead of risking that it contains the user's snippets.
inline bool
ResolveSnippetFiles(const std::filesystem::path &settingsRoot,
                    std::vector<std::filesystem::path> &snippetFiles) {
  namespace fs = std::filesystem;
  using namespace winrt::Windows::Data::Json;

  snippetFiles = {settingsRoot / L"TextExpander" / SnippetFileName};
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
  return ReadLocationSetting(properties, L"snippets_path", configured) &&
         (configured.empty() || SnippetFilesAt(configured, snippetFiles));
}

// Removes from a folder of the report, if it's there, each file or folder that
// matches, without looking inside it. Only the folder's own entries are looked
// at unless recursive. Returns false on any error.
template <typename Matches>
inline bool RemoveFromReport(const std::filesystem::path &folder,
                             bool recursive, Matches matches) {
  namespace fs = std::filesystem;

  std::error_code error;
  bool exists = fs::exists(folder, error) && fs::is_directory(folder, error);
  if (error || !exists) {
    return !error;
  }

  std::vector<fs::path> removed;
  fs::recursive_directory_iterator entry(folder, error);
  const fs::recursive_directory_iterator end;
  for (; !error && entry != end; entry.increment(error)) {
    bool directory = entry->is_directory(error);
    if (error) {
      break;
    }

    if (matches(entry->path(), directory)) {
      removed.push_back(entry->path());
      entry.disable_recursion_pending();
    } else if (!recursive) {
      entry.disable_recursion_pending();
    }
  }

  for (const fs::path &path : removed) {
    if (!error) {
      fs::remove_all(path, error);
    }
  }

  return !error;
}

// Removes the copies Text Expander makes of each snippets file
// (IsSnippetFileFamily) from the report, in the file's own folder, when that
// folder is inside the copied settings.
inline bool ExcludeSnippetFileFamilies(
    const std::filesystem::path &settingsRoot,
    const std::filesystem::path &reportRoot,
    const std::vector<std::filesystem::path> &snippetFiles) {
  namespace fs = std::filesystem;

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

    const std::wstring base = snippetFile.stem().wstring();
    if (!RemoveFromReport(
            reportRoot / relative, false, [&base](const fs::path &path, bool) {
              return IsSnippetFileFamily(path.filename().wstring(), base);
            })) {
      return false;
    }
  }

  return true;
}

// Whether a report keeps a file or folder of the TextExpander folder, given
// relative to it. It keeps only the settings, the engine's diagnostics and the
// logs, so a snippets file of any name there, such as one that Move snippets
// database left behind, and its backups and temporary files, never get in.
inline bool IsKeptInReport(const std::wstring &relative, bool directory) {
  const auto is = [&relative](const wchar_t *name) {
    return _wcsicmp(relative.c_str(), name) == 0;
  };
  const auto isIn = [&relative](const wchar_t *folder) {
    const size_t length = wcslen(folder);
    return _wcsnicmp(relative.c_str(), folder, length) == 0 &&
           (relative.size() == length || relative[length] == L'\\');
  };

  return isIn(L"Logs") || isIn(L"Editor\\Logs") ||
         isIn(L"ModuleInterface\\Logs") ||
         (directory ? is(L"Editor") || is(L"ModuleInterface")
                    : is(L"settings.json") || is(L"diagnostics.txt"));
}

// The largest file the engine and the editor read: .NET's Array.MaxLength.
inline constexpr std::uint64_t MaxSnippetFileSize = 0x7FFFFFC7;

// "replacement" is the longest marker: each character can be six bytes as \uXXXX, plus quotes.
inline constexpr size_t MaxEncodedMarkerBytes = 2 + 6 * 11;

// Decode only short escaped strings, not the surrounding file: snippets may
// have comments and trailing commas, and other report files need not be JSON.
inline void ObserveEscapedSnippetMarkers(std::string_view text, bool &snippets,
                                         bool &entry) {
  if (text.find("\\u") == std::string_view::npos) {
    return;
  }

  // Inspect each quote independently. A chunk may begin inside an unrelated
  // string, so treating its first quote as an opening must not hide a later one.
  for (size_t start = text.find('"');
       start != std::string_view::npos && !(snippets && entry);
       start = text.find('"', start + 1)) {
    bool unicodeEscape = false;
    for (size_t offset = 1;
         offset < MaxEncodedMarkerBytes && offset < text.size() - start;
         ++offset) {
      const char character = text[start + offset];
      if (character == '\\') {
        if (offset + 1 < text.size() - start && text[start + offset + 1] == 'u') {
          unicodeEscape = true;
        }
        ++offset; // An escaped quote does not close the string.
      } else if (character == '"') {
        if (unicodeEscape) {
          using namespace winrt::Windows::Data::Json;
          try {
            JsonValue decoded{nullptr};
            if (JsonValue::TryParse(winrt::to_hstring(text.substr(start, offset + 1)), decoded)) {
              const auto name = decoded.GetString();
              const int length = static_cast<int>(name.size());
              snippets = snippets || CompareStringOrdinal(name.c_str(), length,
                                                          L"snippets", 8, TRUE) == CSTR_EQUAL;
              entry = entry || CompareStringOrdinal(name.c_str(), length,
                                                    L"trigger", 7, TRUE) == CSTR_EQUAL ||
                      CompareStringOrdinal(name.c_str(), length,
                                           L"replacement", 11, TRUE) == CSTR_EQUAL;
            }
          } catch (const winrt::hresult_error &) {
            // Unrelated log or binary bytes need not be valid UTF-8 strings.
          }
        }
        break;
      }
    }
  }
}

// Whether a file holds snippets: "snippets" plus "trigger" or "replacement", quoted,
// in any case, including JSON-escaped spellings. The editor keeps entries without
// a trigger too. Its name doesn't matter, since
// the editor and the engine use a snippets file of any name, such as work.txt,
// and its backups and copies are named after it. Only escaped marker strings
// are parsed; the file itself may have comments and trailing commas. Bytes are
// read a piece at a time, so a large log is never held in memory whole.
// A file that can't be read to the end, or is larger than the engine reads,
// counts.
inline bool HoldsSnippets(const std::filesystem::path &file) {
  std::error_code error;
  const std::uint64_t size = std::filesystem::file_size(file, error);
  if (error || size > MaxSnippetFileSize) {
    return true;
  }

  constexpr std::string_view snippetsKey = "\"snippets\"";
  constexpr std::string_view triggerKey = "\"trigger\"";
  constexpr std::string_view replacementKey = "\"replacement\"";
  std::ifstream stream(file, std::ios::binary);
  std::string chunk(64 * 1024, '\0');
  std::string text;
  std::string carry;
  std::uint64_t read = 0;
  bool snippets = false;
  bool entry = false;
  while (stream && !(snippets && entry)) {
    stream.read(chunk.data(), static_cast<std::streamsize>(chunk.size()));
    const size_t count = static_cast<size_t>(stream.gcount());
    read += count;

    text.assign(carry);
    text.append(chunk, 0, count);

    // Keep original bytes across reads and while decoding: folding an invalid
    // \U escape into \u would otherwise turn an unrelated string into a key.
    carry.assign(text, text.size() > MaxEncodedMarkerBytes - 1
                           ? text.size() - (MaxEncodedMarkerBytes - 1)
                           : 0,
                 std::string::npos);
    ObserveEscapedSnippetMarkers(text, snippets, entry);

    // The ordinary literal markers still use the byte-search fast path.
    for (char &ch : text) {
      ch = static_cast<char>(std::tolower(static_cast<unsigned char>(ch)));
    }

    snippets = snippets || text.find(snippetsKey) != std::string::npos;
    entry = entry || text.find(triggerKey) != std::string::npos ||
            text.find(replacementKey) != std::string::npos;
  }

  return (snippets && entry) || read != size;
}

// Leaves the user's snippets out of a report: the snippets files and the copies
// Text Expander makes of them, wherever they are in the copied settings,
// everything in its TextExpander folder that IsKeptInReport doesn't keep, and
// any other file that HoldsSnippets.
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

  // Move snippets database and Sync from another PC leave the file they used
  // where it was, and a report can't know every place one was kept or what it
  // was called, so the last step finds the rest by their content.
  const std::filesystem::path moduleFolder = reportRoot / L"TextExpander";
  return ExcludeSnippetFileFamilies(settingsRoot, reportRoot, snippetFiles) &&
         RemoveFromReport(moduleFolder, true,
                          [&moduleFolder](const std::filesystem::path &path,
                                          bool directory) {
                            return !IsKeptInReport(
                                path.lexically_relative(moduleFolder).wstring(),
                                directory);
                          }) &&
         RemoveFromReport(
             reportRoot, true,
             [](const std::filesystem::path &path, bool directory) {
               return !directory && HoldsSnippets(path);
             });
}
} // namespace TextExpanderPrivacy
