#include "VideoCaptureProxyFilter.h"

#include "VideoCaptureDevice.h"
#include <mfidl.h>
#include <Shlwapi.h>
#include <mfapi.h>

constexpr static inline wchar_t FILTER_NAME[] = L"PowerToysVCMProxyFilter";
constexpr static inline wchar_t PIN_NAME[] = L"PowerToysVCMProxyPIN";
constexpr static inline wchar_t VENDOR[] = L"Microsoft Corporation";

namespace
{
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
                                                  IMFMediaType* sampleMediaType) noexcept;

HRESULT VideoCaptureProxyPin::Connect(IPin* pReceivePin, const AM_MEDIA_TYPE*)
{
    VERBOSE_LOG;
    if (!pReceivePin)
    {
        return E_POINTER;
    }

    if (_owningFilter->_state == State_Running)
    {
        return VFW_E_NOT_STOPPED;
    }

    if (_connectedInputPin)
    {
        return VFW_E_ALREADY_CONNECTED;
    }

    if (FAILED(pReceivePin->ReceiveConnection(this, _mediaFormat.get())))
    {
        return E_POINTER;
    }

    _connectedInputPin = pReceivePin;

    auto memInput = _connectedInputPin.try_query<IMemInputPin>();
    if (!memInput)
    {
        return VFW_E_NO_TRANSPORT;
    }
    auto allocator = FindAllocator();
    memInput->NotifyAllocator(allocator.get(), false);

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
    VERBOSE_LOG;
    _connectedInputPin.try_copy_to(pPin);
    return S_OK;
}

HRESULT VideoCaptureProxyPin::ConnectionMediaType(AM_MEDIA_TYPE* pmt)
{
    VERBOSE_LOG;
    if (!_connectedInputPin)
    {
        return VFW_E_NOT_CONNECTED;
    }
    *pmt = *CopyMediaType(_mediaFormat).release();
    return S_OK;
}

HRESULT VideoCaptureProxyPin::QueryPinInfo(PIN_INFO* pInfo)
{
    if (!pInfo)
    {
        return E_POINTER;
    }
    VERBOSE_LOG;
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
        return E_POINTER;
    }
    *pPinDir = PINDIR_OUTPUT;
    return S_OK;
}

HRESULT VideoCaptureProxyPin::QueryId(LPWSTR* Id)
{
    if (!Id)
    {
        return E_POINTER;
    }
    *Id = static_cast<LPWSTR>(CoTaskMemAlloc(sizeof(PIN_NAME)));

    std::copy(std::begin(PIN_NAME), std::end(PIN_NAME), *Id);
    return S_OK;
}

HRESULT VideoCaptureProxyPin::QueryAccept(const AM_MEDIA_TYPE*)
{
    VERBOSE_LOG;
    return S_OK;
}

HRESULT VideoCaptureProxyPin::EnumMediaTypes(IEnumMediaTypes** ppEnum)
{
    if (!ppEnum)
    {
        return E_POINTER;
    }
    VERBOSE_LOG;
    auto enumerator = winrt::make_self<MediaTypeEnumerator>();
    enumerator->_objects.emplace_back(CopyMediaType(_mediaFormat));
    *ppEnum = enumerator.detach();

    return S_OK;
}

HRESULT VideoCaptureProxyPin::QueryInternalConnections(IPin**, ULONG*)
{
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
    VERBOSE_LOG;
    if (pmt == nullptr)
        return S_OK;

    _mediaFormat = CopyMediaType(pmt);
    return S_OK;
}

HRESULT VideoCaptureProxyPin::GetFormat(AM_MEDIA_TYPE** ppmt)
{
    if (!ppmt)
    {
        return E_POINTER;
    }

    *ppmt = CopyMediaType(_mediaFormat).release();
    return S_OK;
}

HRESULT VideoCaptureProxyPin::GetNumberOfCapabilities(int* piCount, int* piSize)
{
    if (!piCount || !piSize)
    {
        return E_POINTER;
    }
    VERBOSE_LOG;
    *piCount = 1;
    *piSize = sizeof(VIDEO_STREAM_CONFIG_CAPS);
    return S_OK;
}

HRESULT VideoCaptureProxyPin::GetStreamCaps(int iIndex, AM_MEDIA_TYPE** ppmt, BYTE* pSCC)
{
    if (!ppmt || !pSCC)
    {
        return E_POINTER;
    }
    if (iIndex != 0)
    {
        return S_FALSE;
    }
    VERBOSE_LOG;
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

    *ppmt = CopyMediaType(_mediaFormat).release();

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
        return E_PROP_SET_UNSUPPORTED;
    }
    if (dwPropID != AMPROPERTY_PIN_CATEGORY)
    {
        return E_PROP_ID_UNSUPPORTED;
    }
    if (!pPropData || !pcbReturned)
    {
        return E_POINTER;
    }
    if (pcbReturned)
    {
        *pcbReturned = sizeof(GUID);
    }
    if (!pPropData)
    {
        return S_OK;
    }
    if (cbPropData < sizeof(GUID))
    {
        return E_UNEXPECTED;
    }
    VERBOSE_LOG;
    *(GUID*)pPropData = PIN_CATEGORY_CAPTURE;
    return S_OK;
}

HRESULT VideoCaptureProxyPin::QuerySupported(REFGUID guidPropSet, DWORD dwPropID, DWORD* pTypeSupport)
{
    if (guidPropSet != AMPROPSETID_Pin)
    {
        return E_PROP_SET_UNSUPPORTED;
    }
    if (dwPropID != AMPROPERTY_PIN_CATEGORY)
    {
        return E_PROP_ID_UNSUPPORTED;
    }
    if (pTypeSupport)
    {
        *pTypeSupport = KSPROPERTY_SUPPORT_GET;
    }
    return S_OK;
}

void OverwriteFrame(IMediaSample* frame, wil::com_ptr_nothrow<IMFSample>& image)
{
    if (!image)
    {
        return;
    }
    BYTE* data = nullptr;
    frame->GetPointer(&data);
    if (!data)
    {
        LOG("Couldn't get sample pointer");
        return;
    }
    wil::com_ptr_nothrow<IMFMediaBuffer> buf;
    const long nBytes = frame->GetSize();

    image->GetBufferByIndex(0, &buf);
    BYTE* inputBuf = nullptr;
    DWORD max_length = 0, current_length = 0;
    buf->Lock(&inputBuf, &max_length, &current_length);
    std::copy(inputBuf, inputBuf + current_length, data);
    buf->Unlock();
}

VideoCaptureProxyFilter::VideoCaptureProxyFilter() :
    _worker_thread{ std::thread{ [this]() {
        using namespace std::chrono_literals;
        const auto uninitializedSleepInterval = 15ms;
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
            const auto newSettings = SyncCurrentSettings();
            if (newSettings.webcamDisabled)
            {
                OverwriteFrame(_pending_frame, _overlayImage ? _overlayImage : _blankImage);
            }
            _pending_frame = nullptr;
            input->Receive(sample);
            sample->Release();
        }
    } } }
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
    VERBOSE_LOG;
    if (_state == State_Stopped)
    {
        std::unique_lock<std::mutex> lock{ _worker_mutex };

        if (!_outPin)
        {
            return VFW_E_NO_TRANSPORT;
        }

        auto allocator = _outPin->FindAllocator();
        if (!allocator)
        {
            return VFW_E_NO_TRANSPORT;
        }
        allocator->Commit();
    }

    _state = State_Paused;
    return S_OK;
}

HRESULT VideoCaptureProxyFilter::Run(REFERENCE_TIME)
{
    VERBOSE_LOG;
    _state = State_Running;
    if (_captureDevice)
    {
        _captureDevice->StartCapture();
    }

    return S_OK;
}

HRESULT VideoCaptureProxyFilter::GetState(DWORD, FILTER_STATE* State)
{
    VERBOSE_LOG;
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
    _clock.try_copy_to(pClock);
    return S_OK;
}

HRESULT VideoCaptureProxyFilter::EnumPins(IEnumPins** ppEnum)
{
    VERBOSE_LOG;
    if (!ppEnum)
    {
        LOG("EnumPins: null arg provided!");
        return E_POINTER;
    }
    std::unique_lock<std::mutex> lock{ _worker_mutex };

    // We cannot initialize capture device and outpin during VideoCaptureProxyFilter ctor
    // since that results in a deadlock. Do it now.
    if (!_outPin)
    {
        LOG("Started pin initialization");
        const auto newSettings = SyncCurrentSettings();
        std::vector<VideoCaptureDeviceInfo> webcams;
        webcams = VideoCaptureDevice::ListAll();
        if (webcams.empty())
        {
            LOG("No physical webcams found");
            return S_OK;
        }
        std::optional<size_t> selectedCamIdx;
        for (size_t i = 0; i < size(webcams); ++i)
        {
            if (newSettings.newCameraName == webcams[i].friendlyName)
            {
                selectedCamIdx = i;
                LOG("Webcam selected using settings");
                break;
            }
        }
        if (!selectedCamIdx)
        {
            for (size_t i = 0; i < size(webcams); ++i)
            {
                if (newSettings.newCameraName != CAMERA_NAME)
                {
                    LOG("Webcam selected using first fit");
                    selectedCamIdx = i;
                    break;
                }
            }
        }
        if (!selectedCamIdx)
        {
            LOG("Webcam couldn't be selected");
            return S_OK;
        }
        auto& webcam = webcams[*selectedCamIdx];

        auto pin = winrt::make_self<VideoCaptureProxyPin>();
        pin->_mediaFormat = CopyMediaType(webcam.bestFormat.mediaType);
        pin->_owningFilter = this;
        _outPin.attach(pin.detach());

        wil::com_ptr_nothrow<IMFMediaType> targetMediaType;
        MFCreateMediaType(&targetMediaType);
        targetMediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
        targetMediaType->SetGUID(MF_MT_SUBTYPE, webcam.bestFormat.mediaType->subtype);
        targetMediaType->SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive);
        targetMediaType->SetUINT32(MF_MT_ALL_SAMPLES_INDEPENDENT, TRUE);
        MFSetAttributeSize(
            targetMediaType.get(), MF_MT_FRAME_SIZE, webcam.bestFormat.width, webcam.bestFormat.height);
        MFSetAttributeRatio(targetMediaType.get(), MF_MT_PIXEL_ASPECT_RATIO, 1, 1);

        if (!_blankImage)
        {
            wil::com_ptr_nothrow<IStream> blackBMPImage = SHCreateMemStream(bmpPixelData, sizeof(bmpPixelData));
            _blankImage = LoadImageAsSample(blackBMPImage, targetMediaType.get());
        }
        if (newSettings.overlayImage && !_overlayImage)
        {
            _overlayImage = LoadImageAsSample(newSettings.overlayImage, targetMediaType.get());
        }
        LOG("Loaded images");
        auto frameCallback = [this](IMediaSample* sample) {
            std::unique_lock<std::mutex> lock{ _worker_mutex };
            sample->AddRef();
            _pending_frame = sample;
            _worker_cv.notify_one();
        };

        _captureDevice = VideoCaptureDevice::Create(std::move(webcam), std::move(frameCallback));
        if (_captureDevice)
        {
            LOG("Capture device created successfully");
        }
        else
        {
            LOG("Couldn't create capture device");
        }
    }

    auto enumerator = winrt::make_self<ObjectEnumerator<IPin, IEnumPins>>();
    enumerator->_objects.emplace_back(_outPin);
    *ppEnum = enumerator.detach();
    return S_OK;
}

HRESULT VideoCaptureProxyFilter::FindPin(LPCWSTR, IPin**)
{
    return E_NOTIMPL;
}

HRESULT VideoCaptureProxyFilter::QueryFilterInfo(FILTER_INFO* pInfo)
{
    if (!pInfo)
    {
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
