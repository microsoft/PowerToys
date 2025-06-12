#include "pch.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace WorkspacesLibUnitTests
{
    TEST_CLASS(WorkspacesDataTests)
    {
    public:
        TEST_METHOD(WorkspacesFile_ReturnsValidPath)
        {
            // Act
            std::wstring result = WorkspacesData::WorkspacesFile();

            // Assert
            Assert::IsFalse(result.empty());
            Assert::IsTrue(result.find(L"workspaces.json") != std::wstring::npos);
        }

        TEST_METHOD(TempWorkspacesFile_ReturnsValidPath)
        {
            // Act
            std::wstring result = WorkspacesData::TempWorkspacesFile();

            // Assert
            Assert::IsFalse(result.empty());
            Assert::IsTrue(result.find(L"temp-workspaces.json") != std::wstring::npos);
        }

        TEST_METHOD(WorkspacesFile_TempWorkspacesFile_DifferentPaths)
        {
            // Act
            std::wstring workspacesFile = WorkspacesData::WorkspacesFile();
            std::wstring tempWorkspacesFile = WorkspacesData::TempWorkspacesFile();

            // Assert
            Assert::AreNotEqual(workspacesFile, tempWorkspacesFile);
        }

        TEST_METHOD(Position_ToRect_ConvertsCorrectly)
        {
            // Arrange
            WorkspacesData::WorkspacesProject::Application::Position position;
            position.x = 100;
            position.y = 200;
            position.width = 800;
            position.height = 600;

            // Act
            RECT rect = position.toRect();

            // Assert
            Assert::AreEqual(100, static_cast<int>(rect.left));
            Assert::AreEqual(200, static_cast<int>(rect.top));
            Assert::AreEqual(900, static_cast<int>(rect.right)); // x + width
            Assert::AreEqual(800, static_cast<int>(rect.bottom)); // y + height
        }

        TEST_METHOD(Position_ToRect_ZeroPosition)
        {
            // Arrange
            WorkspacesData::WorkspacesProject::Application::Position position;
            position.x = 0;
            position.y = 0;
            position.width = 0;
            position.height = 0;

            // Act
            RECT rect = position.toRect();

            // Assert
            Assert::AreEqual(0, static_cast<int>(rect.left));
            Assert::AreEqual(0, static_cast<int>(rect.top));
            Assert::AreEqual(0, static_cast<int>(rect.right));
            Assert::AreEqual(0, static_cast<int>(rect.bottom));
        }

        TEST_METHOD(Position_ToRect_NegativeCoordinates)
        {
            // Arrange
            WorkspacesData::WorkspacesProject::Application::Position position;
            position.x = -100;
            position.y = -50;
            position.width = 200;
            position.height = 150;

            // Act
            RECT rect = position.toRect();

            // Assert
            Assert::AreEqual(-100, static_cast<int>(rect.left));
            Assert::AreEqual(-50, static_cast<int>(rect.top));
            Assert::AreEqual(100, static_cast<int>(rect.right)); // -100 + 200
            Assert::AreEqual(100, static_cast<int>(rect.bottom)); // -50 + 150
        }

        TEST_METHOD(Application_DefaultValues)
        {
            // Arrange & Act
            WorkspacesData::WorkspacesProject::Application app;

            // Assert
            Assert::IsTrue(app.id.empty());
            Assert::IsTrue(app.name.empty());
            Assert::IsTrue(app.title.empty());
            Assert::IsTrue(app.path.empty());
            Assert::IsTrue(app.packageFullName.empty());
            Assert::IsTrue(app.appUserModelId.empty());
            Assert::IsTrue(app.pwaAppId.empty());
            Assert::IsTrue(app.commandLineArgs.empty());
            Assert::IsFalse(app.isElevated);
            Assert::IsFalse(app.canLaunchElevated);
            Assert::IsFalse(app.isMinimized);
            Assert::IsFalse(app.isMaximized);
            Assert::AreEqual(0, static_cast<int>(app.position.x));
            Assert::AreEqual(0, static_cast<int>(app.position.y));
            Assert::AreEqual(0, static_cast<int>(app.position.width));
            Assert::AreEqual(0, static_cast<int>(app.position.height));
            Assert::AreEqual(0u, static_cast<unsigned int>(app.monitor));
        }

        TEST_METHOD(Application_Comparison_EqualObjects)
        {
            // Arrange
            WorkspacesData::WorkspacesProject::Application app1;
            app1.id = L"test-id";
            app1.name = L"Test App";
            app1.position.x = 100;
            app1.position.y = 200;

            WorkspacesData::WorkspacesProject::Application app2;
            app2.id = L"test-id";
            app2.name = L"Test App";
            app2.position.x = 100;
            app2.position.y = 200;

            // Act & Assert
            Assert::IsTrue(app1 == app2);
        }

        TEST_METHOD(Application_Comparison_DifferentObjects)
        {
            // Arrange
            WorkspacesData::WorkspacesProject::Application app1;
            app1.id = L"test-id-1";
            app1.name = L"Test App 1";

            WorkspacesData::WorkspacesProject::Application app2;
            app2.id = L"test-id-2";
            app2.name = L"Test App 2";

            // Act & Assert
            Assert::IsTrue(app1 != app2);
        }

        TEST_METHOD(Position_Comparison_EqualPositions)
        {
            // Arrange
            WorkspacesData::WorkspacesProject::Application::Position pos1;
            pos1.x = 100;
            pos1.y = 200;
            pos1.width = 800;
            pos1.height = 600;

            WorkspacesData::WorkspacesProject::Application::Position pos2;
            pos2.x = 100;
            pos2.y = 200;
            pos2.width = 800;
            pos2.height = 600;

            // Act & Assert
            Assert::IsTrue(pos1 == pos2);
        }

        TEST_METHOD(Position_Comparison_DifferentPositions)
        {
            // Arrange
            WorkspacesData::WorkspacesProject::Application::Position pos1;
            pos1.x = 100;
            pos1.y = 200;
            pos1.width = 800;
            pos1.height = 600;

            WorkspacesData::WorkspacesProject::Application::Position pos2;
            pos2.x = 150;
            pos2.y = 200;
            pos2.width = 800;
            pos2.height = 600;

            // Act & Assert
            Assert::IsTrue(pos1 != pos2);
        }
    };
}