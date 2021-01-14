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

#include <shlwapi.h>

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
        s_Factory = nullptr;
        return nullptr;
    }

    return s_Factory;
}

wil::com_ptr_nothrow<IMFSample> LoadImageAsSample(wil::com_ptr_nothrow<IStream> imageStream, IMFMediaType* sampleMediaType) noexcept
{
    //HRESULT hr = S_OK;

    // get target sample frame dimensions
    UINT targetWidth = 0;
    UINT targetHeight = 0;
    MFGetAttributeSize(sampleMediaType, MF_MT_FRAME_SIZE, &targetWidth, &targetHeight);

    IWICImagingFactory* pWIC = _GetWIC();
    if (!pWIC)
    {
        LOG("Failed to create IWICImagingFactory");
        return nullptr;
    }

    // Initialize image bitmap decoder from filename and get the image frame
    wil::com_ptr_nothrow<IWICBitmapDecoder> bitmapDecoder;
    pWIC->CreateDecoderFromStream(imageStream.get(), nullptr, WICDecodeMetadataCacheOnLoad, &bitmapDecoder);

    wil::com_ptr_nothrow<IWICBitmapFrameDecode> decodedFrame;
    bitmapDecoder->GetFrame(0, &decodedFrame);

    UINT imageWidth = 0, imageHeight = 0;
    decodedFrame->GetSize(&imageWidth, &imageHeight);

    // Scale the image if required
    wil::com_ptr_nothrow<IWICBitmapSource> sourceImageFrame;
    if (targetWidth != imageWidth || targetHeight != imageHeight)
    {
        wil::com_ptr_nothrow<IWICBitmapScaler> scaler;
        pWIC->CreateBitmapScaler(&scaler);
        scaler->Initialize(decodedFrame.get(), targetWidth, targetHeight, WICBitmapInterpolationModeHighQualityCubic);
        sourceImageFrame.attach(scaler.detach());
    }
    else
    {
        sourceImageFrame.attach(decodedFrame.detach());
    }

    MFT_REGISTER_TYPE_INFO outputFilter = { MFMediaType_Video, {} };
    sampleMediaType->GetGUID(MF_MT_SUBTYPE, &outputFilter.guidSubtype);

    wil::com_ptr_nothrow<IWICBitmapEncoder> jpgEncoder;
    pWIC->CreateEncoder(
        outputFilter.guidSubtype == MFVideoFormat_RGB24 ? GUID_ContainerFormatBmp : GUID_ContainerFormatJpeg, nullptr, &jpgEncoder);

    // Prepare the encoder output memory stream and encoding params
    wil::com_ptr_nothrow<IStream> jpgStream;
    CreateStreamOnHGlobal(nullptr, true, &jpgStream);
    jpgEncoder->Initialize(jpgStream.get(), WICBitmapEncoderNoCache);
    wil::com_ptr_nothrow<IWICBitmapFrameEncode> jpgFrame;
    jpgEncoder->CreateNewFrame(&jpgFrame, nullptr);
    jpgFrame->Initialize(nullptr);

    WICPixelFormatGUID jpgFormat = GUID_WICPixelFormat24bppBGR;
    jpgFrame->SetPixelFormat(&jpgFormat);
    jpgFrame->SetSize(targetWidth, targetHeight);

    // Commit the image encoding
    jpgFrame->WriteSource(sourceImageFrame.get(), nullptr);
    jpgFrame->Commit();
    jpgEncoder->Commit();

    // Obtain stream size and lock its memory pointer
    STATSTG jpgStreamStat{};
    jpgStream->Stat(&jpgStreamStat, STATFLAG_NONAME);
    const ULONGLONG jpgStreamSize = jpgStreamStat.cbSize.QuadPart;
    HGLOBAL streamMemoryHandle{};
    GetHGlobalFromStream(jpgStream.get(), &streamMemoryHandle);

    auto jpgStreamMemory = static_cast<uint8_t*>(GlobalLock(streamMemoryHandle));
    auto unlockJpgStreamMemory = wil::scope_exit([jpgStreamMemory] { GlobalUnlock(jpgStreamMemory); });

    // Create a sample from the input image buffer
    wil::com_ptr_nothrow<IMFSample> inputSample;
    MFCreateSample(&inputSample);

    IMFMediaBuffer* inputMediaBuffer = nullptr;
    MFCreateAlignedMemoryBuffer(static_cast<DWORD>(jpgStreamSize), MF_64_BYTE_ALIGNMENT, &inputMediaBuffer);
    BYTE* inputBuf = nullptr;
    DWORD max_length = 0, current_length = 0;
    inputMediaBuffer->Lock(&inputBuf, &max_length, &current_length);
    if (max_length < jpgStreamSize)
    {
        return nullptr;
    }
    std::copy(jpgStreamMemory, jpgStreamMemory + jpgStreamSize, inputBuf);
    unlockJpgStreamMemory.reset();
    inputMediaBuffer->Unlock();
    inputMediaBuffer->SetCurrentLength(static_cast<DWORD>(jpgStreamSize));
    inputSample->AddBuffer(inputMediaBuffer);

    // Now we are ready to convert it to the requested media type, so we need to find a suitable jpg encoder
    MFT_REGISTER_TYPE_INFO inputFilter = { MFMediaType_Video, outputFilter.guidSubtype == MFVideoFormat_RGB24 ? MFVideoFormat_RGB24 : MFVideoFormat_MJPG };

    // But if no conversion is needed, just return the input sample
    if (!memcmp(&inputFilter.guidSubtype, &outputFilter.guidSubtype, sizeof(GUID)))
    {
        return inputSample;
    }

    IMFActivate** ppVDActivate = nullptr;
    UINT32 count = 0;

    MFTEnumEx(MFT_CATEGORY_VIDEO_DECODER, MFT_ENUM_FLAG_SYNCMFT, &inputFilter, &outputFilter, &ppVDActivate, &count);
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
        //LOG("No converter avialable for the selected format");
        return nullptr;
    }
    auto shutdownVideoDecoder = wil::scope_exit([&videoDecoder] { MFShutdownObject(videoDecoder.get()); });
    // Set input/output types for the decoder
    wil::com_ptr_nothrow<IMFMediaType> jpgFrameMediaType;
    MFCreateMediaType(&jpgFrameMediaType);
    jpgFrameMediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
    jpgFrameMediaType->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_MJPG);
    jpgFrameMediaType->SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive);
    jpgFrameMediaType->SetUINT32(MF_MT_ALL_SAMPLES_INDEPENDENT, TRUE);
    MFSetAttributeSize(jpgFrameMediaType.get(), MF_MT_FRAME_SIZE, targetWidth, targetHeight);
    MFSetAttributeRatio(jpgFrameMediaType.get(), MF_MT_PIXEL_ASPECT_RATIO, 1, 1);
    videoDecoder->SetInputType(0, jpgFrameMediaType.get(), 0);
    videoDecoder->SetOutputType(0, sampleMediaType, 0);

    // Process the input sample
    videoDecoder->ProcessInput(0, inputSample.get(), 0);

    // Check whether we need to allocate output sample and buffer ourselves
    MFT_OUTPUT_STREAM_INFO outputStreamInfo{};
    videoDecoder->GetOutputStreamInfo(0, &outputStreamInfo);
    const bool onlyProvidesSamples = outputStreamInfo.dwFlags & MFT_OUTPUT_STREAM_PROVIDES_SAMPLES;
    const bool canProvideSamples = outputStreamInfo.dwFlags & MFT_OUTPUT_STREAM_CAN_PROVIDE_SAMPLES;
    const bool mustAllocateSample = (!onlyProvidesSamples && !canProvideSamples) || (!onlyProvidesSamples && (outputStreamInfo.dwFlags & MFT_PROCESS_OUTPUT_DISCARD_WHEN_NO_BUFFER));

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
    videoDecoder->ProcessOutput(0, 1, &outputSamples, &processStatus);
    if (outputSamples.pEvents)
    {
        outputSamples.pEvents->Release();
    }
    return outputSamples.pSample;
}