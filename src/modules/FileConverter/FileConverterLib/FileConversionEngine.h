// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#pragma once

#include <string>

namespace file_converter
{
    enum class ImageFormat
    {
        Png,
        Jpeg,
        Bmp,
        Tiff,
        Heif,
        Webp,
    };

    struct ConversionResult
    {
        HRESULT hr = E_FAIL;
        std::wstring error_message;

        [[nodiscard]] bool succeeded() const
        {
            return SUCCEEDED(hr);
        }
    };

    ConversionResult ConvertImageFile(const std::wstring& input_path, const std::wstring& output_path, ImageFormat format);
    ConversionResult IsOutputFormatSupported(ImageFormat format);
}
