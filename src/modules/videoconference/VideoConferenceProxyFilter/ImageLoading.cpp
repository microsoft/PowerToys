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

bool failed(HRESULT hr)
{
    return FAILED(hr);
}

bool failed(bool val)
{
    return val == false;
}

template<typename T>
bool failed(wil::com_ptr_nothrow<T>& ptr)
{
    return ptr == nullptr;
}

#define OK_OR_BAIL(expr) \
    if (failed(expr))    \
        return {};

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
        OK_OR_BAIL(scaler->Initialize(decodedFrame.get(), targetWidth, targetHeight, WICBitmapInterpolationModeHighQualityCubic));
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
            bitmap = std::move(convertedBitmap);
        }
    }

    return bitmap;
}

wil::com_ptr_nothrow<IStream> EncodeBitmapToContainer(IWICImagingFactory* pWIC,
                                                      wil::com_ptr_nothrow<IWICBitmapSource> bitmap,
                                                      const GUID& containerGUID,
                                                      const UINT width,
                                                      const UINT height)
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
    encoder->Initialize(encodedBitmap.get(), WICBitmapEncoderNoCache);
    wil::com_ptr_nothrow<IWICBitmapFrameEncode> encodedFrame;
    OK_OR_BAIL(encoder->CreateNewFrame(&encodedFrame, nullptr));
    OK_OR_BAIL(encodedFrame->Initialize(nullptr));

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

    OK_OR_BAIL(MFTEnumEx(MFT_CATEGORY_VIDEO_DECODER,
                         MFT_ENUM_FLAG_SYNCMFT,
                         &inputType,
                         &outputType,
                         &ppVDActivate,
                         &count));

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
    OK_OR_BAIL(MFCreateMediaType(&intermediateFrameMediaType));
    intermediateFrameMediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
    intermediateFrameMediaType->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_MJPG);
    intermediateFrameMediaType->SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive);
    intermediateFrameMediaType->SetUINT32(MF_MT_ALL_SAMPLES_INDEPENDENT, TRUE);
    OK_OR_BAIL(MFSetAttributeSize(intermediateFrameMediaType.get(), MF_MT_FRAME_SIZE, width, height));
    OK_OR_BAIL(MFSetAttributeRatio(intermediateFrameMediaType.get(), MF_MT_PIXEL_ASPECT_RATIO, width, height));
    OK_OR_BAIL(videoDecoder->SetInputType(0, intermediateFrameMediaType.get(), 0));
    OK_OR_BAIL(videoDecoder->SetOutputType(0, outputMediaType, 0));

    // Process the input sample
    OK_OR_BAIL(videoDecoder->ProcessInput(0, inputSample.get(), 0));

    // Check whether we need to allocate output sample and buffer ourselves
    MFT_OUTPUT_STREAM_INFO outputStreamInfo{};
    OK_OR_BAIL(videoDecoder->GetOutputStreamInfo(0, &outputStreamInfo));
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
        IMFMediaBuffer* outputMediaBuffer = nullptr;
        OK_OR_BAIL(MFCreateAlignedMemoryBuffer(outputStreamInfo.cbSize, outputStreamInfo.cbAlignment - 1, &outputMediaBuffer));
        OK_OR_BAIL(outputMediaBuffer->SetCurrentLength(outputStreamInfo.cbSize));
        OK_OR_BAIL(outputSample->AddBuffer(outputMediaBuffer));
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

wil::com_ptr_nothrow<IMFSample> LoadImageAsSample(wil::com_ptr_nothrow<IStream> imageStream,
                                                  IMFMediaType* sampleMediaType) noexcept
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

    // Just copy the raw pixels to the sample buffer w/o using any container
    if (outputType.guidSubtype == MFVideoFormat_RGB24)
    {
        IMFSample* outputSample = nullptr;
        OK_OR_BAIL(MFCreateSample(&outputSample));
        OK_OR_BAIL(outputSample->SetSampleDuration(333333));
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
        return outputSample;
    }

    // Otherwise, create an intermediate jpg container sample which will be transcoded to the target format
    wil::com_ptr_nothrow<IStream> intermediateJpgStream =
        EncodeBitmapToContainer(pWIC, srcImageBitmap, GUID_ContainerFormatJpeg, targetWidth, targetHeight);
    if (!intermediateJpgStream)
    {
        return nullptr;
    }

    // Obtain stream size and lock its memory pointer
    STATSTG intermediateStreamStat{};
    OK_OR_BAIL(intermediateJpgStream->Stat(&intermediateStreamStat, STATFLAG_NONAME));
    const ULONGLONG intermediateStreamSize = intermediateStreamStat.cbSize.QuadPart;
    HGLOBAL streamMemoryHandle{};
    OK_OR_BAIL(GetHGlobalFromStream(intermediateJpgStream.get(), &streamMemoryHandle));

    auto intermediateStreamMemory = static_cast<uint8_t*>(GlobalLock(streamMemoryHandle));
    auto unlockIntermediateStreamMemory =
        wil::scope_exit([intermediateStreamMemory] { GlobalUnlock(intermediateStreamMemory); });

    // Create a sample from the input image buffer
    wil::com_ptr_nothrow<IMFSample> inputSample;
    OK_OR_BAIL(MFCreateSample(&inputSample));

    IMFMediaBuffer* inputMediaBuffer = nullptr;
    OK_OR_BAIL(MFCreateAlignedMemoryBuffer(static_cast<DWORD>(intermediateStreamSize), MF_64_BYTE_ALIGNMENT, &inputMediaBuffer));
    BYTE* inputBuf = nullptr;
    DWORD max_length = 0, current_length = 0;
    OK_OR_BAIL(inputMediaBuffer->Lock(&inputBuf, &max_length, &current_length));
    if (max_length < intermediateStreamSize)
    {
        return nullptr;
    }

    std::copy(intermediateStreamMemory, intermediateStreamMemory + intermediateStreamSize, inputBuf);
    unlockIntermediateStreamMemory.reset();
    OK_OR_BAIL(inputMediaBuffer->Unlock());
    OK_OR_BAIL(inputMediaBuffer->SetCurrentLength(static_cast<DWORD>(intermediateStreamSize)));
    OK_OR_BAIL(inputSample->AddBuffer(inputMediaBuffer));

    // Now we are ready to convert it to the requested media type, so we need to find a suitable jpg encoder
    MFT_REGISTER_TYPE_INFO intermediateType = { MFMediaType_Video, MFVideoFormat_MJPG };

    // But if no conversion is needed, just return the input sample
    if (intermediateType.guidSubtype == outputType.guidSubtype)
    {
        return inputSample;
    }

    return ConvertIMFVideoSample(intermediateType, sampleMediaType, inputSample, targetWidth, targetHeight);
}