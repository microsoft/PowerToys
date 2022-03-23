#pragma once

#include <shtypes.h>

#include "common.h"

namespace FileUtils
{
    bool IsMedia(std::wstring const& extension);
    bool IsImage(std::wstring const& extension);
    bool IsVideo(std::wstring const& extension);
    bool IsDocument(std::wstring const& extension);

    namespace FileUtilsInternal
    {
        bool IsPerceivedType(std::wstring const& extension, tagPERCEIVED perceivedType);
    }
};