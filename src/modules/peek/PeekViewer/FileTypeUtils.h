#pragma once

class FileTypeUtils
{
public:
    static bool IsMedia(std::wstring const& extension);
    static bool IsImage(std::wstring const& extension);
    static bool IsVideo(std::wstring const& extension);
    static bool IsDocument(std::wstring const& extension);

private:
    static bool IsPerceivedType(std::wstring const& extension, tagPERCEIVED perceivedType);
};