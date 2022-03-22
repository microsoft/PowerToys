#include "pch.h"
#include "FileTypeUtils.h"

bool FileTypeUtils::IsMedia(std::wstring const& extension)
{
    return IsImage(extension) || IsVideo(extension);
}

bool FileTypeUtils::IsImage(std::wstring const& extension)
{
    return IsPerceivedType(extension, PERCEIVED_TYPE_IMAGE);
}

bool FileTypeUtils::IsVideo(std::wstring const& extension)
{
    return IsPerceivedType(extension, PERCEIVED_TYPE_VIDEO);
}

bool FileTypeUtils::IsDocument(std::wstring const& extension)
{
    return IsPerceivedType(extension, PERCEIVED_TYPE_DOCUMENT);
}

bool FileTypeUtils::IsPerceivedType(std::wstring const& extension, tagPERCEIVED perceivedType)
{
    PERCEIVED perceived;
    PERCEIVEDFLAG flag;
    PWSTR pszType;
    bool isPerceivedType = false;

    if (SUCCEEDED(AssocGetPerceivedType(extension.c_str(), &perceived, &flag, &pszType)))
    {
        isPerceivedType = perceived == perceivedType;
    }
    return isPerceivedType;

}

