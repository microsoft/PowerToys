#pragma once

#include <projects-common/Data.h>

#include <common/utils/resources.h>
#include "Generated Files/resource.h"

namespace ProjectNameUtils
{
    inline std::wstring CreateProjectName(const std::vector<Project>& projects)
    {
        std::wstring defaultNamePrefix = GET_RESOURCE_STRING(IDS_DEFAULTPROJECTNAMEPREFIX);
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