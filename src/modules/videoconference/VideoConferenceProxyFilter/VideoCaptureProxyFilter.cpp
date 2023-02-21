#include "VideoCaptureProxyFilter.h"

#include "VideoCaptureDevice.h"
#include <mfidl.h>
#include <Shlwapi.h>
#include <mfapi.h>
#include <fstream>

constexpr static inline wchar_t FILTER_NAME[] = L"PowerToysVCMProxyFilter";
constexpr static inline wchar_t PIN_NAME[] = L"PowerToysVCMProxyPIN";
constexpr static inline wchar_t VENDOR[] = L"Microsoft Corporation";

namespace
{
    constexpr float initialJpgQuality = 0.5f;
    constexpr std::array<unsigned char, 3> overlayColor = { 0, 0, 0 };
    // clang-format off
    unsigned char bmpPixelData[58] = {
	      0x42, 0x4D, 0x3A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x36, 0x00,
	      0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00,
	      0x00, 0x00, 0x01, 0x00, 0x18, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00,
	      0x00, 0x00, 0xC4, 0x0E, 0x00, 0x00, 0xC4, 0x0E, 0x00, 0x00, 0x00, 0x00,
	      0x00, 0x00, 0x00, 0x00, 0x00, 0x00, overlayColor[0], overlayColor[1], overlayColor[2], 0x00
    };
    // clang-format on
}

wil::com_ptr_nothrow<IMemAllocator> VideoCaptureProxyPin::FindAllocator()
{
    auto allocator = GetPinAllocator(_connectedInputPin);
    if (!allocator && _owningFilter->_captureDevice)
    {
        allocator = _owningFilter->_captureDevice->_allocator;
    }

    return allocator;
}

wil::com_ptr_nothrow<IMFSample> LoadImageAsSample(wil::com_ptr_nothrow<IStream> imageStream,
                                                  IMFMediaType* sampleMediaType,
                                                  const float quality) noexcept;
bool ReencodeJPGImage(BYTE* imageBuf, const DWORD imageSize, DWORD& reencodedSize);

HRESULT VideoCaptureProxyPin::Connect(IPin* pReceivePin, const AM_MEDIA_TYPE*)
{
    if (!pReceivePin)
    {
        LOG("VideoCaptureProxyPin::Connect FAILED pReceivePin");
        return E_POINTER;
    }

    if (_owningFilter->_state == State_Running)
    {
        LOG("VideoCaptureProxyPin::Connect FAILED _owningFilter->_state");
        return VFW_E_NOT_STOPPED;
    }

    if (_connectedInputPin)
    {
        LOG("VideoCaptureProxyPin::Connect FAILED _connectedInputPin");
        return VFW_E_ALREADY_CONNECTED;
    }

    if (FAILED(pReceivePin->ReceiveConnection(this, _mediaFormat.get())))
    {
        LOG("VideoCaptureProxyPin::Connect FAILED pReceivePin->ReceiveConnection");
        return E_POINTER;
    }

    _connectedInputPin = pReceivePin;

    auto memInput = _connectedInputPin.try_query<IMemInputPin>();
    if (!memInput)
    {
        LOG("VideoCaptureProxyPin::Connect FAILED _connectedInputPin.try_query");
        return VFW_E_NO_TRANSPORT;
    }

    auto allocator = FindAllocator();
    if (allocator == nullptr)
    {
        LOG("VideoCaptureProxyPin::Connect FAILED FindAllocator");
        return VFW_E_NO_TRANSPORT;
    }

    if (FAILED(memInput->NotifyAllocator(allocator.get(), false)))
    {
        LOG("VideoCaptureProxyPin::Connect FAILED memInput->NotifyAllocator");
        return VFW_E_NO_TRANSPORT;
    }

    return S_OK;
}

HRESULT VideoCaptureProxyPin::ReceiveConnection(IPin*, const AM_MEDIA_TYPE*)
{
    return S_OK;
}

HRESULT VideoCaptureProxyPin::Disconnect(void)
{
    if (!_connectedInputPin)
    {
        LOG("VideoCaptureProxyPin::Disconnect FAILED _connectedInputPin");
        return S_FALSE;
    }

    _connectedInputPin.reset();
    return S_OK;
}

HRESULT VideoCaptureProxyPin::ConnectedTo(IPin** pPin)
{
    if (!_connectedInputPin)
    {
        *pPin = nullptr;
        return VFW_E_NOT_CONNECTED;
    }

    return _connectedInputPin.try_copy_to(pPin) ? S_OK : E_FAIL;
}

HRESULT VideoCaptureProxyPin::ConnectionMediaType(AM_MEDIA_TYPE* pmt)
{
    if (!_connectedInputPin)
    {
        LOG("VideoCaptureProxyPin::ConnectionMediaType FAILED _connectedInputPin");
        return VFW_E_NOT_CONNECTED;
    }

    *pmt = *CopyMediaType(_mediaFormat.get()).release();
    return S_OK;
}

HRESULT VideoCaptureProxyPin::QueryPinInfo(PIN_INFO* pInfo)
{
    if (!pInfo)
    {
        LOG("VideoCaptureProxyPin::QueryPinInfo FAILED pInfo");
        return E_POINTER;
    }

    pInfo->pFilter = _owningFilter;
    if (_owningFilter)
    {
        _owningFilter->AddRef();
    }

    if (_mediaFormat->majortype == MEDIATYPE_Video)
    {
        std::copy(std::begin(PIN_NAME), std::end(PIN_NAME), pInfo->achName);
    }

    pInfo->dir = PINDIR_OUTPUT;
    return S_OK;
}

HRESULT VideoCaptureProxyPin::QueryDirection(PIN_DIRECTION* pPinDir)
{
    if (!pPinDir)
    {
        LOG("VideoCaptureProxyPin::QueryDirection FAILED pPinDir");
        return E_POINTER;
    }

    *pPinDir = PINDIR_OUTPUT;
    return S_OK;
}

HRESULT VideoCaptureProxyPin::QueryId(LPWSTR* Id)
{
    if (!Id)
    {
        LOG("VideoCaptureProxyPin::QueryId FAILED Id");
        return E_POINTER;
    }

    *Id = static_cast<LPWSTR>(CoTaskMemAlloc(sizeof(PIN_NAME)));
    std::copy(std::begin(PIN_NAME), std::end(PIN_NAME), *Id);
    return S_OK;
}

HRESULT VideoCaptureProxyPin::QueryAccept(const AM_MEDIA_TYPE*)
{
    return S_OK;
}

HRESULT VideoCaptureProxyPin::EnumMediaTypes(IEnumMediaTypes** ppEnum)
{
    if (!ppEnum)
    {
        LOG("VideoCaptureProxyPin::EnumMediaTypes FAILED ppEnum");
        return E_POINTER;
    }

    *ppEnum = nullptr;

    auto enumerator = winrt::make_self<MediaTypeEnumerator>();
    enumerator->_objects.emplace_back(CopyMediaType(_mediaFormat.get()));
    *ppEnum = enumerator.detach();

    return S_OK;
}

HRESULT VideoCaptureProxyPin::QueryInternalConnections(IPin** pins, ULONG*)
{
    if (pins)
    {
        *pins = nullptr;
    }
    return E_NOTIMPL;
}

HRESULT VideoCaptureProxyPin::EndOfStream(void)
{
    return S_OK;
}

HRESULT VideoCaptureProxyPin::BeginFlush(void)
{
    _flushing = true;
    return S_OK;
}

HRESULT VideoCaptureProxyPin::EndFlush(void)
{
    _flushing = false;
    return S_OK;
}

HRESULT VideoCaptureProxyPin::NewSegment(REFERENCE_TIME, REFERENCE_TIME, double)
{
    return S_OK;
}

HRESULT VideoCaptureProxyPin::SetFormat(AM_MEDIA_TYPE* pmt)
{
    if (pmt == nullptr)
    {
        return S_OK;
    }

    _mediaFormat = CopyMediaType(pmt);
    return S_OK;
}

HRESULT VideoCaptureProxyPin::GetFormat(AM_MEDIA_TYPE** ppmt)
{
    if (!ppmt)
    {
        LOG("VideoCaptureProxyPin::GetFormat FAILED ppmt");
        return E_POINTER;
    }
    *ppmt = CopyMediaType(_mediaFormat.get()).release();
    return S_OK;
}

HRESULT VideoCaptureProxyPin::GetNumberOfCapabilities(int* piCount, int* piSize)
{
    if (!piCount || !piSize)
    {
        LOG("VideoCaptureProxyPin::GetNumberOfCapabilities FAILED piCount || piSize");
        return E_POINTER;
    }

    *piCount = 1;
    *piSize = sizeof(VIDEO_STREAM_CONFIG_CAPS);
    return S_OK;
}

HRESULT VideoCaptureProxyPin::GetStreamCaps(int iIndex, AM_MEDIA_TYPE** ppmt, BYTE* pSCC)
{
    if (!ppmt || !pSCC)
    {
        LOG("VideoCaptureProxyPin::GetStreamCaps FAILED ppmt || pSCC");
        return E_POINTER;
    }

    if (iIndex != 0)
    {
        LOG("VideoCaptureProxyPin::GetStreamCaps FAILED iIndex");
        return S_FALSE;
    }

    VIDEOINFOHEADER* vih = reinterpret_cast<decltype(vih)>(_mediaFormat->pbFormat);

    VIDEO_STREAM_CONFIG_CAPS caps{};
    caps.guid = FORMAT_VideoInfo;
    caps.MinFrameInterval = vih->AvgTimePerFrame;
    caps.MaxFrameInterval = vih->AvgTimePerFrame;
    caps.MinOutputSize.cx = vih->bmiHeader.biWidth;
    caps.MinOutputSize.cy = vih->bmiHeader.biHeight;
    caps.MaxOutputSize = caps.MinOutputSize;
    caps.InputSize = caps.MinOutputSize;
    caps.MinCroppingSize = caps.MinOutputSize;
    caps.MaxCroppingSize = caps.MinOutputSize;
    caps.CropGranularityX = vih->bmiHeader.biWidth;
    caps.CropGranularityY = vih->bmiHeader.biHeight;
    caps.MinBitsPerSecond = vih->dwBitRate;
    caps.MaxBitsPerSecond = caps.MinBitsPerSecond;

    *ppmt = CopyMediaType(_mediaFormat.get()).release();

    const auto caps_begin = reinterpret_cast<const char*>(&caps);
    std::copy(caps_begin, caps_begin + sizeof(caps), pSCC);
    return S_OK;
}

HRESULT VideoCaptureProxyPin::Set(REFGUID, DWORD, LPVOID, DWORD, LPVOID, DWORD)
{
    return E_NOTIMPL;
}

HRESULT VideoCaptureProxyPin::Get(
    REFGUID guidPropSet,
    DWORD dwPropID,
    LPVOID,
    DWORD,
    LPVOID pPropData,
    DWORD cbPropData,
    DWORD* pcbReturned)
{
    if (guidPropSet != AMPROPSETID_Pin)
    {
        LOG("VideoCaptureProxyPin::Get FAILED guidPropSet");
        return E_PROP_SET_UNSUPPORTED;
    }

    if (dwPropID != AMPROPERTY_PIN_CATEGORY)
    {
        LOG("VideoCaptureProxyPin::Get FAILED dwPropID");
        return E_PROP_ID_UNSUPPORTED;
    }

    if (!pPropData)
    {
        LOG("VideoCaptureProxyPin::Get FAILED pPropData || pcbReturned");
        return E_POINTER;
    }

    if (pcbReturned)
    {
        *pcbReturned = sizeof(GUID);
    }

    if (cbPropData < sizeof(GUID))
    {
        LOG("VideoCaptureProxyPin::Get FAILED cbPropData");
        return E_UNEXPECTED;
    }

    *static_cast<GUID*>(pPropData) = PIN_CATEGORY_CAPTURE;

    LOG("VideoCaptureProxyPin::Get SUCCESS");
    return S_OK;
}

HRESULT VideoCaptureProxyPin::QuerySupported(REFGUID guidPropSet, DWORD dwPropID, DWORD* pTypeSupport)
{
    if (guidPropSet != AMPROPSETID_Pin)
    {
        LOG("VideoCaptureProxyPin::QuerySupported FAILED guidPropSet");
        return E_PROP_SET_UNSUPPORTED;
    }

    if (dwPropID != AMPROPERTY_PIN_CATEGORY)
    {
        LOG("VideoCaptureProxyPin::QuerySupported FAILED dwPropID");
        return E_PROP_ID_UNSUPPORTED;
    }

    if (pTypeSupport)
    {
        *pTypeSupport = KSPROPERTY_SUPPORT_GET;
    }

    return S_OK;
}

long GetImageSize(wil::com_ptr_nothrow<IMFSample>& image)
{
    if (!image)
    {
        return 0;
    }

    DWORD imageSize = 0;
    wil::com_ptr_nothrow<IMFMediaBuffer> imageBuf;

    OK_OR_BAIL(image->GetBufferByIndex(0, &imageBuf));
    OK_OR_BAIL(imageBuf->GetCurrentLength(&imageSize));
    return imageSize;
}

void ReencodeFrame(IMediaSample* frame)
{
    BYTE* frameData = nullptr;
    frame->GetPointer(&frameData);
    if (!frameData)
    {
        LOG("VideoCaptureProxyPin::ReencodeFrame FAILED frameData");
        return;
    }
    const DWORD frameSize = frame->GetSize();
    DWORD reencodedSize = 0;
    if (!ReencodeJPGImage(frameData, frameSize, reencodedSize))
    {
        LOG("VideoCaptureProxyPin::ReencodeJPGImage FAILED");
        return;
    }
    frame->SetActualDataLength(reencodedSize);
}

bool OverwriteFrame(IMediaSample* frame, wil::com_ptr_nothrow<IMFSample>& image)
{
    if (!image)
    {
        return false;
    }

    BYTE* frameData = nullptr;
    frame->GetPointer(&frameData);
    if (!frameData)
    {
        LOG("VideoCaptureProxyPin::OverwriteFrame FAILED frameData");
        return false;
    }

    wil::com_ptr_nothrow<IMFMediaBuffer> imageBuf;
    const DWORD frameSize = frame->GetSize();

    image->GetBufferByIndex(0, &imageBuf);
    if (!imageBuf)
    {
        LOG("VideoCaptureProxyPin::OverwriteFrame FAILED imageBuf");
        return false;
    }

    BYTE* imageData = nullptr;
    DWORD _ = 0, imageSize = 0;
    imageBuf->Lock(&imageData, &_, &imageSize);
    if (!imageData)
    {
        LOG("VideoCaptureProxyPin::OverwriteFrame FAILED imageData");
        return false;
    }

    if (imageSize > frameSize && failed(frame->SetActualDataLength(imageSize)))
    {
        char buf[512]{};
        sprintf_s(buf, "VideoCaptureProxyPin::OverwriteFrame FAILED overlay image size %lu is larger than frame size %lu", imageSize, frameSize);
        LOG(buf);
        imageBuf->Unlock();
        return false;
    }

    std::copy(imageData, imageData + imageSize, frameData);
    imageBuf->Unlock();
    frame->SetActualDataLength(imageSize);

    return true;
}

//#define DEBUG_FRAME_DATA
//#define DEBUG_OVERWRITE_FRAME
//#define DEBUG_REENCODE_JPG_DATA

#if defined(DEBUG_OVERWRITE_FRAME)
void DebugOverwriteFrame(IMediaSample* frame, std::string_view filepath)
{
    std::ifstream file{ filepath.data(), std::ios::binary };
    std::streampos fileSize = 0;
    fileSize = file.tellg();
    file.seekg(0, std::ios::end);
    fileSize = file.tellg() - fileSize;

    BYTE* frameData = nullptr;
    if (!frame)
    {
        LOG("null frame provided");
        return;
    }
    frame->GetPointer(&frameData);
    const DWORD frameSize = frame->GetSize();

    if (fileSize > frameSize || !frameData)
    {
        LOG("frame can't be filled with data");
        return;
    }
    file.read((char*)frameData, fileSize);
    frame->SetActualDataLength((long)fileSize);
    LOG("DebugOverwriteFrame success");
}

#endif

#if defined(DEBUG_FRAME_DATA)
#include <filesystem>

namespace fs = std::filesystem;

void DumpSample(IMediaSample* frame, const std::string_view filename)
{
    BYTE* data = nullptr;
    frame->GetPointer(&data);
    if (!data)
    {
        LOG("Couldn't get sample pointer");
        return;
    }
    const long nBytes = frame->GetActualDataLength();
    std::ofstream file{ fs::temp_directory_path() / filename, std::ios::binary };
    file.write((const char*)data, nBytes);
}
#endif

VideoCaptureProxyFilter::VideoCaptureProxyFilter() :
    _worker_thread{
        std::thread{
            [this]() {
                using namespace std::chrono_literals;
                const auto uninitializedSleepInterval = 15ms;
                std::vector<float> lowerJpgQualityModes = { 0.1f, 0.25f };
                while (!_shutdown_request)
                {
                    std::unique_lock<std::mutex> lock{ _worker_mutex };
                    _worker_cv.wait(lock, [this] { return _pending_frame != nullptr || _shutdown_request; });

                    if (!_outPin || !_outPin->_connectedInputPin)
                    {
                        lock.unlock();
                        std::this_thread::sleep_for(uninitializedSleepInterval);
                        continue;
                    }

                    auto input = _outPin->_connectedInputPin.try_query<IMemInputPin>();
                    if (!input)
                    {
                        continue;
                    }

                    IMediaSample* sample = _pending_frame;
                    if (!sample)
                    {
                        continue;
                    }
#if defined(DEBUG_FRAME_DATA)
                    static bool realFrameSaved = false;
                    if (!realFrameSaved)
                    {
                        DumpSample(sample, "PowerToysVCMRealFrame.binary");
                        realFrameSaved = true;
                    }
#endif
                    auto newSettings = SyncCurrentSettings();
                    if (newSettings.webcamDisabled)
                    {
#if !defined(DEBUG_OVERWRITE_FRAME)
                        bool overwritten = OverwriteFrame(_pending_frame, _overlayImage ? _overlayImage : _blankImage);
                        while (!overwritten && _overlayImage)
                        {
                            _overlayImage.reset();
                            newSettings = SyncCurrentSettings();
                            if (!lowerJpgQualityModes.empty() && newSettings.overlayImage)
                            {
                                const float quality = lowerJpgQualityModes.back();
                                lowerJpgQualityModes.pop_back();
                                char buf[512]{};
                                sprintf_s(buf, "Reload overlay image with quality %f", quality);
                                LOG(buf);
                                _overlayImage = LoadImageAsSample(newSettings.overlayImage, _targetMediaType.get(), quality);
                                overwritten = OverwriteFrame(_pending_frame, _overlayImage);
                            }
                            else
                            {
                                LOG("Couldn't overwrite frame with image with all available quality modes.");
                            }
                        }
#if defined(DEBUG_FRAME_DATA)
                        static bool overlayFrameSaved = false;
                        if (!overlayFrameSaved && _overlayImage && overwritten)
                        {
                            DumpSample(sample, "PowerToysVCMOverlayImageFrame.binary");
                            overlayFrameSaved = true;
                        }
#endif
                        if (!overwritten && !_overlayImage)
                        {
                            OverwriteFrame(_pending_frame, _blankImage);
                        }
#else
                        DebugOverwriteFrame(_pending_frame, "R:\\frame.data");
#endif
                    }
#if defined(DEBUG_REENCODE_JPG_DATA)
                    else
                    {
                        GUID subtype{};
                        _targetMediaType->GetGUID(MF_MT_SUBTYPE, &subtype);
                        if (subtype == MFVideoFormat_MJPG)
                        {
                            ReencodeFrame(_pending_frame);
                        }
                    }
#endif

                    _pending_frame = nullptr;
                    input->Receive(sample);
                    sample->Release();
                }
            } }
    }
{
}

HRESULT VideoCaptureProxyFilter::Stop(void)
{
    if (_state != State_Stopped && _captureDevice)
    {
        _captureDevice->StopCapture();
    }

    _state = State_Stopped;
    return S_OK;
}

HRESULT VideoCaptureProxyFilter::Pause(void)
{
    if (_state == State_Stopped)
    {
        std::unique_lock<std::mutex> lock{ _worker_mutex };

        if (!_outPin)
        {
            LOG("VideoCaptureProxyPin::Pause FAILED _outPin");
            return VFW_E_NO_TRANSPORT;
        }

        auto allocator = _outPin->FindAllocator();
        if (!allocator)
        {
            LOG("VideoCaptureProxyPin::Pause FAILED allocator");
            return VFW_E_NO_TRANSPORT;
        }

        allocator->Commit();
    }

    _state = State_Paused;
    return S_OK;
}

HRESULT VideoCaptureProxyFilter::Run(REFERENCE_TIME)
{
    _state = State_Running;
    if (_captureDevice)
    {
        _captureDevice->StartCapture();
    }

    return S_OK;
}

HRESULT VideoCaptureProxyFilter::GetState(DWORD, FILTER_STATE* State)
{
    *State = _state;
    return S_OK;
}

HRESULT VideoCaptureProxyFilter::SetSyncSource(IReferenceClock* pClock)
{
    _clock = pClock;
    return S_OK;
}

HRESULT VideoCaptureProxyFilter::GetSyncSource(IReferenceClock** pClock)
{
    if (!pClock)
    {
        return E_POINTER;
    }
    *pClock = nullptr;
    return _clock.try_copy_to(pClock) ? S_OK : E_FAIL;
}

GUID MapDShowSubtypeToMFT(const GUID& dshowSubtype)
{
    if (dshowSubtype == MEDIASUBTYPE_YUY2)
    {
        return MFVideoFormat_YUY2;
    }
    else if (dshowSubtype == MEDIASUBTYPE_MJPG)
    {
        return MFVideoFormat_MJPG;
    }
    else if (dshowSubtype == MEDIASUBTYPE_RGB24)
    {
        return MFVideoFormat_RGB24;
    }
    else
    {
        LOG("MapDShowSubtypeToMFT: Unsupported media type format provided!");
        return MFVideoFormat_MJPG;
    }
}

HRESULT VideoCaptureProxyFilter::EnumPins(IEnumPins** ppEnum)
{
    if (!ppEnum)
    {
        LOG("VideoCaptureProxyFilter::EnumPins null arg provided");
        return E_POINTER;
    }
    *ppEnum = nullptr;
    auto enumerator = winrt::make_self<ObjectEnumerator<IPin, IEnumPins>>();
    auto detached_enumerator = enumerator.detach();
    *ppEnum = detached_enumerator;

    std::unique_lock<std::mutex> lock{ _worker_mutex };

    // We cannot initialize capture device and outpin during VideoCaptureProxyFilter ctor
    // since that results in a deadlock -> initializing now.
    if (!_outPin)
    {
        LOG("VideoCaptureProxyFilter::EnumPins started pin initialization");
        const auto newSettings = SyncCurrentSettings();
        std::vector<VideoCaptureDeviceInfo> webcams;
        webcams = VideoCaptureDevice::ListAll();
        if (webcams.empty())
        {
            LOG("VideoCaptureProxyFilter::EnumPins no physical webcams found");
            return E_FAIL;
        }

        std::optional<size_t> selectedCamIdx;
        for (size_t i = 0; i < size(webcams); ++i)
        {
            if (newSettings.newCameraName == webcams[i].friendlyName)
            {
                selectedCamIdx = i;
                LOG("VideoCaptureProxyFilter::EnumPins webcam selected using settings");
                break;
            }
        }

        if (!selectedCamIdx)
        {
            for (size_t i = 0; i < size(webcams); ++i)
            {
                if (newSettings.newCameraName != CAMERA_NAME)
                {
                    LOG("VideoCaptureProxyFilter::EnumPins webcam selected using first fit");
                    selectedCamIdx = i;
                    break;
                }
            }
        }

        if (!selectedCamIdx)
        {
            LOG("VideoCaptureProxyFilter::EnumPins FAILED webcam couldn't be selected");
            return E_FAIL;
        }

        auto& webcam = webcams[*selectedCamIdx];
        auto pin = winrt::make_self<VideoCaptureProxyPin>();
        pin->_mediaFormat = CopyMediaType(webcam.bestFormat.mediaType.get());
        pin->_owningFilter = this;
        _outPin.attach(pin.detach());

        auto frameCallback = [this](IMediaSample* sample) {
            std::unique_lock<std::mutex> lock{ _worker_mutex };
            sample->AddRef();
            _pending_frame = sample;
            _worker_cv.notify_one();
        };

        _targetMediaType.reset();
        MFCreateMediaType(&_targetMediaType);
        _targetMediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
        _targetMediaType->SetGUID(MF_MT_SUBTYPE, MapDShowSubtypeToMFT(webcam.bestFormat.mediaType->subtype));
        _targetMediaType->SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive);
        _targetMediaType->SetUINT32(MF_MT_ALL_SAMPLES_INDEPENDENT, TRUE);
        MFSetAttributeSize(
            _targetMediaType.get(), MF_MT_FRAME_SIZE, webcam.bestFormat.width, webcam.bestFormat.height);
        MFSetAttributeRatio(_targetMediaType.get(), MF_MT_PIXEL_ASPECT_RATIO, 1, 1);

        _captureDevice = VideoCaptureDevice::Create(std::move(webcam), std::move(frameCallback));
        if (_captureDevice)
        {
            if (!_blankImage)
            {
                wil::com_ptr_nothrow<IStream> blackBMPImage = SHCreateMemStream(bmpPixelData, sizeof(bmpPixelData));
                _blankImage = LoadImageAsSample(blackBMPImage, _targetMediaType.get(), initialJpgQuality);
            }

            _overlayImage = LoadImageAsSample(newSettings.overlayImage, _targetMediaType.get(), initialJpgQuality);
            LOG("VideoCaptureProxyFilter::EnumPins capture device created successfully");
        }
        else
        {
            LOG("VideoCaptureProxyFilter::EnumPins FAILED couldn't create capture device");
        }
    }

    detached_enumerator->_objects.emplace_back(_outPin);

    return S_OK;
}

HRESULT VideoCaptureProxyFilter::FindPin(LPCWSTR, IPin** pin)
{
    if (pin)
    {
        *pin = nullptr;
    }
    return E_NOTIMPL;
}

HRESULT VideoCaptureProxyFilter::QueryFilterInfo(FILTER_INFO* pInfo)
{
    if (!pInfo)
    {
        LOG("VideoCaptureProxyPin::QueryFilterInfo FAILED pInfo");
        return E_POINTER;
    }

    VERBOSE_LOG;
    std::copy(std::begin(FILTER_NAME), std::end(FILTER_NAME), pInfo->achName);

    pInfo->pGraph = _graph;
    if (_graph)
    {
        _graph->AddRef();
    }

    return S_OK;
}

HRESULT VideoCaptureProxyFilter::JoinFilterGraph(IFilterGraph* pGraph, LPCWSTR)
{
    _graph = pGraph;
    return S_OK;
}

HRESULT VideoCaptureProxyFilter::QueryVendorInfo(LPWSTR* pVendorInfo)
{
    auto info = static_cast<LPWSTR>(CoTaskMemAlloc(sizeof(VENDOR)));
    std::copy(std::begin(VENDOR), std::end(VENDOR), info);
    *pVendorInfo = info;
    return S_OK;
}

HRESULT VideoCaptureProxyFilter::GetClassID(CLSID*)
{
    return E_NOTIMPL;
}

ULONG VideoCaptureProxyFilter::GetMiscFlags(void)
{
    return AM_FILTER_MISC_FLAGS_IS_SOURCE;
}

VideoCaptureProxyFilter::~VideoCaptureProxyFilter()
{
    VERBOSE_LOG;
    _shutdown_request = true;

    _worker_cv.notify_one();
    _worker_thread.join();
    if (_settingsUpdateChannel)
    {
        _settingsUpdateChannel->access([](auto settingsMemory) {
            auto settings = reinterpret_cast<CameraSettingsUpdateChannel*>(settingsMemory._data);
            settings->cameraInUse = false;
        });
    }
}

VideoCaptureProxyFilter::SyncedSettings VideoCaptureProxyFilter::SyncCurrentSettings()
{
    SyncedSettings result;
    if (!_settingsUpdateChannel.has_value())
    {
        _settingsUpdateChannel = SerializedSharedMemory::open(CameraSettingsUpdateChannel::endpoint(), sizeof(CameraSettingsUpdateChannel), false);
    }

    if (!_settingsUpdateChannel)
    {
        return result;
    }

    _settingsUpdateChannel->access([this, &result](auto settingsMemory) {
        auto settings = reinterpret_cast<CameraSettingsUpdateChannel*>(settingsMemory._data);
        bool cameraNameUpdated = false;
        result.webcamDisabled = settings->useOverlayImage;

        settings->cameraInUse = true;

        if (settings->sourceCameraName.has_value())
        {
            std::wstring_view newCameraNameView{ settings->sourceCameraName->data() };
            if (!_currentSourceCameraName.has_value() || *_currentSourceCameraName != newCameraNameView)
            {
                cameraNameUpdated = true;
                result.newCameraName = newCameraNameView;
            }
        }

        if (!settings->overlayImageSize.has_value())
        {
            return;
        }

        if (settings->newOverlayImagePosted || !_overlayImage)
        {
            auto imageChannel =
                SerializedSharedMemory::open(CameraOverlayImageChannel::endpoint(), *settings->overlayImageSize, true);
            if (!imageChannel)
            {
                return;
            }

            imageChannel->access([this, settings, &result](auto imageMemory) {
                result.overlayImage = SHCreateMemStream(imageMemory._data, static_cast<UINT>(imageMemory._size));
                if (!result.overlayImage)
                {
                    return;
                }

                settings->newOverlayImagePosted = false;
            });
        }
    });
    return result;
}
