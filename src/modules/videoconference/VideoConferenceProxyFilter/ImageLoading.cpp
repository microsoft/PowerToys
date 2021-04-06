#include <initguid.h>
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

#include <mfapi.h>
#include <mfidl.h>
#include <mftransform.h>
#include <dshow.h>

#include <shlwapi.h>

#include "Logging.h"

IWICImagingFactory* _GetWIC() noexcept
{
    static IWICImagingFactory* s_Factory = nullptr;

    if (s_Factory)
    {
        return s_Factory;
    }

    HRESULT hr = CoCreateInstance(
        CLSID_WICImagingFactory,
        nullptr,
        CLSCTX_INPROC_SERVER,
        __uuidof(IWICImagingFactory),
        (LPVOID*)&s_Factory);

    if (FAILED(hr))
    {
        s_Factory = nullptr;
        return nullptr;
    }

    return s_Factory;
}

wil::com_ptr_nothrow<IWICBitmapSource> LoadAsBitmapWithSize(IWICImagingFactory* pWIC,
                                                            wil::com_ptr_nothrow<IStream> image,
                                                            const UINT targetWidth,
                                                            const UINT targetHeight)
{
    wil::com_ptr_nothrow<IWICBitmapSource> bitmap;
    // Initialize image bitmap decoder from filename and get the image frame
    wil::com_ptr_nothrow<IWICBitmapDecoder> bitmapDecoder;
    pWIC->CreateDecoderFromStream(image.get(), nullptr, WICDecodeMetadataCacheOnLoad, &bitmapDecoder);
    if (!bitmapDecoder)
    {
        return bitmap;
    }

    wil::com_ptr_nothrow<IWICBitmapFrameDecode> decodedFrame;
    bitmapDecoder->GetFrame(0, &decodedFrame);

    if (!decodedFrame)
    {
        return bitmap;
    }

    UINT imageWidth = 0, imageHeight = 0;
    decodedFrame->GetSize(&imageWidth, &imageHeight);

    // Scale the image if required
    if (targetWidth != imageWidth || targetHeight != imageHeight)
    {
        wil::com_ptr_nothrow<IWICBitmapScaler> scaler;
        pWIC->CreateBitmapScaler(&scaler);
        if (!scaler)
        {
            return bitmap;
        }
        scaler->Initialize(decodedFrame.get(), targetWidth, targetHeight, WICBitmapInterpolationModeHighQualityCubic);
        bitmap.attach(scaler.detach());
    }
    else
    {
        bitmap.attach(decodedFrame.detach());
    }

    return bitmap;
}

wil::com_ptr_nothrow<IMFSample> LoadImageAsSample(wil::com_ptr_nothrow<IStream> imageStream,
                                                  IMFMediaType* sampleMediaType) noexcept
{
    UINT targetWidth = 0;
    UINT targetHeight = 0;
    MFGetAttributeSize(sampleMediaType, MF_MT_FRAME_SIZE, &targetWidth, &targetHeight);
    MFT_REGISTER_TYPE_INFO outputType = { MFMediaType_Video, {} };
    sampleMediaType->GetGUID(MF_MT_SUBTYPE, &outputType.guidSubtype);

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

    const auto sourceImageFrame = LoadAsBitmapWithSize(pWIC, imageStream, targetWidth, targetHeight);
    if (!sourceImageFrame)
    {
        return nullptr;
    }

    // Copy the raw pixels data to the sample buffer w/o using any image container
    if (outputType.guidSubtype == MFVideoFormat_RGB24)
    {
        IMFSample* outputSample = nullptr;
        MFCreateSample(&outputSample);
        outputSample->SetSampleDuration(333333);
        IMFMediaBuffer* outputMediaBuffer = nullptr;
        const DWORD nPixelBytes = targetWidth * targetHeight * 3;
        MFCreateAlignedMemoryBuffer(nPixelBytes, MF_64_BYTE_ALIGNMENT, &outputMediaBuffer);

        const UINT stride = 3 * targetWidth;

        DWORD max_length = 0, current_length = 0;
        BYTE* sampleBufferMemory = nullptr;
        outputMediaBuffer->Lock(&sampleBufferMemory, &max_length, &current_length);
        sourceImageFrame->CopyPixels(nullptr, stride, nPixelBytes, sampleBufferMemory);
        outputMediaBuffer->Unlock();

        outputMediaBuffer->SetCurrentLength(nPixelBytes);
        outputSample->AddBuffer(outputMediaBuffer);
        return outputSample;
    }

    // Otherwise, create an intermediate jpg container sample which will be transcoded to the target format
    wil::com_ptr_nothrow<IWICBitmapEncoder> intermediateEncoder;
    pWIC->CreateEncoder(GUID_ContainerFormatJpeg, nullptr, &intermediateEncoder);

    if (!intermediateEncoder)
    {
        return nullptr;
    }

    // Prepare the encoder output memory stream and encoding params
    wil::com_ptr_nothrow<IStream> intermediateContainerStream;
    CreateStreamOnHGlobal(nullptr, true, &intermediateContainerStream);
    intermediateEncoder->Initialize(intermediateContainerStream.get(), WICBitmapEncoderNoCache);
    wil::com_ptr_nothrow<IWICBitmapFrameEncode> intermediateFrame;
    intermediateEncoder->CreateNewFrame(&intermediateFrame, nullptr);
    intermediateFrame->Initialize(nullptr);

    WICPixelFormatGUID intermediateFormat = GUID_WICPixelFormat24bppBGR;
    intermediateFrame->SetPixelFormat(&intermediateFormat);
    intermediateFrame->SetSize(targetWidth, targetHeight);

    // Commit the image encoding
    intermediateFrame->WriteSource(sourceImageFrame.get(), nullptr);
    intermediateFrame->Commit();
    intermediateEncoder->Commit();

    // Obtain stream size and lock its memory pointer
    STATSTG intermediateStreamStat{};
    intermediateContainerStream->Stat(&intermediateStreamStat, STATFLAG_NONAME);
    const ULONGLONG intermediateStreamSize = intermediateStreamStat.cbSize.QuadPart;
    HGLOBAL streamMemoryHandle{};
    GetHGlobalFromStream(intermediateContainerStream.get(), &streamMemoryHandle);

    auto intermediateStreamMemory = static_cast<uint8_t*>(GlobalLock(streamMemoryHandle));
    auto unlockIntermediateStreamMemory =
        wil::scope_exit([intermediateStreamMemory] { GlobalUnlock(intermediateStreamMemory); });

    // Create a sample from the input image buffer
    wil::com_ptr_nothrow<IMFSample> inputSample;
    MFCreateSample(&inputSample);

    IMFMediaBuffer* inputMediaBuffer = nullptr;
    MFCreateAlignedMemoryBuffer(static_cast<DWORD>(intermediateStreamSize), MF_64_BYTE_ALIGNMENT, &inputMediaBuffer);
    BYTE* inputBuf = nullptr;
    DWORD max_length = 0, current_length = 0;
    inputMediaBuffer->Lock(&inputBuf, &max_length, &current_length);
    if (max_length < intermediateStreamSize)
    {
        return nullptr;
    }

    std::copy(intermediateStreamMemory, intermediateStreamMemory + intermediateStreamSize, inputBuf);
    unlockIntermediateStreamMemory.reset();
    inputMediaBuffer->Unlock();
    inputMediaBuffer->SetCurrentLength(static_cast<DWORD>(intermediateStreamSize));
    inputSample->AddBuffer(inputMediaBuffer);

    // Now we are ready to convert it to the requested media type, so we need to find a suitable jpg encoder
    MFT_REGISTER_TYPE_INFO intermediateType = { MFMediaType_Video, MFVideoFormat_MJPG };

    // But if no conversion is needed, just return the input sample
    if (intermediateType.guidSubtype == outputType.guidSubtype)
    {
        return inputSample;
    }

    IMFActivate** ppVDActivate = nullptr;
    UINT32 count = 0;

    MFTEnumEx(MFT_CATEGORY_VIDEO_DECODER,
              MFT_ENUM_FLAG_SYNCMFT | MFT_ENUM_FLAG_TRANSCODE_ONLY,
              &intermediateType,
              &outputType,
              &ppVDActivate,
              &count);
    wil::com_ptr_nothrow<IMFTransform> videoDecoder;

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
        LOG("No converter avialable for the selected format");
        return nullptr;
    }

    auto shutdownVideoDecoder = wil::scope_exit([&videoDecoder] { MFShutdownObject(videoDecoder.get()); });
    // Set input/output types for the decoder
    wil::com_ptr_nothrow<IMFMediaType> intermediateFrameMediaType;
    MFCreateMediaType(&intermediateFrameMediaType);
    intermediateFrameMediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
    intermediateFrameMediaType->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_MJPG);
    intermediateFrameMediaType->SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive);
    intermediateFrameMediaType->SetUINT32(MF_MT_ALL_SAMPLES_INDEPENDENT, TRUE);
    MFSetAttributeSize(intermediateFrameMediaType.get(), MF_MT_FRAME_SIZE, targetWidth, targetHeight);
    MFSetAttributeRatio(intermediateFrameMediaType.get(), MF_MT_PIXEL_ASPECT_RATIO, 1, 1);
    videoDecoder->SetInputType(0, intermediateFrameMediaType.get(), 0);
    videoDecoder->SetOutputType(0, sampleMediaType, 0);

    // Process the input sample
    videoDecoder->ProcessInput(0, inputSample.get(), 0);

    // Check whether we need to allocate output sample and buffer ourselves
    MFT_OUTPUT_STREAM_INFO outputStreamInfo{};
    videoDecoder->GetOutputStreamInfo(0, &outputStreamInfo);
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
        MFCreateSample(&outputSample);
        outputSample->SetSampleDuration(333333);
        IMFMediaBuffer* outputMediaBuffer = nullptr;
        MFCreateAlignedMemoryBuffer(outputStreamInfo.cbSize, outputStreamInfo.cbAlignment - 1, &outputMediaBuffer);
        outputMediaBuffer->SetCurrentLength(outputStreamInfo.cbSize);
        outputSample->AddBuffer(outputMediaBuffer);
        outputSamples.pSample = outputSample;
    }

    // Finally, produce the output sample
    DWORD processStatus = 0;
    if (FAILED(videoDecoder->ProcessOutput(0, 1, &outputSamples, &processStatus)))
    {
        LOG("Failed to convert image frame");
    }
    if (outputSamples.pEvents)
    {
        outputSamples.pEvents->Release();
    }

    return outputSamples.pSample;
}
