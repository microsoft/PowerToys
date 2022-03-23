#include "pch.h"
#include "FileTypeUtils.h"

namespace FileUtils
{
    bool IsViewable(String const& filepath)
    {
        LPCWSTR path = filepath.c_str();
        DWORD attrib = GetFileAttributes(path);
        if (attrib & FILE_ATTRIBUTE_HIDDEN && attrib & FILE_ATTRIBUTE_SYSTEM)
        {
            return false;
        }

        return true;
    }

    bool IsMedia(std::wstring const& extension)
    {
        return IsImage(extension) || IsVideo(extension);
    }

    bool IsImage(std::wstring const& extension)
    {
        return FileUtilsInternal::IsPerceivedType(extension, PERCEIVED_TYPE_IMAGE);
    }

    bool IsVideo(std::wstring const& extension)
    {
        return FileUtilsInternal::IsPerceivedType(extension, PERCEIVED_TYPE_VIDEO);
    }

    bool IsDocument(std::wstring const& extension)
    {
        return FileUtilsInternal::IsPerceivedType(extension, PERCEIVED_TYPE_DOCUMENT);
    }

    namespace FileUtilsInternal
    {
        bool IsPerceivedType(std::wstring const& extension, tagPERCEIVED perceivedType)
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
    }
}
