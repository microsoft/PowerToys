#include "../BugReportTool/TextExpanderPrivacy.h"

#include <chrono>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <string>

namespace fs = std::filesystem;

static bool WriteText(const fs::path &path, const char *contents) {
  fs::create_directories(path.parent_path());
  std::ofstream file(path, std::ios::binary);
  file << contents;
  return file.good();
}

static std::string JsonString(const std::wstring &value) {
  std::wstring escaped;
  for (wchar_t ch : value) {
    if (ch == L'\\' || ch == L'"') {
      escaped.push_back(L'\\');
    }

    escaped.push_back(ch);
  }

  return "\"" + winrt::to_string(escaped) + "\"";
}

static bool WriteSettings(const fs::path &settingsRoot,
                          const std::string &properties) {
  std::string contents =
      "{\"name\":\"TextExpander\",\"properties\":" + properties + "}";
  return WriteText(settingsRoot / "TextExpander" / "settings.json",
                   contents.c_str());
}

static bool ResetReport(const fs::path &settingsRoot,
                        const fs::path &reportRoot) {
  std::error_code error;
  fs::remove_all(reportRoot, error);
  return TextExpanderPrivacy::CopyWithoutDirectoryLinks(settingsRoot,
                                                        reportRoot);
}

struct SettingsCase {
  const char *name;
  std::string properties;
};

int main() {
  fs::path root =
      fs::temp_directory_path() /
      ("PowerToysTextExpanderPrivacy-" +
       std::to_string(
           std::chrono::steady_clock::now().time_since_epoch().count()));
  fs::path settings = root / "settings";
  fs::path report = root / "report";
  fs::path defaultSnippet = settings / "TextExpander" / "snippets.txt";
  fs::path defaultNewSnippet =
      settings / "TextExpander" / "PowerToysTextExpanderSnippets.txt";

  auto cleanup = [&root]() {
    std::error_code error;
    fs::remove_all(root, error);
  };

  if (!WriteText(defaultSnippet, "private default snippet")) {
    std::cerr << "Could not create default snippet fixture\n";
    cleanup();
    return 1;
  }

  fs::path cliLock = settings / "settings.json.cli-lock";
  if (!WriteText(cliLock, "runner lock") ||
      !ResetReport(settings, report) || fs::exists(report / cliLock.filename())) {
    std::cerr << "Runner settings lock was copied into the report\n";
    cleanup();
    return 1;
  }

  fs::remove_all(report);
  fs::copy(settings, report, fs::copy_options::recursive);
  fs::path caseVariant = settings / "textexpander" / "SNIPPETS.TXT";
  if (!TextExpanderPrivacy::ExcludeCopiedSnippetFile(settings, report,
                                                     caseVariant) ||
      fs::exists(report / "TextExpander" / "snippets.txt")) {
    std::cerr << "Default snippet file was not excluded using a case-variant "
                 "source path\n";
    cleanup();
    return 1;
  }

  fs::path customSnippet =
      settings / "TextExpander" / "shared" / "snippets.txt";
  if (!WriteText(customSnippet, "private configured snippet")) {
    std::cerr << "Could not create configured snippet fixture\n";
    cleanup();
    return 1;
  }

  fs::remove_all(report);
  fs::copy(settings, report, fs::copy_options::recursive);
  if (!TextExpanderPrivacy::ExcludeCopiedSnippetFile(settings, report,
                                                     customSnippet) ||
      fs::exists(report / "TextExpander" / "shared" / "snippets.txt")) {
    std::cerr << "Configured snippet file was not excluded from the copied "
                 "settings tree\n";
    cleanup();
    return 1;
  }

  fs::path externalSnippet = root / "external" / "snippets.txt";
  if (!WriteText(externalSnippet, "outside the copied settings tree") ||
      !TextExpanderPrivacy::ExcludeCopiedSnippetFile(settings, report,
                                                     externalSnippet)) {
    std::cerr
        << "An external snippet path should not prevent report preparation\n";
    cleanup();
    return 1;
  }

  // The configured location must resolve exactly as the engine resolves it.
  fs::path configuredFolder = settings / "TextExpander" / "custom";
  fs::path configuredFile = configuredFolder / "snippets.txt";
  fs::path configuredNewFile =
      configuredFolder / "PowerToysTextExpanderSnippets.txt";
  fs::path configuredNamedFile = configuredFolder / "my snippets.txt";
  if (!WriteText(configuredFile, "private configured-folder snippet") ||
      !WriteText(configuredNewFile,
                 "private configured-folder snippet, current name") ||
      !WriteText(configuredNamedFile,
                 "private configured file with its own name")) {
    std::cerr << "Could not create configured-folder snippet fixture\n";
    cleanup();
    return 1;
  }

  const wchar_t *rootVariable = L"POWERTOYS_TE_PRIVACY_TEST_ROOT";
  SetEnvironmentVariableW(rootVariable, settings.wstring().c_str());
  std::wstring folder = configuredFolder.wstring();
  const SettingsCase configuredCases[] = {
      {"wrapped folder",
       "{\"snippets_path\":{\"value\":" + JsonString(folder) + "}}"},
      {"padded and quoted folder", "{\"snippets_path\":{\"value\":" +
                                       JsonString(L"  \"" + folder + L"\"  ") +
                                       "}}"},
      {"unbalanced leading quote",
       "{\"snippets_path\":{\"value\":" + JsonString(L"\"" + folder) + "}}"},
      {"environment variable",
       "{\"snippets_path\":{\"value\":" +
           JsonString(
               L"%POWERTOYS_TE_PRIVACY_TEST_ROOT%\\TextExpander\\custom") +
           "}}"},
      {"direct file path", "{\"snippets_path\":{\"value\":" +
                               JsonString(configuredFile.wstring()) + "}}"},
      {"direct file with its own name",
       "{\"snippets_path\":{\"value\":" +
           JsonString(configuredNamedFile.wstring()) + "}}"},
      {"unwrapped string", "{\"snippets_path\":" + JsonString(folder) + "}"},
  };

  // Both names are excluded wherever the engine could look, whichever of them
  // it would read.
  for (const SettingsCase &testCase : configuredCases) {
    if (!WriteSettings(settings, testCase.properties) ||
        !ResetReport(settings, report) ||
        !TextExpanderPrivacy::ExcludeConfiguredSnippetFile(settings, report) ||
        fs::exists(report / "TextExpander" / "custom" / "snippets.txt") ||
        fs::exists(report / "TextExpander" / "custom" /
                   "PowerToysTextExpanderSnippets.txt")) {
      std::cerr << "Configured snippet file was not excluded: " << testCase.name
                << "\n";
      SetEnvironmentVariableW(rootVariable, nullptr);
      cleanup();
      return 1;
    }
  }

  SetEnvironmentVariableW(rootVariable, nullptr);

  // A configured file is excluded whatever its name.
  if (!WriteSettings(settings, "{\"snippets_path\":{\"value\":" +
                                   JsonString(configuredNamedFile.wstring()) +
                                   "}}") ||
      !ResetReport(settings, report) ||
      !TextExpanderPrivacy::ExcludeConfiguredSnippetFile(settings, report) ||
      fs::exists(report / "TextExpander" / "custom" / "my snippets.txt")) {
    std::cerr
        << "A configured snippet file with its own name was not excluded\n";
    cleanup();
    return 1;
  }

  if (!WriteText(defaultNewSnippet, "private default snippet, current name")) {
    std::cerr << "Could not create the current-name default snippet fixture\n";
    cleanup();
    return 1;
  }

  // The engine falls back to the default folder for these, so both names there
  // must be excluded.
  const SettingsCase defaultCases[] = {
      {"no snippets_path", "{}"},
      {"empty value", "{\"snippets_path\":{\"value\":\"\"}}"},
      {"relative value", "{\"snippets_path\":{\"value\":\"custom\"}}"},
      {"root-relative value", "{\"snippets_path\":{\"value\":\"\\\\custom\"}}"},
      {"empty remembered sync folder",
       "{\"last_sync_folder\":{\"value\":\"\"}}"},
      {"relative remembered sync folder",
       "{\"last_sync_folder\":{\"value\":\"custom\"}}"},
  };

  for (const SettingsCase &testCase : defaultCases) {
    if (!WriteSettings(settings, testCase.properties) ||
        !ResetReport(settings, report) ||
        !TextExpanderPrivacy::ExcludeConfiguredSnippetFile(settings, report) ||
        fs::exists(report / "TextExpander" / "snippets.txt") ||
        fs::exists(report / "TextExpander" /
                   "PowerToysTextExpanderSnippets.txt")) {
      std::cerr << "Default snippet file was not excluded: " << testCase.name
                << "\n";
      cleanup();
      return 1;
    }
  }

  // The OneDrive folder Settings remembers for turning sync back on
  // (last_sync_folder) holds the same snippets, so both names there are
  // excluded too, together with the folder in use.
  fs::path otherFolder = settings / "TextExpander" / "other";
  if (!WriteText(otherFolder / "snippets.txt",
                 "private snippet in the folder in use") ||
      !WriteText(otherFolder / "PowerToysTextExpanderSnippets.txt",
                 "private snippet in the folder in use, current name")) {
    std::cerr << "Could not create the folder-in-use snippet fixture\n";
    cleanup();
    return 1;
  }

  SetEnvironmentVariableW(rootVariable, settings.wstring().c_str());
  const SettingsCase lastSyncCases[] = {
      {"remembered folder with the PowerToys folder in use",
       "{\"snippets_path\":{\"value\":\"\"},\"last_sync_folder\":{\"value\":" +
           JsonString(folder) + "}}"},
      {"remembered folder only",
       "{\"last_sync_folder\":{\"value\":" + JsonString(folder) + "}}"},
      {"remembered folder through a variable",
       "{\"last_sync_folder\":{\"value\":" +
           JsonString(
               L"%POWERTOYS_TE_PRIVACY_TEST_ROOT%\\TextExpander\\custom") +
           "}}"},
      {"padded and quoted remembered folder",
       "{\"last_sync_folder\":{\"value\":" +
           JsonString(L"  \"" + folder + L"\"  ") + "}}"},
      {"unwrapped remembered folder",
       "{\"last_sync_folder\":" + JsonString(folder) + "}"},
      {"remembered file", "{\"last_sync_folder\":{\"value\":" +
                              JsonString(configuredFile.wstring()) + "}}"},
  };

  for (const SettingsCase &testCase : lastSyncCases) {
    if (!WriteSettings(settings, testCase.properties) ||
        !ResetReport(settings, report) ||
        !TextExpanderPrivacy::ExcludeConfiguredSnippetFile(settings, report) ||
        fs::exists(report / "TextExpander" / "custom" / "snippets.txt") ||
        fs::exists(report / "TextExpander" / "custom" /
                   "PowerToysTextExpanderSnippets.txt") ||
        fs::exists(report / "TextExpander" / "snippets.txt") ||
        fs::exists(report / "TextExpander" /
                   "PowerToysTextExpanderSnippets.txt")) {
      std::cerr << "Remembered sync folder snippet file was not excluded: "
                << testCase.name << "\n";
      SetEnvironmentVariableW(rootVariable, nullptr);
      cleanup();
      return 1;
    }
  }

  SetEnvironmentVariableW(rootVariable, nullptr);

  // Both keys at once: the folder in use and the remembered one are both left
  // out.
  if (!WriteSettings(settings, "{\"snippets_path\":{\"value\":" +
                                   JsonString(otherFolder.wstring()) +
                                   "},\"last_sync_folder\":{\"value\":" +
                                   JsonString(folder) + "}}") ||
      !ResetReport(settings, report) ||
      !TextExpanderPrivacy::ExcludeConfiguredSnippetFile(settings, report) ||
      fs::exists(report / "TextExpander" / "other" / "snippets.txt") ||
      fs::exists(report / "TextExpander" / "other" /
                 "PowerToysTextExpanderSnippets.txt") ||
      fs::exists(report / "TextExpander" / "custom" / "snippets.txt") ||
      fs::exists(report / "TextExpander" / "custom" /
                 "PowerToysTextExpanderSnippets.txt")) {
    std::cerr << "The folder in use and the remembered sync folder were not "
                 "both excluded\n";
    cleanup();
    return 1;
  }

  // When the configured location cannot be determined, report preparation must
  // stop.
  const SettingsCase ambiguousCases[] = {
      {"missing wrapped value", "{\"snippets_path\":{}}"},
      {"non-string value", "{\"snippets_path\":{\"value\":42}}"},
      {"null value", "{\"snippets_path\":{\"value\":null}}"},
      {"non-object properties", "\"invalid\""},
      {"missing wrapped remembered sync folder", "{\"last_sync_folder\":{}}"},
      {"non-string remembered sync folder",
       "{\"last_sync_folder\":{\"value\":42}}"},
      {"null remembered sync folder", "{\"snippets_path\":{\"value\":\"\"},"
                                      "\"last_sync_folder\":{\"value\":null}}"},
  };

  for (const SettingsCase &testCase : ambiguousCases) {
    if (!WriteSettings(settings, testCase.properties) ||
        !ResetReport(settings, report) ||
        TextExpanderPrivacy::ExcludeConfiguredSnippetFile(settings, report)) {
      std::cerr << "Ambiguous settings did not fail closed: " << testCase.name
                << "\n";
      cleanup();
      return 1;
    }
  }

  if (!WriteText(settings / "TextExpander" / "settings.json",
                 "{\"properties\":{\"snippets_path\":{\"value\":\"x\"},}}") ||
      !ResetReport(settings, report) ||
      TextExpanderPrivacy::ExcludeConfiguredSnippetFile(settings, report)) {
    std::cerr << "Malformed settings did not fail closed\n";
    cleanup();
    return 1;
  }

  // Copies Text Expander makes of a snippet file are left out too: Stop
  // syncing's backups, keep-both copies and temporary files from saving. The
  // module's other files stay.
  fs::path moduleFolder = settings / "TextExpander";
  const char *familyFiles[] = {
      "PowerToysTextExpanderSnippets.backup.txt",
      "snippets.backup.txt",
      "snippets.backup (2).txt",
      "snippets (TESTPC).txt",
      ".PowerToysTextExpanderSnippets.txt.0123456789abcdef0123456789abcdef.tmp",
      "PowerToysTextExpanderSnippets.txt.tmp-1a2b3c4d",
  };
  for (const char *name : familyFiles) {
    if (!WriteText(moduleFolder / name, "private copy of the snippets")) {
      std::cerr << "Could not create the snippet copy fixture " << name << "\n";
      cleanup();
      return 1;
    }
  }

  if (!WriteText(moduleFolder / "diagnostics.txt", "snippet_count=3") ||
      !WriteText(moduleFolder / "snippetsmith.txt", "not a snippet file") ||
      !WriteSettings(settings, "{}") || !ResetReport(settings, report) ||
      !TextExpanderPrivacy::ExcludeConfiguredSnippetFile(settings, report)) {
    std::cerr << "Snippet copies could not be excluded\n";
    cleanup();
    return 1;
  }

  for (const char *name : familyFiles) {
    if (fs::exists(report / "TextExpander" / name)) {
      std::cerr << "A copy of the snippets was not excluded: " << name << "\n";
      cleanup();
      return 1;
    }
  }

  if (!fs::exists(report / "TextExpander" / "settings.json") ||
      !fs::exists(report / "TextExpander" / "diagnostics.txt") ||
      !fs::exists(report / "TextExpander" / "snippetsmith.txt")) {
    std::cerr << "Files that aren't snippets were excluded\n";
    cleanup();
    return 1;
  }

  // A configured file with its own name: its temporary file goes, in its own
  // folder only, so a name like settings.txt never takes the module's
  // settings.json with it.
  fs::path namedFolder = settings / "TextExpander" / "named";
  if (!WriteText(namedFolder / "settings.txt", "private configured file") ||
      !WriteText(namedFolder / "settings.txt.tmp-1a2b3c4d",
                 "private temporary copy") ||
      !WriteSettings(settings,
                     "{\"snippets_path\":{\"value\":" +
                         JsonString((namedFolder / "settings.txt").wstring()) +
                         "}}") ||
      !ResetReport(settings, report) ||
      !TextExpanderPrivacy::ExcludeConfiguredSnippetFile(settings, report) ||
      fs::exists(report / "TextExpander" / "named" / "settings.txt") ||
      fs::exists(report / "TextExpander" / "named" /
                 "settings.txt.tmp-1a2b3c4d") ||
      !fs::exists(report / "TextExpander" / "settings.json")) {
    std::cerr << "A configured file's copies were not excluded, or the "
                 "settings went with them\n";
    cleanup();
    return 1;
  }

  // A junction inside the settings that leads to the snippet folder is not
  // followed, so the linked snippets never reach the report, while ordinary
  // folders are still copied.
  fs::path linkedFolder = root / "linked";
  fs::path junction = settings / "SnippetLink";
  std::wstring makeJunction = L"cmd /c mklink /J \"" + junction.wstring() +
                              L"\" \"" + linkedFolder.wstring() +
                              L"\" >nul 2>nul";
  if (!WriteText(linkedFolder / "PowerToysTextExpanderSnippets.txt",
                 "private snippets behind a junction") ||
      _wsystem(makeJunction.c_str()) != 0 ||
      !fs::exists(junction / "PowerToysTextExpanderSnippets.txt")) {
    std::cerr << "Could not create the junction fixture\n";
    RemoveDirectoryW(junction.c_str());
    cleanup();
    return 1;
  }

  bool linkLeft =
      !WriteSettings(settings, "{\"snippets_path\":{\"value\":" +
                                   JsonString(linkedFolder.wstring()) + "}}") ||
      !ResetReport(settings, report) ||
      !TextExpanderPrivacy::ExcludeConfiguredSnippetFile(settings, report) ||
      fs::exists(report / "SnippetLink") ||
      !fs::exists(report / "TextExpander" / "settings.json");

  // A directory symbolic link too, where this account may create one.
  fs::path symlink = settings / "SnippetSymlink";
  std::error_code symlinkError;
  fs::create_directory_symlink(linkedFolder, symlink, symlinkError);
  bool symlinkTested = !symlinkError;
  if (symlinkTested) {
    linkLeft = linkLeft || !ResetReport(settings, report) ||
               fs::exists(report / "SnippetSymlink");
  }

  RemoveDirectoryW(junction.c_str());
  if (symlinkTested) {
    RemoveDirectoryW(symlink.c_str());
  }

  if (linkLeft) {
    std::cerr << "A directory link brought snippets into the report\n";
    cleanup();
    return 1;
  }

  cleanup();
  std::cout << "All Text Expander bug-report privacy checks passed"
            << (symlinkTested
                    ? ""
                    : " (directory symbolic link case skipped: no privilege)")
            << "\n";
  return 0;
}
