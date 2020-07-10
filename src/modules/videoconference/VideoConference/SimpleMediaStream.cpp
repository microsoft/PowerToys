#include "stdafx.h"

#include "ImageLoader.h"

#include <vector>
#include <algorithm>

HRESULT CopyAttribute(IMFAttributes* pSrc, IMFAttributes* pDest, const GUID& key);

template<class T>
void SafeRelease(T** ppT)
{
    if (*ppT)
    {
        (*ppT)->Release();
        *ppT = nullptr;
    }
}

void DeviceList::Clear()
{
    for (UINT32 i = 0; i < m_numberDevices; i++)
    {
        CoTaskMemFree(m_deviceFriendlyNames[i]);
        SafeRelease(&m_ppDevices[i]);
    }
    CoTaskMemFree(m_ppDevices);
    m_ppDevices = nullptr;
    delete[] m_deviceFriendlyNames;
    m_deviceFriendlyNames = nullptr;
    m_numberDevices = 0;
}

HRESULT DeviceList::EnumerateDevices()
{
    HRESULT hr = S_OK;
    IMFAttributes* pAttributes = nullptr;

    Clear();

    // Initialize an attribute store. We will use this to
    // specify the enumeration parameters.

    hr = MFCreateAttributes(&pAttributes, 1);

    // Ask for source type = video capture devices
    if (SUCCEEDED(hr))
    {
        hr = pAttributes->SetGUID(
            MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE,
            MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID);
    }

    // Enumerate devices.
    if (SUCCEEDED(hr))
    {
        hr = MFEnumDeviceSources(pAttributes, &m_ppDevices, &m_numberDevices);
    }

    m_deviceFriendlyNames = new (std::nothrow) wchar_t*[m_numberDevices];
    for (UINT32 i = 0; i < m_numberDevices; i++)
    {
        UINT32 nameLength = 0;
        m_ppDevices[i]->GetAllocatedString(MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME, &m_deviceFriendlyNames[i], &nameLength);
    }

    SafeRelease(&pAttributes);

    return hr;
}

HRESULT DeviceList::GetDevice(UINT32 index, IMFActivate** ppActivate)
{
    if (index >= Count())
    {
        return E_INVALIDARG;
    }

    *ppActivate = m_ppDevices[index];
    (*ppActivate)->AddRef();

    return S_OK;
}

std::wstring_view DeviceList::GetDeviceName(UINT32 index)
{
    if (index >= Count())
    {
        return {};
    }

    return m_deviceFriendlyNames[index];
}

//-------------------------------------------------------------------
// CopyAttribute
//
// Copy an attribute value from one attribute store to another.
//-------------------------------------------------------------------

HRESULT CopyAttribute(IMFAttributes* pSrc, IMFAttributes* pDest, const GUID& key)
{
    PROPVARIANT var;
    PropVariantInit(&var);

    HRESULT hr = S_OK;

    hr = pSrc->GetItem(key, &var);
    if (SUCCEEDED(hr))
    {
        hr = pDest->SetItem(key, var);
    }

    PropVariantClear(&var);
    return hr;
}

HRESULT ConfigureSourceReader(IMFSourceReader* pReader)
{
    // The list of acceptable types.
    GUID subtypes[] = {
        MFVideoFormat_NV12, MFVideoFormat_YUY2, MFVideoFormat_UYVY, MFVideoFormat_RGB32, MFVideoFormat_RGB24, MFVideoFormat_IYUV
    };

    HRESULT hr = S_OK;
    BOOL bUseNativeType = FALSE;

    GUID subtype = { 0 };

    IMFMediaType* pType = nullptr;

    // If the source's native format matches any of the formats in
    // the list, prefer the native format.

    // Note: The camera might support multiple output formats,
    // including a range of frame dimensions. The application could
    // provide a list to the user and have the user select the
    // camera's output format. That is outside the scope of this
    // sample, however.

    hr = pReader->GetNativeMediaType(
        (DWORD)MF_SOURCE_READER_FIRST_VIDEO_STREAM,
        0, // Type index
        &pType);

    if (FAILED(hr))
    {
        goto done;
    }

    hr = pType->GetGUID(MF_MT_SUBTYPE, &subtype);

    if (FAILED(hr))
    {
        goto done;
    }

    for (UINT32 i = 0; i < ARRAYSIZE(subtypes); i++)
    {
        if (subtype == subtypes[i])
        {
            hr = pReader->SetCurrentMediaType(
                (DWORD)MF_SOURCE_READER_FIRST_VIDEO_STREAM,
                nullptr,
                pType);

            bUseNativeType = TRUE;
            break;
        }
    }

    if (!bUseNativeType)
    {
        // None of the native types worked. The camera might offer
        // output a compressed type such as MJPEG or DV.

        // Try adding a decoder.

        for (UINT32 i = 0; i < ARRAYSIZE(subtypes); i++)
        {
            hr = pType->SetGUID(MF_MT_SUBTYPE, subtypes[i]);

            if (FAILED(hr))
            {
                goto done;
            }

            hr = pReader->SetCurrentMediaType(
                (DWORD)MF_SOURCE_READER_FIRST_VIDEO_STREAM,
                nullptr,
                pType);

            if (SUCCEEDED(hr))
            {
                break;
            }
        }
    }

done:
    SafeRelease(&pType);
    return hr;
}

ComPtr<IMFMediaType> SelectBestMediaType(IMFSourceReader* reader)
{
    std::vector<ComPtr<IMFMediaType>> supported_mtypes;

    auto type_framerate = [](IMFMediaType* type) {
        UINT32 framerateNum = 0, framerateDenum = 1;
        MFGetAttributeRatio(type, MF_MT_FRAME_RATE, &framerateNum, &framerateDenum);
        const float framerate = static_cast<float>(framerateNum) / framerateDenum;
        return framerate;
    };

    UINT64 max_resolution = 0;
    for (DWORD tyIdx = 0;; ++tyIdx)
    {
        IMFMediaType* next_type = nullptr;
        HRESULT hr = reader->GetNativeMediaType((DWORD)MF_SOURCE_READER_FIRST_VIDEO_STREAM, tyIdx, &next_type);
        if (!next_type)
        {
            break;
        }
#if 0 // Debugging code to test yuy2 conversion
    GUID subtype{};
    next_type->GetGUID(MF_MT_SUBTYPE, &subtype);
    if(memcmp(&subtype, &MFVideoFormat_YUY2, sizeof GUID))
      continue;
#endif
        constexpr float minimal_acceptable_framerate = 15.f;
        // Skip low frame types
        if (type_framerate(next_type) < minimal_acceptable_framerate)
        {
            continue;
        }

        UINT32 w = 0, h = 0;
        MFGetAttributeSize(next_type, MF_MT_FRAME_SIZE, &w, &h);
        const UINT64 cur_resolution_mult = static_cast<UINT64>(w) * h;
        if (cur_resolution_mult >= max_resolution)
        {
            supported_mtypes.emplace_back(next_type);
            max_resolution = cur_resolution_mult;
        }

        if (hr == MF_E_NO_MORE_TYPES || FAILED(hr))
        {
            break;
        }
    }

    // Remove all types with non-optimal resolution
    supported_mtypes.erase(std::remove_if(begin(supported_mtypes), end(supported_mtypes), [max_resolution](ComPtr<IMFMediaType>& ptr) {
                               UINT32 w = 0, h = 0;
                               MFGetAttributeSize(ptr.Get(), MF_MT_FRAME_SIZE, &w, &h);
                               const UINT64 cur_resolution_mult = static_cast<UINT64>(w) * h;
                               return cur_resolution_mult != max_resolution;
                           }),
                           end(supported_mtypes));

    // Desc-sort by frame_rate
    std::sort(begin(supported_mtypes), end(supported_mtypes), [type_framerate](ComPtr<IMFMediaType>& lhs, ComPtr<IMFMediaType>& rhs) {
        return type_framerate(lhs.Get()) > type_framerate(rhs.Get());
    });

    return std::move(supported_mtypes[0]);
}

HRESULT
SimpleMediaStream::RuntimeClassInitialize(
    _In_ SimpleMediaSource* pSource)
{
    HRESULT hr = S_OK;
    ComPtr<IMFMediaTypeHandler> spTypeHandler;
    ComPtr<IMFAttributes> attrs;

    if (nullptr == pSource)
    {
        return E_INVALIDARG;
    }
    RETURN_IF_FAILED(pSource->QueryInterface(IID_PPV_ARGS(&_parent)));

    _devices.Clear();
    _devices.EnumerateDevices();

    // TODO: choose the device from the settings
    _devices.GetDevice((UINT32)0, &_activate);

    IMFMediaSource* realSource = nullptr;

    hr = _activate->ActivateObject(
        __uuidof(IMFMediaSource),
        (void**)&realSource);

    if (SUCCEEDED(hr))
    {
        IMFAttributes* pAttributes = nullptr;

        hr = MFCreateAttributes(&pAttributes, 2);

        if (SUCCEEDED(hr))
        {
            hr = MFCreateSourceReaderFromMediaSource(
                realSource,
                pAttributes,
                &m_pReader);
            _spMediaType = SelectBestMediaType(m_pReader);
            RETURN_IF_FAILED(MFCreateAttributes(&_spAttributes, 10));
            RETURN_IF_FAILED(this->_SetStreamAttributes(_spAttributes.Get()));
            RETURN_IF_FAILED(MFCreateEventQueue(&_spEventQueue));

            // Initialize stream descriptors
            RETURN_IF_FAILED(MFCreateStreamDescriptor(0, 1, _spMediaType.GetAddressOf(), &_spStreamDesc));

            RETURN_IF_FAILED(_spStreamDesc->GetMediaTypeHandler(&spTypeHandler));
            RETURN_IF_FAILED(spTypeHandler->SetCurrentMediaType(_spMediaType.Get()));
            RETURN_IF_FAILED(this->_SetStreamDescriptorAttributes(_spStreamDesc.Get()));
            m_pReader->SetCurrentMediaType((DWORD)MF_SOURCE_READER_FIRST_VIDEO_STREAM, nullptr, _spMediaType.Get());
        }

        SafeRelease(&pAttributes);
        SafeRelease(&realSource);
    }

    return hr;
}

// IMFMediaEventGenerator
IFACEMETHODIMP
SimpleMediaStream::BeginGetEvent(
    _In_ IMFAsyncCallback* pCallback,
    _In_ IUnknown* punkState)
{
    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();

    RETURN_IF_FAILED(_CheckShutdownRequiresLock());
    RETURN_IF_FAILED(_spEventQueue->BeginGetEvent(pCallback, punkState));

    return hr;
}

IFACEMETHODIMP
SimpleMediaStream::EndGetEvent(
    _In_ IMFAsyncResult* pResult,
    _COM_Outptr_ IMFMediaEvent** ppEvent)
{
    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();

    RETURN_IF_FAILED(_CheckShutdownRequiresLock());
    RETURN_IF_FAILED(_spEventQueue->EndGetEvent(pResult, ppEvent));

    return hr;
}

IFACEMETHODIMP
SimpleMediaStream::GetEvent(
    DWORD dwFlags,
    _COM_Outptr_ IMFMediaEvent** ppEvent)
{
    // NOTE:
    // GetEvent can block indefinitely, so we don't hold the lock.
    // This requires some juggling with the event queue pointer.

    HRESULT hr = S_OK;

    ComPtr<IMFMediaEventQueue> spQueue;

    {
        auto lock = _critSec.Lock();

        RETURN_IF_FAILED(_CheckShutdownRequiresLock());
        spQueue = _spEventQueue;
    }

    // Now get the event.
    RETURN_IF_FAILED(_spEventQueue->GetEvent(dwFlags, ppEvent));

    return hr;
}

IFACEMETHODIMP
SimpleMediaStream::QueueEvent(
    MediaEventType eventType,
    REFGUID guidExtendedType,
    HRESULT hrStatus,
    _In_opt_ PROPVARIANT const* pvValue)
{
    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();

    RETURN_IF_FAILED(_CheckShutdownRequiresLock());
    RETURN_IF_FAILED(_spEventQueue->QueueEventParamVar(eventType, guidExtendedType, hrStatus, pvValue));

    return hr;
}

// IMFMediaStream
IFACEMETHODIMP
SimpleMediaStream::GetMediaSource(
    _COM_Outptr_ IMFMediaSource** ppMediaSource)
{
    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();

    if (ppMediaSource == nullptr)
    {
        return E_POINTER;
    }
    *ppMediaSource = nullptr;

    RETURN_IF_FAILED(_CheckShutdownRequiresLock());

    *ppMediaSource = _parent.Get();
    (*ppMediaSource)->AddRef();

    return hr;
}

IFACEMETHODIMP
SimpleMediaStream::GetStreamDescriptor(
    _COM_Outptr_ IMFStreamDescriptor** ppStreamDescriptor)
{
    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();

    if (ppStreamDescriptor == nullptr)
    {
        return E_POINTER;
    }
    *ppStreamDescriptor = nullptr;

    RETURN_IF_FAILED(_CheckShutdownRequiresLock());

    if (_spStreamDesc != nullptr)
    {
        *ppStreamDescriptor = _spStreamDesc.Get();
        (*ppStreamDescriptor)->AddRef();
    }
    else
    {
        return E_UNEXPECTED;
    }

    return hr;
}

IFACEMETHODIMP
SimpleMediaStream::RequestSample(
    _In_ IUnknown* pToken)
{
    auto lock = _critSec.Lock();
    HRESULT hr{};
    RETURN_IF_FAILED(_CheckShutdownRequiresLock());

    const auto nDevices = _devices.Count();

    // Request the first video frame.

    ComPtr<IMFSample> sample;
    DWORD streamFlags = 0;

    hr = m_pReader->ReadSample(
        (DWORD)MF_SOURCE_READER_FIRST_VIDEO_STREAM,
        0,
        nullptr,
        &streamFlags,
        nullptr,
        &sample);
    if (FAILED(hr))
    {
        return hr;
    }

    const wchar_t shmemEndpoint[] = L"Global\\PowerToysWebcamMuteSwitch";

    const auto noiseToggle = [=]() -> const uint8_t* {
        SECURITY_DESCRIPTOR sd;

        InitializeSecurityDescriptor(&sd, SECURITY_DESCRIPTOR_REVISION);

        SetSecurityDescriptorDacl(&sd, true, nullptr, false);

        SECURITY_ATTRIBUTES sa;
        sa.nLength = sizeof(sa);
        sa.lpSecurityDescriptor = &sd;
        sa.bInheritHandle = false;

        auto hMapFile{ CreateFileMappingW(
            INVALID_HANDLE_VALUE, // use paging file
            &sa, // default security
            PAGE_READWRITE, // read/write access
            0, // maximum object size (high-order DWORD)
            1, // maximum object size (low-order DWORD)
            shmemEndpoint) }; // name of mapping object
        if (!hMapFile)
        {
            return nullptr;
        }
        auto shmem = (const uint8_t*)MapViewOfFile(hMapFile, // handle to map object
                                                   FILE_MAP_READ, // read/write permission
                                                   0,
                                                   0,
                                                   1);
        return shmem;
    }();

    const bool disableWebcam = noiseToggle && *noiseToggle;

    // TODO: get the image from the settings
    static auto imageSample = LoadImageAsSample(LR"(P:\wecam_test_1920.jpg)", _spMediaType.Get());

    IMFSample* outputSample = disableWebcam ? imageSample.Get() : sample.Get();
    const bool noSampleAvailable = !outputSample;

    if (noSampleAvailable)
    {
        // Create an empty sample
        RETURN_IF_FAILED(MFCreateSample(&outputSample));
    }
    RETURN_IF_FAILED(outputSample->SetSampleTime(MFGetSystemTime()));
    RETURN_IF_FAILED(outputSample->SetSampleDuration(333333));
    if (pToken != nullptr)
    {
        RETURN_IF_FAILED(outputSample->SetUnknown(MFSampleExtension_Token, pToken));
    }

    if (noSampleAvailable)
    {
        RETURN_IF_FAILED(_spEventQueue->QueueEventParamUnk(MEStreamTick,
                                                           GUID_NULL,
                                                           S_OK,
                                                           nullptr));
    }

    RETURN_IF_FAILED(_spEventQueue->QueueEventParamUnk(MEMediaSample,
                                                       GUID_NULL,
                                                       S_OK,
                                                       outputSample));

    return S_OK;
}

//////////////////////////////////////////////////////////////////////////////////////////
// IMFMediaStream2
IFACEMETHODIMP
SimpleMediaStream::SetStreamState(
    MF_STREAM_STATE state)
{
    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();
    bool runningState = false;

    RETURN_IF_FAILED(_CheckShutdownRequiresLock());

    switch (state)
    {
    case MF_STREAM_STATE_PAUSED:
        goto done; // because not supported
    case MF_STREAM_STATE_RUNNING:
        runningState = true;
        break;
    case MF_STREAM_STATE_STOPPED:
        runningState = false;
        break;
    default:
        hr = MF_E_INVALID_STATE_TRANSITION;
        break;
    }

    _isSelected = runningState;

done:
    return hr;
}

IFACEMETHODIMP
SimpleMediaStream::GetStreamState(
    _Out_ MF_STREAM_STATE* pState)
{
    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();
    BOOLEAN pauseState = false;

    RETURN_IF_FAILED(_CheckShutdownRequiresLock());

    if (SUCCEEDED(hr))
    {
        *pState = (_isSelected ? MF_STREAM_STATE_RUNNING : MF_STREAM_STATE_STOPPED);
    }

    return hr;
}

HRESULT
SimpleMediaStream::Shutdown()
{
    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();

    _isShutdown = true;
    _parent.Reset();

    if (_spEventQueue != nullptr)
    {
        hr = _spEventQueue->Shutdown();
        _spEventQueue.Reset();
    }

    _spAttributes.Reset();
    _spMediaType.Reset();
    _spStreamDesc.Reset();

    m_pReader->Release();

    _isSelected = false;

    return hr;
}

HRESULT
SimpleMediaStream::_CheckShutdownRequiresLock()
{
    if (_isShutdown)
    {
        return MF_E_SHUTDOWN;
    }

    if (_spEventQueue == nullptr)
    {
        return E_UNEXPECTED;
    }
    return S_OK;
}

HRESULT
SimpleMediaStream::_SetStreamAttributes(
    _In_ IMFAttributes* pAttributeStore)
{
    HRESULT hr = S_OK;

    if (nullptr == pAttributeStore)
    {
        return E_INVALIDARG;
    }

    RETURN_IF_FAILED(pAttributeStore->SetGUID(MF_DEVICESTREAM_STREAM_CATEGORY, PINNAME_VIDEO_CAPTURE));
    RETURN_IF_FAILED(pAttributeStore->SetUINT32(MF_DEVICESTREAM_STREAM_ID, STREAMINDEX));
    RETURN_IF_FAILED(pAttributeStore->SetUINT32(MF_DEVICESTREAM_FRAMESERVER_SHARED, 1));
    RETURN_IF_FAILED(pAttributeStore->SetUINT32(MF_DEVICESTREAM_ATTRIBUTE_FRAMESOURCE_TYPES, _MFFrameSourceTypes::MFFrameSourceTypes_Color));

    return hr;
}

HRESULT
SimpleMediaStream::_SetStreamDescriptorAttributes(
    _In_ IMFAttributes* pAttributeStore)
{
    HRESULT hr = S_OK;

    if (nullptr == pAttributeStore)
    {
        return E_INVALIDARG;
    }

    RETURN_IF_FAILED(pAttributeStore->SetGUID(MF_DEVICESTREAM_STREAM_CATEGORY, PINNAME_VIDEO_CAPTURE));
    RETURN_IF_FAILED(pAttributeStore->SetUINT32(MF_DEVICESTREAM_STREAM_ID, STREAMINDEX));
    RETURN_IF_FAILED(pAttributeStore->SetUINT32(MF_DEVICESTREAM_FRAMESERVER_SHARED, 1));
    RETURN_IF_FAILED(pAttributeStore->SetUINT32(MF_DEVICESTREAM_ATTRIBUTE_FRAMESOURCE_TYPES, _MFFrameSourceTypes::MFFrameSourceTypes_Color));

    return hr;
}
