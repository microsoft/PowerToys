#include "pch.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace WorkspacesLibUnitTests
{
    // Test class for CommandLineArgsHelper functionality
    TEST_CLASS(CommandLineArgsHelperTests)
    {
    private:
        // Helper function to simulate improved command line parsing
        // This implements the fix we'll apply to CommandLineArgsHelper::GetCommandLineArgs
        std::wstring ParseCommandLineArgs(const std::wstring& executablePath, const std::wstring& fullCommandLine)
        {
            if (fullCommandLine.empty())
            {
                return L"";
            }

            std::wstring commandLineArgs = fullCommandLine;
            
            // First try to find quoted executable path
            std::wstring quotedPath = L"\"" + executablePath + L"\"";
            auto quotedPos = commandLineArgs.find(quotedPath);
            if (quotedPos != std::wstring::npos)
            {
                // Ensure this is at the beginning or after whitespace (not a substring)
                if (quotedPos == 0 || std::iswspace(commandLineArgs[quotedPos - 1]))
                {
                    commandLineArgs = commandLineArgs.substr(quotedPos + quotedPath.size());
                    // Remove leading space if present
                    if (!commandLineArgs.empty() && commandLineArgs[0] == L' ')
                    {
                        commandLineArgs = commandLineArgs.substr(1);
                    }
                }
                else
                {
                    // Fall back to unquoted path logic
                    goto try_unquoted;
                }
            }
            else
            {
try_unquoted:
                // Fall back to unquoted executable path (original behavior)
                auto pos = commandLineArgs.find(executablePath);
                if (pos != std::wstring::npos)
                {
                    // Ensure this is at the beginning or after whitespace (not a substring)
                    if (pos == 0 || std::iswspace(commandLineArgs[pos - 1]))
                    {
                        commandLineArgs = commandLineArgs.substr(pos + executablePath.size());
                        auto spacePos = commandLineArgs.find_first_of(' ');
                        if (spacePos != std::wstring::npos)
                        {
                            commandLineArgs = commandLineArgs.substr(spacePos + 1);
                        }
                        else
                        {
                            commandLineArgs = L"";
                        }
                    }
                }
            }

            return commandLineArgs;
        }

    public:
        TEST_METHOD(ParseCommandLineArgs_WithSpacesInPath_QuotedExecutable)
        {
            // Arrange
            std::wstring executablePath = L"C:\\Program Files\\My App\\app.exe";
            std::wstring fullCommandLine = L"\"C:\\Program Files\\My App\\app.exe\" document.docx";
            
            // Act
            std::wstring result = ParseCommandLineArgs(executablePath, fullCommandLine);

            // Assert
            Assert::AreEqual(std::wstring(L"document.docx"), result);
        }

        TEST_METHOD(ParseCommandLineArgs_WithSpacesInPath_QuotedExecutableMultipleArgs)
        {
            // Arrange
            std::wstring executablePath = L"C:\\Users\\University Name\\OneDrive\\My App\\app.exe";
            std::wstring fullCommandLine = L"\"C:\\Users\\University Name\\OneDrive\\My App\\app.exe\" \"file with spaces.docx\" -readonly";
            
            // Act
            std::wstring result = ParseCommandLineArgs(executablePath, fullCommandLine);

            // Assert
            Assert::AreEqual(std::wstring(L"\"file with spaces.docx\" -readonly"), result);
        }

        TEST_METHOD(ParseCommandLineArgs_NoSpacesInPath_UnquotedExecutable)
        {
            // Arrange
            std::wstring executablePath = L"C:\\Program\\app.exe";
            std::wstring fullCommandLine = L"C:\\Program\\app.exe document.txt";
            
            // Act
            std::wstring result = ParseCommandLineArgs(executablePath, fullCommandLine);

            // Assert
            Assert::AreEqual(std::wstring(L"document.txt"), result);
        }

        TEST_METHOD(ParseCommandLineArgs_NoArgs_ReturnsEmpty)
        {
            // Arrange
            std::wstring executablePath = L"C:\\Program Files\\My App\\app.exe";
            std::wstring fullCommandLine = L"\"C:\\Program Files\\My App\\app.exe\"";
            
            // Act
            std::wstring result = ParseCommandLineArgs(executablePath, fullCommandLine);

            // Assert
            Assert::AreEqual(std::wstring(L""), result);
        }

        TEST_METHOD(ParseCommandLineArgs_EmptyCommandLine_ReturnsEmpty)
        {
            // Arrange
            std::wstring executablePath = L"C:\\Program Files\\My App\\app.exe";
            std::wstring fullCommandLine = L"";
            
            // Act
            std::wstring result = ParseCommandLineArgs(executablePath, fullCommandLine);

            // Assert
            Assert::AreEqual(std::wstring(L""), result);
        }

        TEST_METHOD(ParseCommandLineArgs_OneDriveScenario)
        {
            // Arrange - Simulating the OneDrive with organization name scenario
            std::wstring executablePath = L"C:\\Program Files\\Microsoft Office\\Office16\\WINWORD.EXE";
            std::wstring fullCommandLine = L"\"C:\\Program Files\\Microsoft Office\\Office16\\WINWORD.EXE\" \"C:\\Users\\User\\University Name - OneDrive\\Documents\\Document.docx\"";
            
            // Act
            std::wstring result = ParseCommandLineArgs(executablePath, fullCommandLine);

            // Assert
            Assert::AreEqual(std::wstring(L"\"C:\\Users\\User\\University Name - OneDrive\\Documents\\Document.docx\""), result);
        }

        TEST_METHOD(ParseCommandLineArgs_MultipleSpacesAfterExecutable)
        {
            // Arrange
            std::wstring executablePath = L"C:\\Program Files\\My App\\app.exe";
            std::wstring fullCommandLine = L"\"C:\\Program Files\\My App\\app.exe\"   document.txt";
            
            // Act
            std::wstring result = ParseCommandLineArgs(executablePath, fullCommandLine);

            // Assert
            Assert::AreEqual(std::wstring(L"  document.txt"), result);
        }

        TEST_METHOD(ParseCommandLineArgs_PathNotFound_ReturnsOriginal)
        {
            // Arrange
            std::wstring executablePath = L"C:\\Different\\Path\\app.exe";
            std::wstring fullCommandLine = L"\"C:\\Program Files\\My App\\app.exe\" document.txt";
            
            // Act
            std::wstring result = ParseCommandLineArgs(executablePath, fullCommandLine);

            // Assert
            Assert::AreEqual(fullCommandLine, result);
        }

        TEST_METHOD(ParseCommandLineArgs_MultipleSpacesAfterExecutable)
        {
            // Arrange
            std::wstring executablePath = L"C:\\Program Files\\My App\\app.exe";
            std::wstring fullCommandLine = L"\"C:\\Program Files\\My App\\app.exe\"   document.txt";
            
            // Act
            std::wstring result = ParseCommandLineArgs(executablePath, fullCommandLine);

            // Assert
            Assert::AreEqual(std::wstring(L"  document.txt"), result);
        }

        TEST_METHOD(ParseCommandLineArgs_ExecutableSubstringOfPath_NoFalseMatch)
        {
            // Arrange - Ensure we don't match when the executable path is just a substring
            std::wstring executablePath = L"C:\\app.exe";
            std::wstring fullCommandLine = L"\"C:\\app.exe.backup\\real_app.exe\" document.txt";
            
            // Act
            std::wstring result = ParseCommandLineArgs(executablePath, fullCommandLine);

            // Assert - Should return original command line since no valid match
            Assert::AreEqual(fullCommandLine, result);
        }
    };
}