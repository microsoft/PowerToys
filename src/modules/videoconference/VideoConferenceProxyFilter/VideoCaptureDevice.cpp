#include "Logging.h"
#include "VideoCaptureDevice.h"

#include <wil/resource.h>
#include <cguid.h>

struct VideoCaptureReceiverFilter : winrt::implements<VideoCaptureReceiverFilter, IBaseFilter, IAMFilterMiscFlags>
{
    FILTER_STATE _state = State_Stopped;
    IFilterGraph* _graph = nullptr;
    wil::com_ptr_nothrow<IPin> _videoReceiverPin;

    ULONG STDMETHODCALLTYPE GetMiscFlags() override { return AM_FILTER_MISC_FLAGS_IS_RENDERER; }

    HRESULT STDMETHODCALLTYPE GetClassID(CLSID*) override { return E_NOTIMPL; }

    HRESULT STDMETHODCALLTYPE Stop() override
    {
        _state = State_Stopped;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE Pause() override
    {
        _state = State_Paused;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE Run(REFERENCE_TIME) override
    {
        _state = State_Running;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE GetState(DWORD, FILTER_STATE* outState) override
    {
        *outState = _state;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE GetSyncSource(IReferenceClock** outRefClock) override
    {
        *outRefClock = nullptr;
        return NOERROR;
    }

    HRESULT STDMETHODCALLTYPE SetSyncSource(IReferenceClock*) override { return S_OK; }

    HRESULT STDMETHODCALLTYPE EnumPins(IEnumPins** ppEnum) override
    {
        auto enumerator = winrt::make_self<ObjectEnumerator<IPin, IEnumPins>>();
        enumerator->_objects.emplace_back(_videoReceiverPin);
        *ppEnum = enumerator.detach();

        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE FindPin(LPCWSTR, IPin**) override { return E_NOTIMPL; }

    HRESULT STDMETHODCALLTYPE JoinFilterGraph(IFilterGraph* pGraph, LPCWSTR) override
    {
        _graph = pGraph;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE QueryFilterInfo(FILTER_INFO* pInfo) override
    {
        std::copy(std::begin(NAME), std::end(NAME), pInfo->achName);
        if (_graph)
        {
            pInfo->pGraph = _graph;
            _graph->AddRef();
        }
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE QueryVendorInfo(LPWSTR* pVendorInfo) override
    {
        auto info = static_cast<LPWSTR>(CoTaskMemAlloc(sizeof(VENDOR)));
        std::copy(std::begin(VENDOR), std::end(VENDOR), info);
        *pVendorInfo = info;
        return S_OK;
    }

    virtual ~VideoCaptureReceiverFilter() = default;

    constexpr static inline wchar_t NAME[] = L"PowerToysVCMCaptureFilter";
    constexpr static inline wchar_t VENDOR[] = L"Microsoft Corporation";
};

struct VideoCaptureReceiverPin : winrt::implements<VideoCaptureReceiverPin, IPin, IMemInputPin>
{
    VideoCaptureReceiverFilter* _owningFilter = nullptr;
    unique_media_type_ptr _expectedMediaType;
    wil::com_ptr_nothrow<IPin> _captureInputPin;
    unique_media_type_ptr _inputCaptureMediaType;
    std::atomic_bool _flushing = false;
    VideoCaptureDevice::callback_t _frameCallback;

    wil::com_ptr_nothrow<IMemAllocator> _allocator;

    VideoCaptureReceiverPin(unique_media_type_ptr mediaType, VideoCaptureReceiverFilter* filter) :
        _expectedMediaType{ std::move(mediaType) }, _owningFilter{ filter }
    {
    }

    HRESULT STDMETHODCALLTYPE Connect(IPin*, const AM_MEDIA_TYPE* pmt) override
    {
        if (_owningFilter->_state == State_Running)
        {
            return VFW_E_NOT_STOPPED;
        }

        if (_captureInputPin)
        {
            return VFW_E_ALREADY_CONNECTED;
        }

        if (!pmt || pmt->majortype == GUID_NULL)
        {
            return S_OK;
        }

        if (pmt->majortype != _expectedMediaType->majortype || pmt->subtype != _expectedMediaType->subtype)
        {
            return S_FALSE;
        }

        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE ReceiveConnection(IPin* pConnector, const AM_MEDIA_TYPE* pmt) override
    {
        if (!pConnector || !pmt)
        {
            return E_POINTER;
        }

        if (_captureInputPin)
        {
            return VFW_E_ALREADY_CONNECTED;
        }

        if (_owningFilter->_state != State_Stopped)
        {
            return VFW_E_NOT_STOPPED;
        }

        if (QueryAccept(pmt) != S_OK)
        {
            return VFW_E_TYPE_NOT_ACCEPTED;
        }

        _captureInputPin = pConnector;
        _inputCaptureMediaType = CopyMediaType(pmt);

        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE Disconnect() override
    {
        _allocator.reset();
        _captureInputPin.reset();
        _inputCaptureMediaType.reset();
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE ConnectedTo(IPin** pPin) override
    {
        if (!_captureInputPin)
        {
            return VFW_E_NOT_CONNECTED;
        }

        return _captureInputPin.try_copy_to(pPin) ? S_OK : E_FAIL;
    }

    HRESULT STDMETHODCALLTYPE ConnectionMediaType(AM_MEDIA_TYPE* pmt) override
    {
        if (!pmt)
        {
            return E_POINTER;
        }

        if (!_inputCaptureMediaType)
        {
            return VFW_E_NOT_CONNECTED;
        }

        *pmt = *CopyMediaType(_inputCaptureMediaType.get()).release();
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE QueryPinInfo(PIN_INFO* pInfo) override
    {
        if (!pInfo)
        {
            return E_POINTER;
        }

        pInfo->pFilter = _owningFilter;
        if (_owningFilter)
        {
            _owningFilter->AddRef();
        }

        pInfo->dir = PINDIR_INPUT;
        std::copy(std::begin(NAME), std::end(NAME), pInfo->achName);
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE QueryDirection(PIN_DIRECTION* pPinDir) override
    {
        if (!pPinDir)
        {
            return E_POINTER;
        }

        *pPinDir = PINDIR_INPUT;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE QueryId(LPWSTR* lpId) override
    {
        if (!lpId)
        {
            return E_POINTER;
        }

        *lpId = static_cast<LPWSTR>(CoTaskMemAlloc(sizeof(NAME)));

        std::copy(std::begin(NAME), std::end(NAME), *lpId);
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE QueryAccept(const AM_MEDIA_TYPE* pmt) override
    {
        if (!pmt)
        {
            return E_POINTER;
        }

        if (pmt->majortype != _expectedMediaType->majortype || pmt->subtype != _expectedMediaType->subtype)
        {
            return S_FALSE;
        }

        if (_captureInputPin)
        {
// disable warning 26492 - Don't use const_cast to cast away const
// reset needs 'pmt' to be non-const, we can't easily change the query accept prototype
// because of the inheritance.
#pragma warning(suppress : 26492)
            _inputCaptureMediaType.reset(const_cast<AM_MEDIA_TYPE*>(pmt));
        }

        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE EnumMediaTypes(IEnumMediaTypes** ppEnum) override
    {
        if (!ppEnum)
        {
            return E_POINTER;
        }

        auto enumerator = winrt::make_self<MediaTypeEnumerator>();
        enumerator->_objects.emplace_back(CopyMediaType(_expectedMediaType.get()));
        *ppEnum = enumerator.detach();

        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE QueryInternalConnections(IPin**, ULONG*) override { return E_NOTIMPL; }

    HRESULT STDMETHODCALLTYPE EndOfStream() override { return S_OK; }

    HRESULT STDMETHODCALLTYPE BeginFlush() override
    {
        _flushing = true;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE EndFlush() override
    {
        _flushing = false;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE NewSegment(REFERENCE_TIME, REFERENCE_TIME, double) override { return S_OK; }

    HRESULT STDMETHODCALLTYPE GetAllocator(IMemAllocator** allocator) override
    {
        VERBOSE_LOG;
        if (!_allocator)
        {
            return VFW_E_NO_ALLOCATOR;
        }

        return _allocator.try_copy_to(allocator) ? S_OK : E_FAIL;
    }

    HRESULT STDMETHODCALLTYPE NotifyAllocator(IMemAllocator* allocator, BOOL readOnly) override
    {
        VERBOSE_LOG;
        LOG(readOnly ? "Allocator READONLY: true" : "Allocator READONLY: false");
        _allocator = allocator;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE GetAllocatorRequirements(ALLOCATOR_PROPERTIES*) override { return E_NOTIMPL; }

    HRESULT STDMETHODCALLTYPE Receive(IMediaSample* pSample) override
    {
        if (_flushing)
        {
            return S_FALSE;
        }

        if (!pSample)
        {
            return E_POINTER;
        }

        if (pSample && _frameCallback)
        {
            _frameCallback(pSample);
        }

        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE ReceiveMultiple(IMediaSample** pSamples, long nSamples, long* nSamplesProcessed) override
    {
        if (!pSamples && nSamples)
        {
            return E_POINTER;
        }

        if (_flushing)
        {
            return S_FALSE;
        }

        for (long i = 0; i < nSamples; i++)
        {
            Receive(pSamples[i]);
        }

        *nSamplesProcessed = nSamples;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE ReceiveCanBlock() override { return S_FALSE; }

    virtual ~VideoCaptureReceiverPin() = default;

    constexpr static inline wchar_t NAME[] = L"PowerToysVCMCapturePin";
};

constexpr long MINIMAL_FPS_ALLOWED = 29;

const char* GetMediaSubTypeString(const GUID& guid)
{
    if (guid == MEDIASUBTYPE_RGB24)
    {
        return "MEDIASUBTYPE_RGB24";
    }

    if (guid == MEDIASUBTYPE_YUY2)
    {
        return "MEDIASUBTYPE_YUY2";
    }

    if (guid == MEDIASUBTYPE_MJPG)
    {
        return "MEDIASUBTYPE_MJPG";
    }

    if (guid == MEDIASUBTYPE_NV12)
    {
        return "MEDIASUBTYPE_NV12";
    }

    return "MEDIASUBTYPE_UNKNOWN";
}

std::optional<VideoStreamFormat> SelectBestMediaType(wil::com_ptr_nothrow<IPin>& pin)
{
    VERBOSE_LOG;
    wil::com_ptr_nothrow<IEnumMediaTypes> mediaTypeEnum;
    if (pin->EnumMediaTypes(&mediaTypeEnum); !mediaTypeEnum)
    {
        return std::nullopt;
    }

    ULONG _ = 0;
    VideoStreamFormat bestFormat;
    unique_media_type_ptr mt;
    while (mediaTypeEnum->Next(1, wil::out_param(mt), &_) == S_OK)
    {
        if (mt->majortype != MEDIATYPE_Video)
        {
            continue;
        }

        auto format = reinterpret_cast<VIDEOINFOHEADER*>(mt->pbFormat);
        if (!format || !format->AvgTimePerFrame)
        {
            LOG("VideoInfoHeader not found");
            continue;
        }

        const auto formatAvgFPS = 10000000LL / format->AvgTimePerFrame;
        if (format->AvgTimePerFrame > bestFormat.avgFrameTime || formatAvgFPS < MINIMAL_FPS_ALLOWED)
        {
            continue;
        }

        if (format->bmiHeader.biWidth < bestFormat.width || format->bmiHeader.biHeight < bestFormat.height)
        {
            continue;
        }

        if (mt->subtype != MEDIASUBTYPE_YUY2 && mt->subtype != MEDIASUBTYPE_MJPG && mt->subtype != MEDIASUBTYPE_RGB24)
        {
            OLECHAR* guidString;
            StringFromCLSID(mt->subtype, &guidString);
            LOG("Skipping mediatype due to unsupported subtype: ");
            LOG(guidString);
            ::CoTaskMemFree(guidString);
            continue;
        }

        bestFormat.avgFrameTime = format->AvgTimePerFrame;
        bestFormat.width = format->bmiHeader.biWidth;
        bestFormat.height = format->bmiHeader.biHeight;
        bestFormat.mediaType = std::move(mt);
    }

    if (!bestFormat.mediaType)
    {
        LOG(L"Couldn't select a suitable media format");
        return std::nullopt;
    }

    char selectedFormat[512]{};
    sprintf_s(selectedFormat, "Selected media format: %s %ldx%ld %lld fps", GetMediaSubTypeString(bestFormat.mediaType->subtype), bestFormat.width, bestFormat.height, 10000000LL / bestFormat.avgFrameTime);
    LOG(selectedFormat);

    return std::move(bestFormat);
}

std::vector<VideoCaptureDeviceInfo> VideoCaptureDevice::ListAll()
{
    std::vector<VideoCaptureDeviceInfo> devices;
    auto enumeratorFactory = wil::CoCreateInstanceNoThrow<ICreateDevEnum>(CLSID_SystemDeviceEnum);
    if (!enumeratorFactory)
    {
        LOG("Couldn't create devenum factory");
        return devices;
    }

    wil::com_ptr_nothrow<IEnumMoniker> enumMoniker;
    enumeratorFactory->CreateClassEnumerator(CLSID_VideoInputDeviceCategory, &enumMoniker, CDEF_DEVMON_PNP_DEVICE);
    if (!enumMoniker)
    {
        LOG("Couldn't create class enumerator");
        return devices;
    }

    ULONG _ = 0;
    wil::com_ptr_nothrow<IMoniker> moniker;
    while (enumMoniker->Next(1, &moniker, &_) == S_OK)
    {
        LOG("Inspecting moniker");
        VideoCaptureDeviceInfo deviceInfo;

        wil::com_ptr_nothrow<IPropertyBag> propertyData;
        moniker->BindToStorage(nullptr, nullptr, IID_IPropertyBag, reinterpret_cast<void**>(&propertyData));
        if (!propertyData)
        {
            LOG("BindToStorage failed");
            continue;
        }

        wil::unique_variant propVal;
        propVal.vt = VT_BSTR;

        if (FAILED(propertyData->Read(L"FriendlyName", &propVal, nullptr)))
        {
            LOG("Couldn't obtain FriendlyName property");
            continue;
        }

        deviceInfo.friendlyName = { propVal.bstrVal, SysStringLen(propVal.bstrVal) };
        LOG(deviceInfo.friendlyName);

        propVal.reset();
        propVal.vt = VT_BSTR;

        if (FAILED(propertyData->Read(L"DevicePath", &propVal, nullptr)))
        {
            LOG("Couldn't obtain DevicePath property");
            continue;
        }
        deviceInfo.devicePath = { propVal.bstrVal, SysStringLen(propVal.bstrVal) };

        wil::com_ptr_nothrow<IBaseFilter> filter;
        moniker->BindToObject(nullptr, nullptr, IID_IBaseFilter, reinterpret_cast<void**>(&filter));
        if (!filter)
        {
            LOG("Couldn't BindToObject");
            continue;
        }

        wil::com_ptr_nothrow<IEnumPins> pinsEnum;
        if (FAILED(filter->EnumPins(&pinsEnum)))
        {
            LOG("BindToObject EnumPins");
            continue;
        }

        wil::com_ptr_nothrow<IPin> pin;
        while (pinsEnum->Next(1, &pin, &_) == S_OK)
        {
            LOG("Inspecting pin");
            // Skip pins which do not belong to capture category
            GUID category{};
            DWORD __;
            if (auto props = pin.try_copy<IKsPropertySet>();
                !props ||
                FAILED(props->Get(AMPROPSETID_Pin, AMPROPERTY_PIN_CATEGORY, nullptr, 0, &category, sizeof(GUID), &__)) ||
                category != PIN_CATEGORY_CAPTURE)
            {
                continue;
            }

            // Skip non-output pins
            if (PIN_DIRECTION direction = {}; FAILED(pin->QueryDirection(&direction)) || direction != PINDIR_OUTPUT)
            {
                continue;
            }

            LOG("Found a pin of suitable category and direction, selecting format");
            auto bestFormat = SelectBestMediaType(pin);
            if (!bestFormat)
            {
                continue;
            }

            deviceInfo.captureOutputPin = std::move(pin);
            deviceInfo.bestFormat = std::move(bestFormat.value());
            deviceInfo.captureOutputFilter = std::move(filter);
            devices.emplace_back(std::move(deviceInfo));
        }
    }

    return devices;
}

std::optional<VideoCaptureDevice> VideoCaptureDevice::Create(VideoCaptureDeviceInfo&& vdi, callback_t callback)
{
    VERBOSE_LOG;
    VideoCaptureDevice result;

    result._graph = wil::CoCreateInstanceNoThrow<IGraphBuilder>(CLSID_FilterGraph);
    result._builder = wil::CoCreateInstanceNoThrow<ICaptureGraphBuilder2>(CLSID_CaptureGraphBuilder2);
    if (!result._graph || !result._builder)
    {
        return std::nullopt;
    }

    if (FAILED(result._builder->SetFiltergraph(result._graph.get())))
    {
        return std::nullopt;
    }

    result._control = result._graph.try_query<IMediaControl>();
    if (!result._control)
    {
        return std::nullopt;
    }

    auto pinConfig = vdi.captureOutputPin.try_query<IAMStreamConfig>();
    if (!pinConfig)
    {
        return std::nullopt;
    }

    if (FAILED(pinConfig->SetFormat(vdi.bestFormat.mediaType.get())))
    {
        return std::nullopt;
    }

    auto captureInputFilter = winrt::make_self<VideoCaptureReceiverFilter>();
    auto receiverPin = winrt::make_self<VideoCaptureReceiverPin>(std::move(vdi.bestFormat.mediaType), captureInputFilter.get());
    receiverPin->_frameCallback = std::move(callback);
    captureInputFilter->_videoReceiverPin.attach(receiverPin.get());
    auto detachReceiverPin = wil::scope_exit([&receiverPin]() { receiverPin.detach(); });

    if (FAILED(result._graph->AddFilter(captureInputFilter.get(), nullptr)))
    {
        return std::nullopt;
    }

    if (FAILED(result._graph->AddFilter(vdi.captureOutputFilter.get(), nullptr)))
    {
        return std::nullopt;
    }

    if (FAILED(result._graph->ConnectDirect(vdi.captureOutputPin.get(), captureInputFilter->_videoReceiverPin.get(), nullptr)))
    {
        return std::nullopt;
    }

    result._allocator = receiverPin->_allocator;
    return std::make_optional(std::move(result));
}

bool VideoCaptureDevice::StartCapture()
{
    VERBOSE_LOG;
    return SUCCEEDED(_control->Run());
}

bool VideoCaptureDevice::StopCapture()
{
    VERBOSE_LOG;
    return SUCCEEDED(_control->Stop());
}

VideoCaptureDevice::~VideoCaptureDevice()
{
    StopCapture();
}
