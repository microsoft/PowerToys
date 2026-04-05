// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#pragma once

namespace winrt::PowerToys::FileConverter::Constants
{
    inline constexpr wchar_t PipeNamePrefix[] = L"\\\\.\\pipe\\powertoys_fileconverter_";

    inline constexpr wchar_t ActionFormatConvert[] = L"FormatConvert";

    inline constexpr wchar_t JsonActionKey[] = L"action";
    inline constexpr wchar_t JsonDestinationKey[] = L"destination";
    inline constexpr wchar_t JsonFilesKey[] = L"files";

    inline constexpr wchar_t FormatPng[] = L"png";
    inline constexpr wchar_t FormatJpg[] = L"jpg";
    inline constexpr wchar_t FormatJpeg[] = L"jpeg";
    inline constexpr wchar_t FormatBmp[] = L"bmp";
    inline constexpr wchar_t FormatTif[] = L"tif";
    inline constexpr wchar_t FormatTiff[] = L"tiff";
    inline constexpr wchar_t FormatHeic[] = L"heic";
    inline constexpr wchar_t FormatHeif[] = L"heif";
    inline constexpr wchar_t FormatWebp[] = L"webp";

    inline constexpr wchar_t ExtensionPng[] = L".png";
    inline constexpr wchar_t ExtensionJpg[] = L".jpg";
    inline constexpr wchar_t ExtensionJpeg[] = L".jpeg";
    inline constexpr wchar_t ExtensionBmp[] = L".bmp";
    inline constexpr wchar_t ExtensionTif[] = L".tif";
    inline constexpr wchar_t ExtensionTiff[] = L".tiff";
    inline constexpr wchar_t ExtensionHeic[] = L".heic";
    inline constexpr wchar_t ExtensionHeif[] = L".heif";
    inline constexpr wchar_t ExtensionWebp[] = L".webp";
}
