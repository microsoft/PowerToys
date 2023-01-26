#include <initguid.h>

#include <dxgiformat.h>
#include <assert.h>
#include <winrt/base.h>

#pragma warning(push)
#pragma warning(disable : 4005)
#include <wincodec.h>
#pragma warning(pop)

#include <memory>
#include <mfapi.h>
#include <shcore.h>
#include <algorithm>

#include <wil/resource.h>
#include <wil/com.h>

#include <mfidl.h>
#include <mftransform.h>
#include <dshow.h>
#include <Wincodecsdk.h>

#include <shlwapi.h>

#include "Logging.h"

IWICImagingFactory* _GetWIC() noexcept
{
    static IWICImagingFactory* s_Factory = nullptr;

    if (s_Factory)
    {
        return s_Factory;
    }

    OK_OR_BAIL(CoCreateInstance(
        CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER, __uuidof(IWICImagingFactory), (LPVOID*)&s_Factory));

    return s_Factory;
}

bool ReencodeJPGImage(BYTE* imageBuf, const DWORD imageSize, DWORD& reencodedSize)
{
    auto pWIC = _GetWIC();
    wil::com_ptr_nothrow<IStream> imageStream = SHCreateMemStream(imageBuf, imageSize);
    if (!imageStream)
    {
        return false;
    }

    // Decode jpg into bitmap
    wil::com_ptr_nothrow<IWICBitmapDecoder> bitmapDecoder;
    OK_OR_BAIL(pWIC->CreateDecoderFromStream(imageStream.get(), nullptr, WICDecodeMetadataCacheOnLoad, &bitmapDecoder));
    wil::com_ptr_nothrow<IWICBitmapFrameDecode> decodedFrame;
    OK_OR_BAIL(bitmapDecoder->GetFrame(0, &decodedFrame));
    wil::com_ptr_nothrow<IWICBitmapSource> bitmap;
    bitmap.attach(decodedFrame.detach());
    UINT width = 0, height = 0;
    OK_OR_BAIL(bitmap->GetSize(&width, &height));

    // Initialize jpg encoder
    wil::com_ptr_nothrow<IWICBitmapEncoder> encoder;
    OK_OR_BAIL(pWIC->CreateEncoder(GUID_ContainerFormatJpeg, nullptr, &encoder));

    wil::com_ptr_nothrow<IStream> outputStream;
    OK_OR_BAIL(CreateStreamOnHGlobal(nullptr, true, &outputStream));
    OK_OR_BAIL(encoder->Initialize(outputStream.get(), WICBitmapEncoderNoCache));
    wil::com_ptr_nothrow<IWICBitmapFrameEncode> encodedFrame;
    wil::com_ptr_nothrow<IPropertyBag2> encoderOptions;
    OK_OR_BAIL(encoder->CreateNewFrame(&encodedFrame, &encoderOptions));

    ULONG nProperties = 0;
    OK_OR_BAIL(encoderOptions->CountProperties(&nProperties));
    for (ULONG propIdx = 0; propIdx < nProperties; ++propIdx)
    {
        PROPBAG2 propBag{};
        ULONG _;
        OK_OR_BAIL(encoderOptions->GetPropertyInfo(propIdx, 1, &propBag, &_));
        if (propBag.pstrName == std::wstring_view{ L"ImageQuality" })
        {
            wil::unique_variant variant;
            variant.vt = VT_R4;
            variant.fltVal = 0.1f;
            OK_OR_BAIL(encoderOptions->Write(1, &propBag, &variant));
            LOG("Successfully set jpg compression quality");
            // skip the rest of the properties
            propIdx = nProperties;
        }
        CoTaskMemFree(propBag.pstrName);
    }

    OK_OR_BAIL(encodedFrame->Initialize(encoderOptions.get()));
    WICPixelFormatGUID intermediateFormat = GUID_WICPixelFormat24bppRGB;

    OK_OR_BAIL(encodedFrame->SetPixelFormat(&intermediateFormat));
    OK_OR_BAIL(encodedFrame->SetSize(width, height));

    // Commit the image encoding
    OK_OR_BAIL(encodedFrame->WriteSource(bitmap.get(), nullptr));
    OK_OR_BAIL(encodedFrame->Commit());
    OK_OR_BAIL(encoder->Commit());

    STATSTG intermediateStreamStat{};
    OK_OR_BAIL(outputStream->Stat(&intermediateStreamStat, STATFLAG_NONAME));
    const ULONGLONG jpgStreamSize = intermediateStreamStat.cbSize.QuadPart;
    HGLOBAL streamMemoryHandle{};
    OK_OR_BAIL(GetHGlobalFromStream(outputStream.get(), &streamMemoryHandle));

    auto jpgStreamMemory = static_cast<uint8_t*>(GlobalLock(streamMemoryHandle));
    std::copy(jpgStreamMemory, jpgStreamMemory + jpgStreamSize, imageBuf);
    auto unlockJpgStreamMemory = wil::scope_exit([jpgStreamMemory] { GlobalUnlock(jpgStreamMemory); });
    reencodedSize = static_cast<DWORD>(jpgStreamSize);
    return true;
}

wil::com_ptr_nothrow<IWICBitmapSource> LoadAsRGB24BitmapWithSize(IWICImagingFactory* pWIC,
                                                                 wil::com_ptr_nothrow<IStream> image,
                                                                 const UINT targetWidth,
                                                                 const UINT targetHeight)
{
    wil::com_ptr_nothrow<IWICBitmapSource> bitmap;
    // Initialize image bitmap decoder from filename and get the image frame
    wil::com_ptr_nothrow<IWICBitmapDecoder> bitmapDecoder;
    OK_OR_BAIL(pWIC->CreateDecoderFromStream(image.get(), nullptr, WICDecodeMetadataCacheOnLoad, &bitmapDecoder));

    wil::com_ptr_nothrow<IWICBitmapFrameDecode> decodedFrame;
    OK_OR_BAIL(bitmapDecoder->GetFrame(0, &decodedFrame));

    UINT imageWidth = 0, imageHeight = 0;
    OK_OR_BAIL(decodedFrame->GetSize(&imageWidth, &imageHeight));

    // Scale the image if required
    if (targetWidth != imageWidth || targetHeight != imageHeight)
    {
        wil::com_ptr_nothrow<IWICBitmapScaler> scaler;
        OK_OR_BAIL(pWIC->CreateBitmapScaler(&scaler));
        OK_OR_BAIL(
            scaler->Initialize(decodedFrame.get(), targetWidth, targetHeight, WICBitmapInterpolationModeHighQualityCubic));
        bitmap.attach(scaler.detach());
    }
    else
    {
        bitmap.attach(decodedFrame.detach());
    }
    WICPixelFormatGUID pixelFormat{};
    OK_OR_BAIL(bitmap->GetPixelFormat(&pixelFormat));

    const auto targetPixelFormat = GUID_WICPixelFormat24bppBGR;
    if (pixelFormat != targetPixelFormat)
    {
        wil::com_ptr_nothrow<IWICBitmapSource> convertedBitmap;
        if (SUCCEEDED(WICConvertBitmapSource(targetPixelFormat, bitmap.get(), &convertedBitmap)))
        {
            return convertedBitmap;
        }
    }

    return bitmap;
}

wil::com_ptr_nothrow<IStream> EncodeBitmapToContainer(IWICImagingFactory* pWIC,
                                                      wil::com_ptr_nothrow<IWICBitmapSource> bitmap,
                                                      const GUID& containerGUID,
                                                      const UINT width,
                                                      const UINT height,
                                                      const float quality)
{
    wil::com_ptr_nothrow<IWICBitmapEncoder> encoder;
    pWIC->CreateEncoder(containerGUID, nullptr, &encoder);

    if (!encoder)
    {
        return nullptr;
    }

    // Prepare the encoder output memory stream and encoding params
    wil::com_ptr_nothrow<IStream> encodedBitmap;
    OK_OR_BAIL(CreateStreamOnHGlobal(nullptr, true, &encodedBitmap));
    OK_OR_BAIL(encoder->Initialize(encodedBitmap.get(), WICBitmapEncoderNoCache));
    wil::com_ptr_nothrow<IWICBitmapFrameEncode> encodedFrame;

    wil::com_ptr_nothrow<IPropertyBag2> encoderOptions;
    OK_OR_BAIL(encoder->CreateNewFrame(&encodedFrame, &encoderOptions));

    ULONG nProperties = 0;
    OK_OR_BAIL(encoderOptions->CountProperties(&nProperties));
    for (ULONG propIdx = 0; propIdx < nProperties; ++propIdx)
    {
        PROPBAG2 propBag{};
        ULONG _;
        OK_OR_BAIL(encoderOptions->GetPropertyInfo(propIdx, 1, &propBag, &_));
        if (propBag.pstrName == std::wstring_view{ L"ImageQuality" })
        {
            wil::unique_variant variant;
            variant.vt = VT_R4;
            variant.fltVal = quality;
            OK_OR_BAIL(encoderOptions->Write(1, &propBag, &variant));
            LOG("Successfully set jpg compression quality");
            // skip the rest of the properties
            propIdx = nProperties;
        }
        CoTaskMemFree(propBag.pstrName);
    }

    OK_OR_BAIL(encodedFrame->Initialize(encoderOptions.get()));

    WICPixelFormatGUID intermediateFormat = GUID_WICPixelFormat24bppRGB;
    OK_OR_BAIL(encodedFrame->SetPixelFormat(&intermediateFormat));
    OK_OR_BAIL(encodedFrame->SetSize(width, height));

    // Commit the image encoding
    OK_OR_BAIL(encodedFrame->WriteSource(bitmap.get(), nullptr));
    OK_OR_BAIL(encodedFrame->Commit());
    OK_OR_BAIL(encoder->Commit());
    return encodedBitmap;
}

IMFSample* ConvertIMFVideoSample(const MFT_REGISTER_TYPE_INFO& inputType,
                                 IMFMediaType* outputMediaType,
                                 const wil::com_ptr_nothrow<IMFSample>& inputSample,
                                 const UINT width,
                                 const UINT height)
{
    IMFActivate** ppVDActivate = nullptr;
    UINT32 count = 0;

    MFT_REGISTER_TYPE_INFO outputType = { MFMediaType_Video, {} };
    outputMediaType->GetGUID(MF_MT_SUBTYPE, &outputType.guidSubtype);

    const std::array<GUID, 3> transformerCategories = {
        MFT_CATEGORY_VIDEO_PROCESSOR, MFT_CATEGORY_VIDEO_DECODER, MFT_CATEGORY_VIDEO_ENCODER
    };

    for (const auto& transformerCategory : transformerCategories)
    {
        OK_OR_BAIL(MFTEnumEx(transformerCategory, MFT_ENUM_FLAG_SYNCMFT, &inputType, &outputType, &ppVDActivate, &count));
        if (count != 0)
        {
            break;
        }
    }

    wil::com_ptr_nothrow<IMFTransform> videoTransformer;

    bool videoDecoderActivated = false;
    for (UINT32 i = 0; i < count; ++i)
    {
        if (!videoDecoderActivated && !FAILED(ppVDActivate[i]->ActivateObject(IID_PPV_ARGS(&videoTransformer))))
        {
            videoDecoderActivated = true;
        }
        ppVDActivate[i]->Release();
    }

    if (count)
    {
        CoTaskMemFree(ppVDActivate);
    }

    if (!videoDecoderActivated)
    {
        LOG("No converter available for the selected format");
        return nullptr;
    }

    auto shutdownVideoDecoder = wil::scope_exit([&videoTransformer] { MFShutdownObject(videoTransformer.get()); });
    // Set input/output types for the decoder
    wil::com_ptr_nothrow<IMFMediaType> intermediateFrameMediaType;
    OK_OR_BAIL(MFCreateMediaType(&intermediateFrameMediaType));
    intermediateFrameMediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
    intermediateFrameMediaType->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_RGB24);
    intermediateFrameMediaType->SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive);
    intermediateFrameMediaType->SetUINT32(MF_MT_ALL_SAMPLES_INDEPENDENT, TRUE);
    OK_OR_BAIL(MFSetAttributeSize(intermediateFrameMediaType.get(), MF_MT_FRAME_SIZE, width, height));
    OK_OR_BAIL(MFSetAttributeRatio(intermediateFrameMediaType.get(), MF_MT_PIXEL_ASPECT_RATIO, width, height));
    OK_OR_BAIL(videoTransformer->SetInputType(0, intermediateFrameMediaType.get(), 0));
    OK_OR_BAIL(videoTransformer->SetOutputType(0, outputMediaType, 0));

    // Process the input sample
    OK_OR_BAIL(videoTransformer->ProcessInput(0, inputSample.get(), 0));

    // Check whether we need to allocate output sample and buffer ourselves
    MFT_OUTPUT_STREAM_INFO outputStreamInfo{};
    OK_OR_BAIL(videoTransformer->GetOutputStreamInfo(0, &outputStreamInfo));
    const bool onlyProvidesSamples = outputStreamInfo.dwFlags & MFT_OUTPUT_STREAM_PROVIDES_SAMPLES;
    const bool canProvideSamples = outputStreamInfo.dwFlags & MFT_OUTPUT_STREAM_CAN_PROVIDE_SAMPLES;
    const bool mustAllocateSample =
        (!onlyProvidesSamples && !canProvideSamples) ||
        (!onlyProvidesSamples && (outputStreamInfo.dwFlags & MFT_PROCESS_OUTPUT_DISCARD_WHEN_NO_BUFFER));

    MFT_OUTPUT_DATA_BUFFER outputSamples{};
    IMFSample* outputSample = nullptr;

    // If so, do the allocation
    if (mustAllocateSample)
    {
        OK_OR_BAIL(MFCreateSample(&outputSample));
        OK_OR_BAIL(outputSample->SetSampleDuration(333333));
        OK_OR_BAIL(outputSample->SetSampleTime(1));
        OK_OR_BAIL(outputSample->SetUINT32(MF_MT_VIDEO_ROTATION, MFVideoRotationFormat::MFVideoRotationFormat_0));
        IMFMediaBuffer* outputMediaBuffer = nullptr;
        OK_OR_BAIL(
            MFCreateAlignedMemoryBuffer(outputStreamInfo.cbSize, outputStreamInfo.cbAlignment - 1, &outputMediaBuffer));
        OK_OR_BAIL(outputMediaBuffer->SetCurrentLength(outputStreamInfo.cbSize));
        OK_OR_BAIL(outputSample->AddBuffer(outputMediaBuffer));
        outputSamples.pSample = outputSample;
    }

    // Finally, produce the output sample
    DWORD processStatus = 0;
    if (failed(videoTransformer->ProcessOutput(0, 1, &outputSamples, &processStatus)))
    {
        LOG("Failed to convert image frame");
    }
    if (outputSamples.pEvents)
    {
        outputSamples.pEvents->Release();
    }

    return outputSamples.pSample;
}

wil::com_ptr_nothrow<IMFSample> LoadImageAsSample(wil::com_ptr_nothrow<IStream> imageStream,
                                                  IMFMediaType* sampleMediaType,
                                                  const float quality) noexcept
{
    UINT targetWidth = 0;
    UINT targetHeight = 0;
    OK_OR_BAIL(MFGetAttributeSize(sampleMediaType, MF_MT_FRAME_SIZE, &targetWidth, &targetHeight));
    MFT_REGISTER_TYPE_INFO outputType = { MFMediaType_Video, {} };
    OK_OR_BAIL(sampleMediaType->GetGUID(MF_MT_SUBTYPE, &outputType.guidSubtype));

    IWICImagingFactory* pWIC = _GetWIC();
    if (!pWIC)
    {
        LOG("Failed to create IWICImagingFactory");
        return nullptr;
    }

    if (!imageStream)
    {
        return nullptr;
    }

    const auto srcImageBitmap = LoadAsRGB24BitmapWithSize(pWIC, imageStream, targetWidth, targetHeight);
    if (!srcImageBitmap)
    {
        return nullptr;
    }

    // First, let's create a sample containing RGB24 bitmap
    IMFSample* outputSample = nullptr;
    OK_OR_BAIL(MFCreateSample(&outputSample));
    OK_OR_BAIL(outputSample->SetUINT32(MF_MT_VIDEO_ROTATION, MFVideoRotationFormat::MFVideoRotationFormat_0));
    OK_OR_BAIL(outputSample->SetSampleDuration(333333));
    OK_OR_BAIL(outputSample->SetSampleTime(1));
    IMFMediaBuffer* outputMediaBuffer = nullptr;
    const DWORD nPixelBytes = targetWidth * targetHeight * 3;
    OK_OR_BAIL(MFCreateAlignedMemoryBuffer(nPixelBytes, MF_64_BYTE_ALIGNMENT, &outputMediaBuffer));

    const UINT stride = 3 * targetWidth;

    DWORD max_length = 0, current_length = 0;
    BYTE* sampleBufferMemory = nullptr;
    OK_OR_BAIL(outputMediaBuffer->Lock(&sampleBufferMemory, &max_length, &current_length));
    OK_OR_BAIL(srcImageBitmap->CopyPixels(nullptr, stride, nPixelBytes, sampleBufferMemory));
    OK_OR_BAIL(outputMediaBuffer->Unlock());

    OK_OR_BAIL(outputMediaBuffer->SetCurrentLength(nPixelBytes));
    OK_OR_BAIL(outputSample->AddBuffer(outputMediaBuffer));

    if (outputType.guidSubtype == MFVideoFormat_RGB24)
    {
        return outputSample;
    }

    // Special case for mjpg, since we need to use jpg container for it instead of supplying raw pixels
    if (outputType.guidSubtype == MFVideoFormat_MJPG)
    {
        // Use an intermediate jpg container sample which will be transcoded to the target format
        wil::com_ptr_nothrow<IStream> jpgStream =
            EncodeBitmapToContainer(pWIC, srcImageBitmap, GUID_ContainerFormatJpeg, targetWidth, targetHeight, quality);

        // Obtain stream size and lock its memory pointer
        STATSTG intermediateStreamStat{};
        OK_OR_BAIL(jpgStream->Stat(&intermediateStreamStat, STATFLAG_NONAME));
        const ULONGLONG jpgStreamSize = intermediateStreamStat.cbSize.QuadPart;
        HGLOBAL streamMemoryHandle{};
        OK_OR_BAIL(GetHGlobalFromStream(jpgStream.get(), &streamMemoryHandle));

        auto jpgStreamMemory = static_cast<uint8_t*>(GlobalLock(streamMemoryHandle));
        auto unlockJpgStreamMemory = wil::scope_exit([jpgStreamMemory] { GlobalUnlock(jpgStreamMemory); });

        // Create a sample from the input image buffer
        wil::com_ptr_nothrow<IMFSample> jpgSample;
        OK_OR_BAIL(MFCreateSample(&jpgSample));
        OK_OR_BAIL(jpgSample->SetUINT32(MF_MT_VIDEO_ROTATION, MFVideoRotationFormat::MFVideoRotationFormat_0));
        IMFMediaBuffer* inputMediaBuffer = nullptr;
        OK_OR_BAIL(MFCreateAlignedMemoryBuffer(static_cast<DWORD>(jpgStreamSize), MF_64_BYTE_ALIGNMENT, &inputMediaBuffer));
        BYTE* inputBuf = nullptr;
        OK_OR_BAIL(inputMediaBuffer->Lock(&inputBuf, &max_length, &current_length));
        if (max_length < jpgStreamSize)
        {
            return nullptr;
        }

        std::copy(jpgStreamMemory, jpgStreamMemory + jpgStreamSize, inputBuf);
        unlockJpgStreamMemory.reset();
        OK_OR_BAIL(inputMediaBuffer->Unlock());
        OK_OR_BAIL(inputMediaBuffer->SetCurrentLength(static_cast<DWORD>(jpgStreamSize)));
        OK_OR_BAIL(jpgSample->AddBuffer(inputMediaBuffer));

        return jpgSample;
    }

    // Now we are ready to convert it to the requested media type
    MFT_REGISTER_TYPE_INFO intermediateType = { MFMediaType_Video, MFVideoFormat_RGB24 };

    // But if no conversion is needed, just return the input sample

    return ConvertIMFVideoSample(intermediateType, sampleMediaType, outputSample, targetWidth, targetHeight);
}