#include "pch.h"
#include <filesystem>
#include <fstream>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace WorkspacesLibUnitTests
{
    TEST_CLASS (JsonUtilsTests)
    {
    private:
        std::wstring CreateTempJsonFile(const std::wstring& content)
        {
            std::wstring tempPath = std::filesystem::temp_directory_path();
            tempPath += L"\\test_workspace_" + std::to_wstring(GetTickCount64()) + L".json";

            std::wofstream file(tempPath);
            file << content;
            file.close();

            return tempPath;
        }

        void DeleteTempFile(const std::wstring& filePath)
        {
            if (std::filesystem::exists(filePath))
            {
                std::filesystem::remove(filePath);
            }
        }

    public:
        TEST_METHOD (ReadSingleWorkspace_NonExistentFile_ReturnsEmptyWorkspace)
        {
            // Arrange
            std::wstring nonExistentFile = L"C:\\NonExistent\\File.json";

            // Act
            auto result = JsonUtils::ReadSingleWorkspace(nonExistentFile);

            // Assert
            Assert::IsTrue(result.isOk());
            auto workspace = result.value();
            Assert::IsTrue(workspace.name.empty());
        }

        TEST_METHOD (ReadSingleWorkspace_InvalidJsonFile_ReturnsError)
        {
            // Arrange
            std::wstring tempFile = CreateTempJsonFile(L"invalid json content {");

            // Act
            auto result = JsonUtils::ReadSingleWorkspace(tempFile);

            // Assert
            Assert::IsTrue(result.isError());
            Assert::AreEqual(static_cast<int>(JsonUtils::WorkspacesFileError::IncorrectFileError),
                             static_cast<int>(result.error()));

            // Cleanup
            DeleteTempFile(tempFile);
        }

        TEST_METHOD (ReadWorkspaces_NonExistentFile_ReturnsEmptyVector)
        {
            // Arrange
            std::wstring nonExistentFile = L"C:\\NonExistent\\File.json";

            // Act
            auto result = JsonUtils::ReadWorkspaces(nonExistentFile);

            // Assert
            Assert::IsTrue(result.isError());
            Assert::AreEqual(static_cast<int>(JsonUtils::WorkspacesFileError::IncorrectFileError),
                             static_cast<int>(result.error()));
        }

        TEST_METHOD (ReadWorkspaces_InvalidJsonFile_ReturnsError)
        {
            // Arrange
            std::wstring tempFile = CreateTempJsonFile(L"invalid json content {");

            // Act
            auto result = JsonUtils::ReadWorkspaces(tempFile);

            // Assert
            Assert::IsTrue(result.isError());
            Assert::AreEqual(static_cast<int>(JsonUtils::WorkspacesFileError::IncorrectFileError),
                             static_cast<int>(result.error()));

            // Cleanup
            DeleteTempFile(tempFile);
        }

        TEST_METHOD (Write_ValidWorkspace_ReturnsTrue)
        {
            // Arrange
            std::wstring tempPath = std::filesystem::temp_directory_path();
            tempPath += L"\\test_write_workspace_" + std::to_wstring(GetTickCount64()) + L".json";

            WorkspacesData::WorkspacesProject workspace;
            workspace.name = L"Test Workspace";

            // Convert string to time_t
            std::tm tm = {};
            workspace.creationTime = std::mktime(&tm);

            // Act
            bool result = JsonUtils::Write(tempPath, workspace);

            // Assert
            Assert::IsTrue(result);
            Assert::IsTrue(std::filesystem::exists(tempPath));

            // Cleanup
            DeleteTempFile(tempPath);
        }

        TEST_METHOD (Write_ValidWorkspacesList_ReturnsTrue)
        {
            // Arrange
            std::wstring tempPath = std::filesystem::temp_directory_path();
            tempPath += L"\\test_write_workspaces_" + std::to_wstring(GetTickCount64()) + L".json";

            std::vector<WorkspacesData::WorkspacesProject> workspaces;

            WorkspacesData::WorkspacesProject workspace1;
            workspace1.name = L"Test Workspace 1";
            workspace1.creationTime = std::time(nullptr);

            WorkspacesData::WorkspacesProject workspace2;
            workspace2.name = L"Test Workspace 2";
            workspace2.creationTime = std::time(nullptr);

            workspaces.push_back(workspace1);
            workspaces.push_back(workspace2);

            // Act
            bool result = JsonUtils::Write(tempPath, workspaces);

            // Assert
            Assert::IsTrue(result);
            Assert::IsTrue(std::filesystem::exists(tempPath));

            // Cleanup
            DeleteTempFile(tempPath);
        }

        TEST_METHOD (Write_EmptyWorkspacesList_ReturnsTrue)
        {
            // Arrange
            std::wstring tempPath = std::filesystem::temp_directory_path();
            tempPath += L"\\test_write_empty_" + std::to_wstring(GetTickCount64()) + L".json";

            std::vector<WorkspacesData::WorkspacesProject> emptyWorkspaces;

            // Act
            bool result = JsonUtils::Write(tempPath, emptyWorkspaces);

            // Assert
            Assert::IsTrue(result);
            Assert::IsTrue(std::filesystem::exists(tempPath));

            // Cleanup
            DeleteTempFile(tempPath);
        }

        /*
        TEST_METHOD(Write_InvalidPath_ReturnsFalse)
        {
            // Arrange
            std::wstring invalidPath = L"C:\\NonExistent\\Path\\workspace.json";
            
            WorkspacesData::WorkspacesProject workspace;
            workspace.name = L"Test Workspace";
            workspace.creationTime = std::time(nullptr);

            // Act
            bool result = JsonUtils::Write(invalidPath, workspace);

            // Assert
            Assert::IsFalse(result);
        }
        */
    };
}