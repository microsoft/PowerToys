// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#include "pch.h"

#include "FileConversionEngine.h"

#include <winrt/Windows.ApplicationModel.Resources.h>
#include <wrl/client.h>

#include <sstream>

namespace
{
    std::wstring LoadLocalizedString(std::wstring_view key, std::wstring_view fallback)
    {
        try
        {
            static const auto loader = winrt::Windows::ApplicationModel::Resources::ResourceLoader::GetForViewIndependentUse(L"Resources");
            const auto value = loader.GetString(winrt::hstring{ key });
            if (!value.empty())
            {
                return value.c_str();
            }
        }
        catch (...)
        {
        }

        return std::wstring{ fallback };
    }

    GUID ContainerFormatFor(file_converter::ImageFormat format)
    {
        switch (format)
        {
        case file_converter::ImageFormat::Jpeg:
            return GUID_ContainerFormatJpeg;
        case file_converter::ImageFormat::Bmp:
            return GUID_ContainerFormatBmp;
        case file_converter::ImageFormat::Tiff:
            return GUID_ContainerFormatTiff;
        case file_converter::ImageFormat::Heif:
            return GUID_ContainerFormatHeif;
        case file_converter::ImageFormat::Webp:
            return GUID_ContainerFormatWebp;
        case file_converter::ImageFormat::Png:
        default:
            return GUID_ContainerFormatPng;
        }
    }

    const wchar_t* ExtensionFor(file_converter::ImageFormat format)
    {
        switch (format)
        {
        case file_converter::ImageFormat::Jpeg:
            return L".jpg";
        case file_converter::ImageFormat::Bmp:
            return L".bmp";
        case file_converter::ImageFormat::Tiff:
            return L".tiff";
        case file_converter::ImageFormat::Heif:
            return L".heic";
        case file_converter::ImageFormat::Webp:
            return L".webp";
        case file_converter::ImageFormat::Png:
        default:
            return L".png";
        }
    }

    constexpr bool IsMissingCodecHresult(HRESULT hr) noexcept
    {
        return hr == WINCODEC_ERR_COMPONENTNOTFOUND ||
               hr == HRESULT_FROM_WIN32(ERROR_NOT_SUPPORTED);
    }

    std::wstring HrMessage(std::wstring_view prefix, HRESULT hr)
    {
        std::wstringstream stream;
        stream << prefix << L" HRESULT=0x" << std::hex << std::uppercase << static_cast<unsigned long>(hr);
        return stream.str();
    }

    struct ScopedCom
    {
        HRESULT hr;
        bool uninitialize;

        ScopedCom()
            : hr(E_FAIL), uninitialize(false)
        {
            // Prefer MTA, but gracefully handle callers that already initialized
            // COM in a different apartment (e.g. Explorer STA threads).
            hr = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
            if (hr == RPC_E_CHANGED_MODE)
            {
                hr = S_OK;
                return;
            }

            if (SUCCEEDED(hr))
            {
                uninitialize = true;
            }
        }

        ~ScopedCom()
        {
            if (uninitialize)
            {
                CoUninitialize();
            }
        }
    };

    HRESULT CreateWicFactory(Microsoft::WRL::ComPtr<IWICImagingFactory>& factory)
    {
        HRESULT hr = CoCreateInstance(CLSID_WICImagingFactory2, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&factory));
        if (FAILED(hr))
        {
            hr = CoCreateInstance(CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&factory));
        }

        return hr;
    }

    file_converter::ConversionResult EnsureOutputEncoderAvailable(IWICImagingFactory* factory, file_converter::ImageFormat format)
    {
        if (factory == nullptr)
        {
            return { E_POINTER, LoadLocalizedString(L"FileConverter_Engine_WicFactoryNull", L"WIC factory is null.") };
        }

        Microsoft::WRL::ComPtr<IWICBitmapEncoder> encoder_probe;
        const HRESULT hr = factory->CreateEncoder(ContainerFormatFor(format), nullptr, &encoder_probe);
        if (FAILED(hr))
        {
            if (IsMissingCodecHresult(hr))
            {
                const std::wstring error = LoadLocalizedString(L"FileConverter_Engine_NoEncoderInstalled", L"No WIC encoder is installed for destination format '") + ExtensionFor(format) + L"'.";
                return { HRESULT_FROM_WIN32(ERROR_NOT_SUPPORTED), error };
            }

            return { hr, HrMessage(LoadLocalizedString(L"FileConverter_Engine_CreateEncoderFailed", L"Failed creating image encoder."), hr) };
        }

        return { S_OK, L"" };
    }
}

namespace file_converter
{
    ConversionResult IsOutputFormatSupported(ImageFormat format)
    {
        ScopedCom com;
        if (FAILED(com.hr))
        {
            return { com.hr, HrMessage(LoadLocalizedString(L"FileConverter_Engine_CoInitializeFailed", L"CoInitializeEx failed."), com.hr) };
        }

        Microsoft::WRL::ComPtr<IWICImagingFactory> factory;
        const HRESULT hr = CreateWicFactory(factory);
        if (FAILED(hr))
        {
            return { hr, HrMessage(LoadLocalizedString(L"FileConverter_Engine_CreateWicFactoryFailed", L"Failed creating WIC factory."), hr) };
        }

        return EnsureOutputEncoderAvailable(factory.Get(), format);
    }

    ConversionResult ConvertImageFile(const std::wstring& input_path, const std::wstring& output_path, ImageFormat format)
    {
        ScopedCom com;
        if (FAILED(com.hr))
        {
            return { com.hr, HrMessage(LoadLocalizedString(L"FileConverter_Engine_CoInitializeFailed", L"CoInitializeEx failed."), com.hr) };
        }

        Microsoft::WRL::ComPtr<IWICImagingFactory> factory;
        HRESULT hr = CreateWicFactory(factory);

        if (FAILED(hr))
        {
            return { hr, HrMessage(LoadLocalizedString(L"FileConverter_Engine_CreateWicFactoryFailed", L"Failed creating WIC factory."), hr) };
        }

        const auto output_support = EnsureOutputEncoderAvailable(factory.Get(), format);
        if (FAILED(output_support.hr))
        {
            return output_support;
        }

        Microsoft::WRL::ComPtr<IWICBitmapDecoder> decoder;
        hr = factory->CreateDecoderFromFilename(input_path.c_str(), nullptr, GENERIC_READ, WICDecodeMetadataCacheOnLoad, &decoder);
        if (FAILED(hr))
        {
            if (hr == WINCODEC_ERR_UNKNOWNIMAGEFORMAT || IsMissingCodecHresult(hr))
            {
                return { hr, LoadLocalizedString(L"FileConverter_Engine_InputUnsupported", L"Input image format is not supported by installed WIC decoders.") };
            }

            return { hr, HrMessage(LoadLocalizedString(L"FileConverter_Engine_OpenInputFailed", L"Failed opening input image."), hr) };
        }

        Microsoft::WRL::ComPtr<IWICBitmapFrameDecode> source_frame;
        hr = decoder->GetFrame(0, &source_frame);
        if (FAILED(hr))
        {
            return { hr, HrMessage(LoadLocalizedString(L"FileConverter_Engine_ReadFirstFrameFailed", L"Failed reading first image frame."), hr) };
        }

        UINT width = 0;
        UINT height = 0;
        hr = source_frame->GetSize(&width, &height);
        if (FAILED(hr))
        {
            return { hr, HrMessage(LoadLocalizedString(L"FileConverter_Engine_ReadImageSizeFailed", L"Failed reading image size."), hr) };
        }

        WICPixelFormatGUID pixel_format = {};
        hr = source_frame->GetPixelFormat(&pixel_format);
        if (FAILED(hr))
        {
            return { hr, HrMessage(LoadLocalizedString(L"FileConverter_Engine_ReadPixelFormatFailed", L"Failed reading source pixel format."), hr) };
        }

        Microsoft::WRL::ComPtr<IWICStream> output_stream;
        hr = factory->CreateStream(&output_stream);
        if (FAILED(hr))
        {
            return { hr, HrMessage(LoadLocalizedString(L"FileConverter_Engine_CreateStreamFailed", L"Failed creating WIC stream."), hr) };
        }

        hr = output_stream->InitializeFromFilename(output_path.c_str(), GENERIC_WRITE);
        if (FAILED(hr))
        {
            return { hr, HrMessage(LoadLocalizedString(L"FileConverter_Engine_OpenOutputFailed", L"Failed opening output path."), hr) };
        }

        Microsoft::WRL::ComPtr<IWICBitmapEncoder> encoder;
        hr = factory->CreateEncoder(ContainerFormatFor(format), nullptr, &encoder);
        if (FAILED(hr))
        {
            return { hr, HrMessage(LoadLocalizedString(L"FileConverter_Engine_CreateEncoderFailed", L"Failed creating image encoder."), hr) };
        }

        hr = encoder->Initialize(output_stream.Get(), WICBitmapEncoderNoCache);
        if (FAILED(hr))
        {
            return { hr, HrMessage(LoadLocalizedString(L"FileConverter_Engine_InitEncoderFailed", L"Failed initializing encoder."), hr) };
        }

        Microsoft::WRL::ComPtr<IWICBitmapFrameEncode> target_frame;
        Microsoft::WRL::ComPtr<IPropertyBag2> frame_properties;
        hr = encoder->CreateNewFrame(&target_frame, &frame_properties);
        if (FAILED(hr))
        {
            return { hr, HrMessage(LoadLocalizedString(L"FileConverter_Engine_CreateTargetFrameFailed", L"Failed creating target frame."), hr) };
        }

        hr = target_frame->Initialize(frame_properties.Get());
        if (FAILED(hr))
        {
            return { hr, HrMessage(LoadLocalizedString(L"FileConverter_Engine_InitTargetFrameFailed", L"Failed initializing target frame."), hr) };
        }

        hr = target_frame->SetSize(width, height);
        if (FAILED(hr))
        {
            return { hr, HrMessage(LoadLocalizedString(L"FileConverter_Engine_SetTargetSizeFailed", L"Failed setting target size."), hr) };
        }

        WICPixelFormatGUID target_pixel_format = pixel_format;
        hr = target_frame->SetPixelFormat(&target_pixel_format);
        if (FAILED(hr))
        {
            return { hr, HrMessage(LoadLocalizedString(L"FileConverter_Engine_SetTargetPixelFormatFailed", L"Failed setting target pixel format."), hr) };
        }

        Microsoft::WRL::ComPtr<IWICBitmapSource> source_for_write = source_frame;
        Microsoft::WRL::ComPtr<IWICFormatConverter> format_converter;

        if (!InlineIsEqualGUID(pixel_format, target_pixel_format))
        {
            hr = factory->CreateFormatConverter(&format_converter);
            if (FAILED(hr))
            {
                return { hr, HrMessage(LoadLocalizedString(L"FileConverter_Engine_CreateFormatConverterFailed", L"Failed creating format converter."), hr) };
            }

            BOOL can_convert = FALSE;
            hr = format_converter->CanConvert(pixel_format, target_pixel_format, &can_convert);
            if (FAILED(hr) || !can_convert)
            {
                return { hr, HrMessage(LoadLocalizedString(L"FileConverter_Engine_UnsupportedPixelConversion", L"Source pixel format cannot be converted to target pixel format."), FAILED(hr) ? hr : E_FAIL) };
            }

            hr = format_converter->Initialize(source_frame.Get(), target_pixel_format, WICBitmapDitherTypeNone, nullptr, 0.0f, WICBitmapPaletteTypeCustom);
            if (FAILED(hr))
            {
                return { hr, HrMessage(LoadLocalizedString(L"FileConverter_Engine_InitFormatConverterFailed", L"Failed initializing format converter."), hr) };
            }

            source_for_write = format_converter;
        }

        hr = target_frame->WriteSource(source_for_write.Get(), nullptr);
        if (FAILED(hr))
        {
            return { hr, HrMessage(LoadLocalizedString(L"FileConverter_Engine_WriteTargetFrameFailed", L"Failed writing target frame."), hr) };
        }

        hr = target_frame->Commit();
        if (FAILED(hr))
        {
            return { hr, HrMessage(LoadLocalizedString(L"FileConverter_Engine_CommitTargetFrameFailed", L"Failed committing target frame."), hr) };
        }

        hr = encoder->Commit();
        if (FAILED(hr))
        {
            return { hr, HrMessage(LoadLocalizedString(L"FileConverter_Engine_CommitEncoderFailed", L"Failed committing encoder."), hr) };
        }

        return { S_OK, L"" };
    }
}
