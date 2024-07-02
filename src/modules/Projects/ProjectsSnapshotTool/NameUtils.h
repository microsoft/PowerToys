#pragma once

#include <string>
#include <vector>

#include <projects-common/Data.h>

namespace ProjectNameUtils
{
    inline std::wstring CreateProjectName(const std::vector<Project>& projects)
    {
        // new project name
        std::wstring defaultNamePrefix = L"Project"; // TODO: localizable
        int nextProjectIndex = 0;
        for (const auto& proj : projects)
        {
            const std::wstring& name = proj.name;
            if (name.starts_with(defaultNamePrefix))
            {
                try
                {
                    int index = std::stoi(name.substr(defaultNamePrefix.length() + 1));
                    if (nextProjectIndex < index)
                    {
                        nextProjectIndex = index;
                    }
                }
                catch (std::exception)
                {
                }
            }
        }

        return defaultNamePrefix + L" " + std::to_wstring(nextProjectIndex + 1);
    }
}