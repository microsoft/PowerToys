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

#include "ImageLoader.h"

#include "Logging.h"

IWICImagingFactory* _GetWIC() noexcept
{
    static IWICImagingFactory* s_Factory = nullptr;

    if (s_Factory)
        return s_Factory;

    HRESULT hr = CoCreateInstance(
        CLSID_WICImagingFactory,
        nullptr,
        CLSCTX_INPROC_SERVER,
        __uuidof(IWICImagingFactory),
        (LPVOID*)&s_Factory);

    if (FAILED(hr))
    {
        LogToFile(std::string("_GetWIC() failed with code: " + hr));
        s_Factory = nullptr;
        return nullptr;
    }

    return s_Factory;
}

using Microsoft::WRL::ComPtr;

ComPtr<IMFSample> LoadImageAsSample(ComPtr<IStream> imageStream, IMFMediaType* sampleMediaType) noexcept
{
    HRESULT hr = S_OK;

    // Get target sample frame dimensions
    UINT targetWidth = 0;
    UINT targetHeight = 0;
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(MFGetAttributeSize(sampleMediaType, MF_MT_FRAME_SIZE, &targetWidth, &targetHeight));

    IWICImagingFactory* pWIC = _GetWIC();
    if (!pWIC)
    {
        LogToFile("Failed to create IWICImagingFactory");
        return nullptr;
    }

    // Initialize image bitmap decoder from filename and get the image frame
    ComPtr<IWICBitmapDecoder> bitmapDecoder;
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(pWIC->CreateDecoderFromStream(imageStream.Get(), nullptr, WICDecodeMetadataCacheOnLoad, &bitmapDecoder));

    ComPtr<IWICBitmapFrameDecode> decodedFrame;
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(bitmapDecoder->GetFrame(0, &decodedFrame));

    UINT imageWidth = 0, imageHeight = 0;
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(decodedFrame->GetSize(&imageWidth, &imageHeight));

    // Scale the image if required
    ComPtr<IWICBitmapSource> sourceImageFrame;
    if (targetWidth != imageWidth || targetHeight != imageHeight)
    {
        ComPtr<IWICBitmapScaler> scaler;
        RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(pWIC->CreateBitmapScaler(&scaler));
        RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(scaler->Initialize(decodedFrame.Get(), targetWidth, targetHeight, WICBitmapInterpolationModeHighQualityCubic));
        sourceImageFrame.Attach(scaler.Detach());
    }
    else
    {
        sourceImageFrame.Attach(decodedFrame.Detach());
    }

    MFT_REGISTER_TYPE_INFO outputFilter = { MFMediaType_Video, {} };
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(sampleMediaType->GetGUID(MF_MT_SUBTYPE, &outputFilter.guidSubtype));

    ComPtr<IWICBitmapEncoder> jpgEncoder;
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(pWIC->CreateEncoder(
        outputFilter.guidSubtype == MFVideoFormat_RGB24 ? GUID_ContainerFormatBmp : GUID_ContainerFormatJpeg, nullptr, &jpgEncoder));

    // Prepare the encoder output memory stream and encoding params
    ComPtr<IStream> jpgStream;
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(CreateStreamOnHGlobal(nullptr, true, &jpgStream));
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(jpgEncoder->Initialize(jpgStream.Get(), WICBitmapEncoderNoCache));
    ComPtr<IWICBitmapFrameEncode> jpgFrame;
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(jpgEncoder->CreateNewFrame(&jpgFrame, nullptr));
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(jpgFrame->Initialize(nullptr));

    WICPixelFormatGUID jpgFormat = GUID_WICPixelFormat24bppBGR;
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(jpgFrame->SetPixelFormat(&jpgFormat));
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(jpgFrame->SetSize(targetWidth, targetHeight));

    // Commit the image encoding
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(jpgFrame->WriteSource(sourceImageFrame.Get(), nullptr));
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(jpgFrame->Commit());
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(jpgEncoder->Commit());

    // Obtain stream size and lock its memory pointer
    STATSTG jpgStreamStat{};
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(jpgStream->Stat(&jpgStreamStat, STATFLAG_NONAME));
    const size_t jpgStreamSize = jpgStreamStat.cbSize.QuadPart;
    HGLOBAL streamMemoryHandle{};
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(GetHGlobalFromStream(jpgStream.Get(), &streamMemoryHandle));

    auto jpgStreamMemory = static_cast<uint8_t*>(GlobalLock(streamMemoryHandle));
    auto unlockJpgStreamMemory = wil::scope_exit([jpgStreamMemory] { GlobalUnlock(jpgStreamMemory); });

    // Create a sample from the input image buffer
    ComPtr<IMFSample> inputSample;
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(MFCreateSample(&inputSample));

    IMFMediaBuffer* inputMediaBuffer = nullptr;
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(MFCreateAlignedMemoryBuffer(static_cast<DWORD>(jpgStreamSize), MF_64_BYTE_ALIGNMENT, &inputMediaBuffer));
    BYTE* inputBuf = nullptr;
    DWORD max_length = 0, current_length = 0;
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(inputMediaBuffer->Lock(&inputBuf, &max_length, &current_length));
    if (max_length < jpgStreamSize)
    {
        LogToFile("max_length < jpgStreamSize");
        return nullptr;
    }
    std::copy(jpgStreamMemory, jpgStreamMemory + jpgStreamSize, inputBuf);
    unlockJpgStreamMemory.reset();
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(inputMediaBuffer->Unlock());
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(inputMediaBuffer->SetCurrentLength(static_cast<DWORD>(jpgStreamSize)));
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(inputSample->AddBuffer(inputMediaBuffer));

    // Now we are ready to convert it to the requested media type, so we need to find a suitable jpg encoder
    MFT_REGISTER_TYPE_INFO inputFilter = { MFMediaType_Video, outputFilter.guidSubtype == MFVideoFormat_RGB24 ? MFVideoFormat_RGB24 : MFVideoFormat_MJPG };

    // But if no conversion is needed, just return the input sample
    if (!memcmp(&inputFilter.guidSubtype, &outputFilter.guidSubtype, sizeof(GUID)))
    {
        return inputSample;
    }

    IMFActivate** ppVDActivate = nullptr;
    UINT32 count = 0;

    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(MFTEnumEx(MFT_CATEGORY_VIDEO_DECODER, MFT_ENUM_FLAG_SYNCMFT, &inputFilter, &outputFilter, &ppVDActivate, &count));
    ComPtr<IMFTransform> videoDecoder;

    bool videoDecoderActivated = false;
    for (UINT32 i = 0; i < count; ++i)
    {
        if (!videoDecoderActivated && !FAILED(ppVDActivate[i]->ActivateObject(IID_PPV_ARGS(&videoDecoder))))
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
        LogToFile("No converter avialable for the selected format");
        return nullptr;
    }
    auto shutdownVideoDecoder = wil::scope_exit([&videoDecoder] { MFShutdownObject(videoDecoder.Get()); });
    // Set input/output types for the decoder
    ComPtr<IMFMediaType> jpgFrameMediaType;
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(MFCreateMediaType(&jpgFrameMediaType));
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(jpgFrameMediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video));
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(jpgFrameMediaType->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_MJPG));
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(jpgFrameMediaType->SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive));
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(jpgFrameMediaType->SetUINT32(MF_MT_ALL_SAMPLES_INDEPENDENT, TRUE));
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(MFSetAttributeSize(jpgFrameMediaType.Get(), MF_MT_FRAME_SIZE, targetWidth, targetHeight));
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(MFSetAttributeRatio(jpgFrameMediaType.Get(), MF_MT_PIXEL_ASPECT_RATIO, 1, 1));
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(videoDecoder->SetInputType(0, jpgFrameMediaType.Get(), 0));
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(videoDecoder->SetOutputType(0, sampleMediaType, 0));

    // Process the input sample
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(videoDecoder->ProcessInput(0, inputSample.Get(), 0));

    // Check whether we need to allocate output sample and buffer ourselves
    MFT_OUTPUT_STREAM_INFO outputStreamInfo{};
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(videoDecoder->GetOutputStreamInfo(0, &outputStreamInfo));
    const bool onlyProvidesSamples = outputStreamInfo.dwFlags & MFT_OUTPUT_STREAM_PROVIDES_SAMPLES;
    const bool canProvideSamples = outputStreamInfo.dwFlags & MFT_OUTPUT_STREAM_CAN_PROVIDE_SAMPLES;
    const bool mustAllocateSample = (!onlyProvidesSamples && !canProvideSamples) || (!onlyProvidesSamples && (outputStreamInfo.dwFlags & MFT_PROCESS_OUTPUT_DISCARD_WHEN_NO_BUFFER));

    MFT_OUTPUT_DATA_BUFFER outputSamples{};
    IMFSample* outputSample = nullptr;

    // If so, do the allocation
    if (mustAllocateSample)
    {
        RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(MFCreateSample(&outputSample));
        RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(outputSample->SetSampleDuration(333333));
        IMFMediaBuffer* outputMediaBuffer = nullptr;
        RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(MFCreateAlignedMemoryBuffer(outputStreamInfo.cbSize, outputStreamInfo.cbAlignment - 1, &outputMediaBuffer));
        RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(outputMediaBuffer->SetCurrentLength(outputStreamInfo.cbSize));
        RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(outputSample->AddBuffer(outputMediaBuffer));
        outputSamples.pSample = outputSample;
    }

    // Finally, produce the output sample
    DWORD processStatus = 0;
    RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(videoDecoder->ProcessOutput(0, 1, &outputSamples, &processStatus));
    if (outputSamples.pEvents)
    {
        outputSamples.pEvents->Release();
    }
    return outputSamples.pSample;
}