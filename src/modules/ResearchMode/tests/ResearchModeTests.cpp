#include "research_mode.h"
#include <cassert>
#include <filesystem>
#include <string>

int main()
{
    // Use a temporary folder within current directory
    std::wstring folder = L"research_test_output";
    std::filesystem::create_directory(folder);
    ResearchMode::SetResearchFolder(folder);

    // Append sample content
    ResearchMode::AppendToLog(L"Test entry");

    // Check that at least one file exists in the output folder
    bool hasFile = false;
    for (const auto& entry : std::filesystem::directory_iterator(folder))
    {
        hasFile = true;
        break;
    }

    assert(hasFile && "Expected log file to be created");
    return 0;
}
