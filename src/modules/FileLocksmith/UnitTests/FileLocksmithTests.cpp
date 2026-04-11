#include "pch.h"
#include "CppUnitTest.h"
#include "../FileLocksmithLib/Constants.h"
#include "../FileLocksmithLib/ProcessResult.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

// ============================================================================
// Path normalization helpers (mirror logic that would be in FileLocksmith)
// ============================================================================
namespace PathUtils
{
    // Normalize forward slashes to backslashes
    inline std::wstring NormalizeSeparators(const std::wstring& path)
    {
        std::wstring result = path;
        std::replace(result.begin(), result.end(), L'/', L'\\');
        return result;
    }

    // Remove trailing backslash (unless it's root like C:\)
    inline std::wstring RemoveTrailingSlash(const std::wstring& path)
    {
        if (path.size() > 3 && path.back() == L'\\')
            return path.substr(0, path.size() - 1);
        return path;
    }

    // Case-insensitive path comparison
    inline bool PathsEqual(const std::wstring& a, const std::wstring& b)
    {
        if (a.size() != b.size())
            return false;
        for (size_t i = 0; i < a.size(); ++i)
        {
            if (towlower(a[i]) != towlower(b[i]))
                return false;
        }
        return true;
    }

    // Extract file name from full path
    inline std::wstring GetFileName(const std::wstring& path)
    {
        auto pos = path.find_last_of(L"\\/");
        if (pos == std::wstring::npos)
            return path;
        return path.substr(pos + 1);
    }
}

// ============================================================================
// Output formatting helpers (mirror get_text / get_json behavior from CLILogic)
// ============================================================================
namespace OutputFormatting
{
    inline std::wstring FormatTextOutput(const std::vector<ProcessResult>& results, const std::wstring& header, const std::wstring& noProcesses)
    {
        std::wstringstream ss;
        if (results.empty())
        {
            ss << noProcesses;
            return ss.str();
        }

        ss << header;
        for (const auto& result : results)
        {
            ss << result.pid << L"\t"
               << result.user << L"\t"
               << result.name << std::endl;
        }
        return ss.str();
    }
}

namespace FileLocksmithUnitTests
{
    // ========================================================================
    // Constants validation
    // ========================================================================
    TEST_CLASS(ConstantsTests)
    {
    public:

        TEST_METHOD(PowerToyKey_IsFileLocksmith)
        {
            Assert::AreEqual(L"File Locksmith", constants::nonlocalizable::PowerToyKey);
        }

        TEST_METHOD(PowerToyName_IsFileLocksmith)
        {
            Assert::AreEqual(L"File Locksmith", constants::nonlocalizable::PowerToyName);
        }

        TEST_METHOD(JsonKeyEnabled_IsEnabled)
        {
            Assert::AreEqual(L"Enabled", constants::nonlocalizable::JsonKeyEnabled);
        }

        TEST_METHOD(JsonKeyExtendedMenu_IsCorrect)
        {
            Assert::AreEqual(L"showInExtendedContextMenu",
                             constants::nonlocalizable::JsonKeyShowInExtendedContextMenu);
        }

        TEST_METHOD(DataFilePath_ContainsJson)
        {
            std::wstring path(constants::nonlocalizable::DataFilePath);
            Assert::IsTrue(path.find(L".json") != std::wstring::npos,
                           L"Data file path should end in .json");
        }

        TEST_METHOD(LastRunPath_ContainsLog)
        {
            std::wstring path(constants::nonlocalizable::LastRunPath);
            Assert::IsTrue(path.find(L".log") != std::wstring::npos,
                           L"Last run path should end in .log");
        }

        TEST_METHOD(UIExe_ContainsPowerToys)
        {
            std::wstring exe(constants::nonlocalizable::FileNameUIExe);
            Assert::IsTrue(exe.find(L"PowerToys") != std::wstring::npos,
                           L"UI executable should contain PowerToys in name");
        }

        TEST_METHOD(RegistryKeyDescription_NotEmpty)
        {
            std::wstring desc(constants::nonlocalizable::RegistryKeyDescription);
            Assert::IsFalse(desc.empty());
        }
    };

    // ========================================================================
    // Path normalization
    // ========================================================================
    TEST_CLASS(PathNormalizationTests)
    {
    public:

        TEST_METHOD(ForwardSlash_ConvertedToBackslash)
        {
            auto result = PathUtils::NormalizeSeparators(L"C:/Users/test/file.txt");
            Assert::AreEqual(std::wstring(L"C:\\Users\\test\\file.txt"), result);
        }

        TEST_METHOD(MixedSlashes_AllNormalized)
        {
            auto result = PathUtils::NormalizeSeparators(L"C:\\Users/test\\sub/file.txt");
            Assert::AreEqual(std::wstring(L"C:\\Users\\test\\sub\\file.txt"), result);
        }

        TEST_METHOD(NoSlashes_Unchanged)
        {
            auto result = PathUtils::NormalizeSeparators(L"filename.txt");
            Assert::AreEqual(std::wstring(L"filename.txt"), result);
        }

        TEST_METHOD(TrailingSlash_Removed)
        {
            auto result = PathUtils::RemoveTrailingSlash(L"C:\\Users\\test\\");
            Assert::AreEqual(std::wstring(L"C:\\Users\\test"), result);
        }

        TEST_METHOD(RootPath_TrailingSlashKept)
        {
            // Root path (C:\) should keep its trailing slash
            auto result = PathUtils::RemoveTrailingSlash(L"C:\\");
            Assert::AreEqual(std::wstring(L"C:\\"), result);
        }

        TEST_METHOD(NoTrailingSlash_Unchanged)
        {
            auto result = PathUtils::RemoveTrailingSlash(L"C:\\Users\\test");
            Assert::AreEqual(std::wstring(L"C:\\Users\\test"), result);
        }

        TEST_METHOD(UNCPath_SlashNormalized)
        {
            auto result = PathUtils::NormalizeSeparators(L"//server/share/file.txt");
            Assert::AreEqual(std::wstring(L"\\\\server\\share\\file.txt"), result);
        }

        TEST_METHOD(MultipleSeparators_EachConverted)
        {
            auto result = PathUtils::NormalizeSeparators(L"C:/Users//test///file.txt");
            // Each forward slash becomes a backslash (consecutive slashes preserved)
            Assert::AreEqual(std::wstring(L"C:\\Users\\\\test\\\\\\file.txt"), result);
        }

        TEST_METHOD(PathWithSpaces_Preserved)
        {
            auto result = PathUtils::NormalizeSeparators(L"C:/Program Files/My App/file.txt");
            Assert::AreEqual(std::wstring(L"C:\\Program Files\\My App\\file.txt"), result);
        }
    };

    // ========================================================================
    // Case-insensitive path comparison
    // ========================================================================
    TEST_CLASS(PathComparisonTests)
    {
    public:

        TEST_METHOD(SamePath_Equal)
        {
            Assert::IsTrue(PathUtils::PathsEqual(L"C:\\test\\file.txt", L"C:\\test\\file.txt"));
        }

        TEST_METHOD(DifferentCase_Equal)
        {
            Assert::IsTrue(PathUtils::PathsEqual(L"C:\\Test\\File.TXT", L"c:\\test\\file.txt"));
        }

        TEST_METHOD(DifferentPaths_NotEqual)
        {
            Assert::IsFalse(PathUtils::PathsEqual(L"C:\\test\\file1.txt", L"C:\\test\\file2.txt"));
        }

        TEST_METHOD(DifferentLengths_NotEqual)
        {
            Assert::IsFalse(PathUtils::PathsEqual(L"C:\\test", L"C:\\test\\sub"));
        }

        TEST_METHOD(EmptyPaths_Equal)
        {
            Assert::IsTrue(PathUtils::PathsEqual(L"", L""));
        }

        TEST_METHOD(UNCPaths_CaseInsensitive)
        {
            Assert::IsTrue(PathUtils::PathsEqual(
                L"\\\\Server\\Share\\File.txt",
                L"\\\\server\\share\\file.txt"));
        }

        TEST_METHOD(PathsWithSpaces_ComparedExactly)
        {
            Assert::IsTrue(PathUtils::PathsEqual(
                L"C:\\Program Files\\App\\data.db",
                L"c:\\program files\\app\\data.db"));
        }
    };

    // ========================================================================
    // ProcessResult structure
    // ========================================================================
    TEST_CLASS(ProcessResultTests)
    {
    public:

        TEST_METHOD(FieldAssignment)
        {
            ProcessResult pr;
            pr.name = L"test.exe";
            pr.pid = 1234;
            pr.user = L"SYSTEM";
            pr.files = { L"C:\\file1.txt", L"C:\\file2.txt" };

            Assert::AreEqual(std::wstring(L"test.exe"), pr.name);
            Assert::AreEqual((DWORD)1234, pr.pid);
            Assert::AreEqual(std::wstring(L"SYSTEM"), pr.user);
            Assert::AreEqual((size_t)2, pr.files.size());
        }

        TEST_METHOD(EmptyFiles)
        {
            ProcessResult pr;
            pr.name = L"proc.exe";
            pr.pid = 0;
            pr.user = L"";
            Assert::IsTrue(pr.files.empty());
        }

        TEST_METHOD(MultiplePaths)
        {
            ProcessResult pr;
            pr.files = { L"A", L"B", L"C", L"D" };
            Assert::AreEqual((size_t)4, pr.files.size());
            Assert::AreEqual(std::wstring(L"C"), pr.files[2]);
        }
    };

    // ========================================================================
    // File name extraction
    // ========================================================================
    TEST_CLASS(FileNameExtractionTests)
    {
    public:

        TEST_METHOD(FullPath_ExtractsFileName)
        {
            auto name = PathUtils::GetFileName(L"C:\\Windows\\System32\\notepad.exe");
            Assert::AreEqual(std::wstring(L"notepad.exe"), name);
        }

        TEST_METHOD(JustFileName_ReturnsItself)
        {
            auto name = PathUtils::GetFileName(L"notepad.exe");
            Assert::AreEqual(std::wstring(L"notepad.exe"), name);
        }

        TEST_METHOD(ForwardSlashPath_ExtractsFileName)
        {
            auto name = PathUtils::GetFileName(L"C:/Windows/notepad.exe");
            Assert::AreEqual(std::wstring(L"notepad.exe"), name);
        }

        TEST_METHOD(EmptyPath_ReturnsEmpty)
        {
            auto name = PathUtils::GetFileName(L"");
            Assert::AreEqual(std::wstring(L""), name);
        }

        TEST_METHOD(TrailingSlash_ReturnsEmpty)
        {
            auto name = PathUtils::GetFileName(L"C:\\Windows\\");
            Assert::AreEqual(std::wstring(L""), name);
        }
    };

    // ========================================================================
    // Output formatting
    // ========================================================================
    TEST_CLASS(OutputFormattingTests)
    {
    public:

        TEST_METHOD(EmptyResults_ShowsNoProcesses)
        {
            std::vector<ProcessResult> empty;
            auto output = OutputFormatting::FormatTextOutput(empty, L"HEADER\n", L"No processes found");
            Assert::AreEqual(std::wstring(L"No processes found"), output);
        }

        TEST_METHOD(SingleResult_ContainsPidAndName)
        {
            std::vector<ProcessResult> results = { { L"notepad.exe", 5678, L"user", { L"file" } } };
            auto output = OutputFormatting::FormatTextOutput(results, L"PID\tUser\tName\n", L"None");
            Assert::IsTrue(output.find(L"5678") != std::wstring::npos);
            Assert::IsTrue(output.find(L"notepad.exe") != std::wstring::npos);
            Assert::IsTrue(output.find(L"user") != std::wstring::npos);
        }

        TEST_METHOD(MultipleResults_AllPresent)
        {
            std::vector<ProcessResult> results = {
                { L"proc1.exe", 100, L"user1", {} },
                { L"proc2.exe", 200, L"user2", {} },
                { L"proc3.exe", 300, L"user3", {} },
            };
            auto output = OutputFormatting::FormatTextOutput(results, L"", L"None");
            Assert::IsTrue(output.find(L"100") != std::wstring::npos);
            Assert::IsTrue(output.find(L"200") != std::wstring::npos);
            Assert::IsTrue(output.find(L"300") != std::wstring::npos);
        }

        TEST_METHOD(Output_ContainsTabSeparators)
        {
            std::vector<ProcessResult> results = { { L"test.exe", 42, L"admin", {} } };
            auto output = OutputFormatting::FormatTextOutput(results, L"", L"");
            Assert::IsTrue(output.find(L"\t") != std::wstring::npos);
        }
    };

    // ========================================================================
    // Current process name extraction (integration-style)
    // ========================================================================
    TEST_CLASS(CurrentProcessTests)
    {
    public:

        TEST_METHOD(GetCurrentProcessId_NonZero)
        {
            DWORD pid = ::GetCurrentProcessId();
            Assert::IsTrue(pid > 0, L"Current PID should be non-zero");
        }

        TEST_METHOD(CurrentProcessPath_ContainsExe)
        {
            wchar_t path[MAX_PATH] = {};
            DWORD len = GetModuleFileNameW(NULL, path, MAX_PATH);
            Assert::IsTrue(len > 0, L"Should get module file name");
            std::wstring pathStr(path);
            Assert::IsTrue(pathStr.find(L".exe") != std::wstring::npos ||
                           pathStr.find(L".dll") != std::wstring::npos,
                           L"Process path should contain executable extension");
        }
    };
}
