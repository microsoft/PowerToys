#pragma once

#include <shtypes.h>

#include "common.h"

namespace FileUtils
{
    bool IsMedia(String const& extension);
    bool IsImage(String const& extension);
    bool IsVideo(String const& extension);
    bool IsDocument(String const& extension);

    namespace FileUtilsInternal
    {
        bool IsPerceivedType(std::wstring const& extension, tagPERCEIVED perceivedType);
    }
};