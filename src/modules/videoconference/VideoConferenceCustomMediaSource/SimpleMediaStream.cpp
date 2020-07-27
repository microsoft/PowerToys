#include "stdafx.h"

#include "ImageLoader.h"

#include <vector>
#include <algorithm>

#include "SimpleMediaSource.h"
#include "SimpleMediaStream.h"
#include <common\user.h>

#include "Logging.h"

HRESULT CopyAttribute(IMFAttributes* pSrc, IMFAttributes* pDest, const GUID& key);

const static std::wstring_view MODULE_NAME = L"Video Conference";
const static std::wstring_view VIRTUAL_CAMERA_NAME = L"PowerToys VideoConference";

void DeviceList::Clear()
{
    LogToFile(__FUNCTION__);

    for (UINT32 i = 0; i < m_numberDevices; i++)
    {
        CoTaskMemFree(m_deviceFriendlyNames[i]);
        if (m_ppDevices[i])
        {
            m_ppDevices[i]->Release();
        }
    }
    CoTaskMemFree(m_ppDevices);
    m_ppDevices = nullptr;
    if (m_deviceFriendlyNames)
    {
        delete[] m_deviceFriendlyNames;
    }

    m_deviceFriendlyNames = nullptr;
    m_numberDevices = 0;
}

HRESULT DeviceList::EnumerateDevices()
{
    LogToFile(__FUNCTION__);

    HRESULT hr = S_OK;
    ComPtr<IMFAttributes> pAttributes;
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
        hr = MFEnumDeviceSources(pAttributes.Get(), &m_ppDevices, &m_numberDevices);
    }

    if (FAILED(hr))
    {
        return hr;
    }

    m_deviceFriendlyNames = new (std::nothrow) wchar_t*[m_numberDevices];
    for (UINT32 i = 0; i < m_numberDevices; i++)
    {
        UINT32 nameLength = 0;
        m_ppDevices[i]->GetAllocatedString(MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME, &m_deviceFriendlyNames[i], &nameLength);
    }

    return hr;
}

HRESULT DeviceList::GetDevice(UINT32 index, IMFActivate** ppActivate)
{
    LogToFile(__FUNCTION__);

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
    LogToFile(__FUNCTION__);

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
    LogToFile(__FUNCTION__);

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

ComPtr<IMFMediaType> SelectBestMediaType(IMFSourceReader* reader)
{
    LogToFile(__FUNCTION__);

    std::vector<ComPtr<IMFMediaType>> supportedMTypes;

    auto typeFramerate = [](IMFMediaType* type) {
        UINT32 framerateNum = 0, framerateDenum = 1;
        MFGetAttributeRatio(type, MF_MT_FRAME_RATE, &framerateNum, &framerateDenum);
        const float framerate = static_cast<float>(framerateNum) / framerateDenum;
        return framerate;
    };

    UINT64 maxResolution = 0;
    for (DWORD tyIdx = 0;; ++tyIdx)
    {
        IMFMediaType* nextType = nullptr;
        HRESULT hr = reader->GetNativeMediaType((DWORD)MF_SOURCE_READER_FIRST_VIDEO_STREAM, tyIdx, &nextType);
        if (!nextType)
        {
            break;
        }

        GUID subtype{};
        nextType->GetGUID(MF_MT_SUBTYPE, &subtype);

        if (subtype == MFVideoFormat_YUY2)
            LogToFile("MFVideoFormat_YUY2");
        else if (subtype == MFVideoFormat_RGB32)
            LogToFile("MFVideoFormat_RGB32");
        else if (subtype == MFVideoFormat_RGB24)
            LogToFile("MFVideoFormat_RGB24");
        else if (subtype == MFVideoFormat_ARGB32)
            LogToFile("MFVideoFormat_ARGB32");
        else if (subtype == MFVideoFormat_RGB555)
            LogToFile("MFVideoFormat_RGB555");
        else if (subtype == MFVideoFormat_RGB565)
            LogToFile("MFVideoFormat_RGB565");
        else if (subtype == MFVideoFormat_RGB8)
            LogToFile("MFVideoFormat_RGB8");
        else if (subtype == MFVideoFormat_L8)
            LogToFile("MFVideoFormat_L8");
        else if (subtype == MFVideoFormat_L16)
            LogToFile("MFVideoFormat_L16");
        else if (subtype == MFVideoFormat_D16)
            LogToFile("MFVideoFormat_D16");
        else if (subtype == MFVideoFormat_AYUV)
            LogToFile("MFVideoFormat_AYUV");
        else if (subtype == MFVideoFormat_YUY2)
            LogToFile("MFVideoFormat_YUY2");
        else if (subtype == MFVideoFormat_YVYU)
            LogToFile("MFVideoFormat_YVYU");
        else if (subtype == MFVideoFormat_YVU9)
            LogToFile("MFVideoFormat_YVU9");
        else if (subtype == MFVideoFormat_UYVY)
            LogToFile("MFVideoFormat_UYVY");
        else if (subtype == MFVideoFormat_NV11)
            LogToFile("MFVideoFormat_NV11");
        else if (subtype == MFVideoFormat_NV12)
            LogToFile("MFVideoFormat_NV12");
        else if (subtype == MFVideoFormat_YV12)
            LogToFile("MFVideoFormat_YV12");
        else if (subtype == MFVideoFormat_I420)
            LogToFile("MFVideoFormat_I420");
        else if (subtype == MFVideoFormat_IYUV)
            LogToFile("MFVideoFormat_IYUV");
        else if (subtype == MFVideoFormat_Y210)
            LogToFile("MFVideoFormat_Y210");
        else if (subtype == MFVideoFormat_Y216)
            LogToFile("MFVideoFormat_Y216");
        else if (subtype == MFVideoFormat_Y410)
            LogToFile("MFVideoFormat_Y410");
        else if (subtype == MFVideoFormat_Y416)
            LogToFile("MFVideoFormat_Y416");
        else if (subtype == MFVideoFormat_Y41P)
            LogToFile("MFVideoFormat_Y41P");
        else if (subtype == MFVideoFormat_Y41T)
            LogToFile("MFVideoFormat_Y41T");
        else if (subtype == MFVideoFormat_Y42T)
            LogToFile("MFVideoFormat_Y42T");
        else if (subtype == MFVideoFormat_P210)
            LogToFile("MFVideoFormat_P210");
        else if (subtype == MFVideoFormat_P216)
            LogToFile("MFVideoFormat_P216");
        else if (subtype == MFVideoFormat_P010)
            LogToFile("MFVideoFormat_P010");
        else if (subtype == MFVideoFormat_P016)
            LogToFile("MFVideoFormat_P016");
        else if (subtype == MFVideoFormat_v210)
            LogToFile("MFVideoFormat_v210");
        else if (subtype == MFVideoFormat_v216)
            LogToFile("MFVideoFormat_v216");
        else if (subtype == MFVideoFormat_v410)
            LogToFile("MFVideoFormat_v410");
        else if (subtype == MFVideoFormat_MP43)
            LogToFile("MFVideoFormat_MP43");
        else if (subtype == MFVideoFormat_MP4S)
            LogToFile("MFVideoFormat_MP4S");
        else if (subtype == MFVideoFormat_M4S2)
            LogToFile("MFVideoFormat_M4S2");
        else if (subtype == MFVideoFormat_MP4V)
            LogToFile("MFVideoFormat_MP4V");
        else if (subtype == MFVideoFormat_WMV1)
            LogToFile("MFVideoFormat_WMV1");
        else if (subtype == MFVideoFormat_WMV2)
            LogToFile("MFVideoFormat_WMV2");
        else if (subtype == MFVideoFormat_WMV3)
            LogToFile("MFVideoFormat_WMV3");
        else if (subtype == MFVideoFormat_WVC1)
            LogToFile("MFVideoFormat_WVC1");
        else if (subtype == MFVideoFormat_MSS1)
            LogToFile("MFVideoFormat_MSS1");
        else if (subtype == MFVideoFormat_MSS2)
            LogToFile("MFVideoFormat_MSS2");
        else if (subtype == MFVideoFormat_MPG1)
            LogToFile("MFVideoFormat_MPG1");
        else if (subtype == MFVideoFormat_DVSL)
            LogToFile("MFVideoFormat_DVSL");
        else if (subtype == MFVideoFormat_DVSD)
            LogToFile("MFVideoFormat_DVSD");
        else if (subtype == MFVideoFormat_DVHD)
            LogToFile("MFVideoFormat_DVHD");
        else if (subtype == MFVideoFormat_DV25)
            LogToFile("MFVideoFormat_DV25");
        else if (subtype == MFVideoFormat_DV50)
            LogToFile("MFVideoFormat_DV50");
        else if (subtype == MFVideoFormat_DVH1)
            LogToFile("MFVideoFormat_DVH1");
        else if (subtype == MFVideoFormat_DVC)
            LogToFile("MFVideoFormat_DVC");
        else if (subtype == MFVideoFormat_H264)
            LogToFile("MFVideoFormat_H264");
        else if (subtype == MFVideoFormat_H265)
            LogToFile("MFVideoFormat_H265");
        else if (subtype == MFVideoFormat_MJPG)
            LogToFile("MFVideoFormat_MJPG");
        else if (subtype == MFVideoFormat_420O)
            LogToFile("MFVideoFormat_420O");
        else if (subtype == MFVideoFormat_HEVC)
            LogToFile("MFVideoFormat_HEVC");
        else if (subtype == MFVideoFormat_HEVC_ES)
            LogToFile("MFVideoFormat_HEVC_ES");
        else if (subtype == MFVideoFormat_VP80)
            LogToFile("MFVideoFormat_VP80");
        else if (subtype == MFVideoFormat_VP90)
            LogToFile("MFVideoFormat_VP90");
        else if (subtype == MFVideoFormat_ORAW)
            LogToFile("MFVideoFormat_ORAW");
        else
            LogToFile("Some ohter format");

        if (subtype != MFVideoFormat_RGB24)
            continue;

        constexpr float minimalAcceptableFramerate = 15.f;
        // Skip low frame types
        if (typeFramerate(nextType) < minimalAcceptableFramerate)
        {
            continue;
        }

        UINT32 w = 0, h = 0;
        MFGetAttributeSize(nextType, MF_MT_FRAME_SIZE, &w, &h);
        const UINT64 curResolutionMult = static_cast<UINT64>(w) * h;
        if (curResolutionMult >= maxResolution)
        {
            supportedMTypes.emplace_back(nextType);
            maxResolution = curResolutionMult;
        }

        if (hr == MF_E_NO_MORE_TYPES || FAILED(hr))
        {
            break;
        }
    }

    // Remove all types with non-optimal resolution
    supportedMTypes.erase(std::remove_if(begin(supportedMTypes), end(supportedMTypes), [maxResolution](ComPtr<IMFMediaType>& ptr) {
                              UINT32 w = 0, h = 0;
                              MFGetAttributeSize(ptr.Get(), MF_MT_FRAME_SIZE, &w, &h);
                              const UINT64 curResolutionMult = static_cast<UINT64>(w) * h;
                              return curResolutionMult != maxResolution;
                          }),
                          end(supportedMTypes));

    // Desc-sort by frame_rate
    std::sort(begin(supportedMTypes), end(supportedMTypes), [typeFramerate](ComPtr<IMFMediaType>& lhs, ComPtr<IMFMediaType>& rhs) {
        return typeFramerate(lhs.Get()) > typeFramerate(rhs.Get());
    });

    return std::move(supportedMTypes[0]);
}

HRESULT
SimpleMediaStream::RuntimeClassInitialize(
    _In_ SimpleMediaSource* pSource)
{
    LogToFile(__FUNCTION__);

    HRESULT hr = S_OK;

    if (nullptr == pSource)
    {
        return E_INVALIDARG;
    }
    RETURN_IF_FAILED_WITH_LOGGING(pSource->QueryInterface(IID_PPV_ARGS(&_parent)));

    SyncCurrentSettings();
    // We couldn't connect to the PT, so choose a default webcam
    if (!_settingsUpdateChannel)
    {
        UpdateSourceCamera(L"");
    }

    return S_OK;
}

// IMFMediaEventGenerator
IFACEMETHODIMP
SimpleMediaStream::BeginGetEvent(
    _In_ IMFAsyncCallback* pCallback,
    _In_ IUnknown* punkState)
{
    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();

    RETURN_IF_FAILED_WITH_LOGGING(_CheckShutdownRequiresLock());
    RETURN_IF_FAILED_WITH_LOGGING(_spEventQueue->BeginGetEvent(pCallback, punkState));

    return hr;
}

IFACEMETHODIMP
SimpleMediaStream::EndGetEvent(
    _In_ IMFAsyncResult* pResult,
    _COM_Outptr_ IMFMediaEvent** ppEvent)
{
    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();

    RETURN_IF_FAILED_WITH_LOGGING(_CheckShutdownRequiresLock());
    RETURN_IF_FAILED_WITH_LOGGING(_spEventQueue->EndGetEvent(pResult, ppEvent));

    return hr;
}

IFACEMETHODIMP
SimpleMediaStream::GetEvent(
    DWORD dwFlags,
    _COM_Outptr_ IMFMediaEvent** ppEvent)
{
    LogToFile(__FUNCTION__);

    // NOTE:
    // GetEvent can block indefinitely, so we don't hold the lock.
    // This requires some juggling with the event queue pointer.

    HRESULT hr = S_OK;

    ComPtr<IMFMediaEventQueue> spQueue;

    {
        auto lock = _critSec.Lock();

        RETURN_IF_FAILED_WITH_LOGGING(_CheckShutdownRequiresLock());
        spQueue = _spEventQueue;
    }

    // Now get the event.
    RETURN_IF_FAILED_WITH_LOGGING(_spEventQueue->GetEvent(dwFlags, ppEvent));

    return hr;
}

IFACEMETHODIMP
SimpleMediaStream::QueueEvent(
    MediaEventType eventType,
    REFGUID guidExtendedType,
    HRESULT hrStatus,
    _In_opt_ PROPVARIANT const* pvValue)
{
    LogToFile(__FUNCTION__);

    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();

    RETURN_IF_FAILED_WITH_LOGGING(_CheckShutdownRequiresLock());
    RETURN_IF_FAILED_WITH_LOGGING(_spEventQueue->QueueEventParamVar(eventType, guidExtendedType, hrStatus, pvValue));

    return hr;
}

// IMFMediaStream
IFACEMETHODIMP
SimpleMediaStream::GetMediaSource(
    _COM_Outptr_ IMFMediaSource** ppMediaSource)
{
    LogToFile(__FUNCTION__);

    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();

    if (ppMediaSource == nullptr)
    {
        return E_POINTER;
    }
    *ppMediaSource = nullptr;

    RETURN_IF_FAILED_WITH_LOGGING(_CheckShutdownRequiresLock());

    *ppMediaSource = _parent.Get();
    (*ppMediaSource)->AddRef();

    return hr;
}

IFACEMETHODIMP
SimpleMediaStream::GetStreamDescriptor(
    _COM_Outptr_ IMFStreamDescriptor** ppStreamDescriptor)
{
    LogToFile(__FUNCTION__);

    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();

    if (ppStreamDescriptor == nullptr)
    {
        return E_POINTER;
    }
    *ppStreamDescriptor = nullptr;

    RETURN_IF_FAILED_WITH_LOGGING(_CheckShutdownRequiresLock());

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
    LogToFile(__FUNCTION__);

    auto lock = _critSec.Lock();
    HRESULT hr{};
    RETURN_IF_FAILED_WITH_LOGGING(_CheckShutdownRequiresLock());

    const bool disableWebcam = SyncCurrentSettings();

    // Request the first video frame.

    ComPtr<IMFSample> sample;
    DWORD streamFlags = 0;

    RETURN_IF_FAILED_WITH_LOGGING(_sourceCamera->ReadSample(
        (DWORD)MF_SOURCE_READER_FIRST_VIDEO_STREAM,
        0,
        nullptr,
        &streamFlags,
        nullptr,
        &sample));

    IMFSample* outputSample = disableWebcam ? _overlayImage.Get() : sample.Get();
    const bool noSampleAvailable = !outputSample;

    if (noSampleAvailable)
    {
        // Create an empty sample
        RETURN_IF_FAILED_WITH_LOGGING(MFCreateSample(&outputSample));
    }
    RETURN_IF_FAILED_WITH_LOGGING(outputSample->SetSampleTime(MFGetSystemTime()));
    RETURN_IF_FAILED_WITH_LOGGING(outputSample->SetSampleDuration(333333));
    if (pToken != nullptr)
    {
        RETURN_IF_FAILED_WITH_LOGGING(outputSample->SetUnknown(MFSampleExtension_Token, pToken));
    }

    if (noSampleAvailable)
    {
        RETURN_IF_FAILED_WITH_LOGGING(_spEventQueue->QueueEventParamUnk(MEStreamTick,
                                                                        GUID_NULL,
                                                                        S_OK,
                                                                        nullptr));
    }

    RETURN_IF_FAILED_WITH_LOGGING(_spEventQueue->QueueEventParamUnk(MEMediaSample,
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
    LogToFile(__FUNCTION__);

    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();
    bool runningState = false;

    RETURN_IF_FAILED(_CheckShutdownRequiresLock());

    switch (state)
    {
    case MF_STREAM_STATE_PAUSED:
        LogToFile("SetStreamState: MF_STREAM_STATE_PAUSED");
        goto done; // because not supported
    case MF_STREAM_STATE_RUNNING:
        LogToFile("SetStreamState: MF_STREAM_STATE_RUNNING");
        runningState = true;
        break;
    case MF_STREAM_STATE_STOPPED:
        LogToFile("SetStreamState: MF_STREAM_STATE_STOPPED");
        runningState = false;
        _parent->Shutdown();
        break;
    default:
        LogToFile("SetStreamState: MF_E_INVALID_STATE_TRANSITION");
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
    LogToFile(__FUNCTION__);

    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();

    RETURN_IF_FAILED_WITH_LOGGING(_CheckShutdownRequiresLock());

    *pState = (_isSelected ? MF_STREAM_STATE_RUNNING : MF_STREAM_STATE_STOPPED);

    return hr;
}

HRESULT
SimpleMediaStream::Shutdown()
{
    LogToFile(__FUNCTION__);

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

    _sourceCamera.Reset();
    _currentSourceCameraName.reset();
    _settingsUpdateChannel.reset();
    _overlayImage.Reset();

    _isSelected = false;

    return hr;
}

HRESULT SimpleMediaStream::UpdateSourceCamera(std::wstring_view newCameraName)
{
    LogToFile(__FUNCTION__);

    HRESULT hr = S_OK;

    _cameraList.Clear();
    RETURN_IF_FAILED_WITH_LOGGING(_cameraList.EnumerateDevices());

    bool webcamIsChosen = false;
    ComPtr<IMFActivate> webcamSourceActivator;
    for (UINT32 i = 0; i < _cameraList.Count(); ++i)
    {
        if (_cameraList.GetDeviceName(i) == newCameraName)
        {
            _cameraList.GetDevice(i, &webcamSourceActivator);
            webcamIsChosen = true;
            _currentSourceCameraName.emplace(newCameraName);
            break;
        }
    }

    // Try selecting the first camera which isn't us, since at this point we can only guess the user's preferrence
    if (!webcamIsChosen)
    {
        for (UINT32 i = 0; i < _cameraList.Count(); ++i)
        {
            const auto camName = _cameraList.GetDeviceName(i);
            const bool differentCamera = _currentSourceCameraName.has_value() && camName != *_currentSourceCameraName;
            if (camName != VIRTUAL_CAMERA_NAME && (differentCamera || !_currentSourceCameraName.has_value()))
            {
                RETURN_IF_FAILED_WITH_LOGGING(_cameraList.GetDevice(i, &webcamSourceActivator));
                webcamIsChosen = true;
                _currentSourceCameraName.emplace(camName);
                break;
            }
        }
    }
    if (!webcamIsChosen)
    {
        return E_ABORT;
    }
    ComPtr<IMFMediaSource> realSource;

    RETURN_IF_FAILED_WITH_LOGGING(webcamSourceActivator->ActivateObject(
        __uuidof(IMFMediaSource),
        (void**)realSource.GetAddressOf()));

    ComPtr<IMFAttributes> pAttributes;

    hr = MFCreateAttributes(&pAttributes, 2);

    if (SUCCEEDED(hr))
    {
        ComPtr<IMFMediaTypeHandler> spTypeHandler;

        hr = MFCreateSourceReaderFromMediaSource(
            realSource.Get(),
            pAttributes.Get(),
            &_sourceCamera);
        _spAttributes.Reset();
        _spMediaType = SelectBestMediaType(_sourceCamera.Get());
        RETURN_IF_FAILED_WITH_LOGGING(MFCreateAttributes(&_spAttributes, 10));
        RETURN_IF_FAILED_WITH_LOGGING(_SetStreamAttributes(_spAttributes.Get()));
        if (_spEventQueue)
        {
            _spEventQueue->Shutdown();
            _spEventQueue.Reset();
        }
        RETURN_IF_FAILED_WITH_LOGGING(MFCreateEventQueue(&_spEventQueue));
        _spStreamDesc.Reset();
        // Initialize stream descriptors
        RETURN_IF_FAILED_WITH_LOGGING(MFCreateStreamDescriptor(0, 1, _spMediaType.GetAddressOf(), &_spStreamDesc));

        RETURN_IF_FAILED_WITH_LOGGING(_spStreamDesc->GetMediaTypeHandler(&spTypeHandler));
        RETURN_IF_FAILED_WITH_LOGGING(spTypeHandler->SetCurrentMediaType(_spMediaType.Get()));
        RETURN_IF_FAILED_WITH_LOGGING(_SetStreamDescriptorAttributes(_spStreamDesc.Get()));
        RETURN_IF_FAILED_WITH_LOGGING(_sourceCamera->SetCurrentMediaType((DWORD)MF_SOURCE_READER_FIRST_VIDEO_STREAM, nullptr, _spMediaType.Get()));
    }

    return hr;
}

bool SimpleMediaStream::SyncCurrentSettings()
{
    bool webcamDisabled = false;
    if (!_settingsUpdateChannel.has_value())
    {
        LogToFile("!_settingsUpdateChannel.has_value()");
        _settingsUpdateChannel = SerializedSharedMemory::open(CameraSettingsUpdateChannel::endpoint(), sizeof(CameraSettingsUpdateChannel), false);
    }
    if (!_settingsUpdateChannel)
    {
        LogToFile("!_settingsUpdateChannel");
        return webcamDisabled;
    }

    _settingsUpdateChannel->access([this, &webcamDisabled](auto settingsMemory) {
        LogToFile("_settingsUpdateChannel->access lambda");

        auto settings = reinterpret_cast<CameraSettingsUpdateChannel*>(settingsMemory.data());
        bool cameraNameUpdated = false;
        std::wstring_view newCameraName;
        webcamDisabled = settings->useOverlayImage;
        if (settings->sourceCameraName.has_value())
        {
            std::wstring_view newCameraNameView{ settings->sourceCameraName->data() };
            if (!_currentSourceCameraName.has_value() || *_currentSourceCameraName != newCameraNameView)
            {
                cameraNameUpdated = true;
                newCameraName = newCameraNameView;
            }
        }
        bool cameraUpdated = false;
        if (cameraNameUpdated)
        {
            cameraUpdated = SUCCEEDED(UpdateSourceCamera(std::move(newCameraName)));
        }

        if (!settings->overlayImageSize.has_value())
        {
            LogToFile("!settings->overlayImageSize.has_value()");
            return;
        }

        if (settings->newOverlayImagePosted || !_overlayImage || cameraUpdated)
        {
            LogToFile("settings->newOverlayImagePosted || !_overlayImage || cameraUpdated");
            auto imageChannel =
                SerializedSharedMemory::open(CameraOverlayImageChannel::endpoint(), *settings->overlayImageSize, true);
            if (!imageChannel)
            {
                LogToFile("!imageChannel");
                return;
            }
            imageChannel->access([this, settings](auto imageMemory) {
                LogToFile("imageChannel->access([this, settings](auto imageMemory)");
                ComPtr<IStream> imageStream = SHCreateMemStream(imageMemory.data(), static_cast<UINT>(imageMemory.size()));
                if (!imageStream)
                {
                    LogToFile("!imageStream");
                    return;
                }
                if (auto imageSample = LoadImageAsSample(imageStream, _spMediaType.Get()))
                {
                    LogToFile("auto imageSample = LoadImageAsSample(imageStream, _spMediaType.Get())");
                    _overlayImage = imageSample;
                    settings->newOverlayImagePosted = false;
                }
                else
                {
                    LogToFile("Failed to load image");
                }
            });
        }
    });
    return webcamDisabled;
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
    LogToFile(__FUNCTION__);

    HRESULT hr = S_OK;

    if (nullptr == pAttributeStore)
    {
        return E_INVALIDARG;
    }

    RETURN_IF_FAILED_WITH_LOGGING(pAttributeStore->SetGUID(MF_DEVICESTREAM_STREAM_CATEGORY, PINNAME_VIDEO_CAPTURE));
    RETURN_IF_FAILED_WITH_LOGGING(pAttributeStore->SetUINT32(MF_DEVICESTREAM_STREAM_ID, 0));
    RETURN_IF_FAILED_WITH_LOGGING(pAttributeStore->SetUINT32(MF_DEVICESTREAM_FRAMESERVER_SHARED, 1));
    RETURN_IF_FAILED_WITH_LOGGING(pAttributeStore->SetUINT32(MF_DEVICESTREAM_ATTRIBUTE_FRAMESOURCE_TYPES, _MFFrameSourceTypes::MFFrameSourceTypes_Color));

    return hr;
}

HRESULT
SimpleMediaStream::_SetStreamDescriptorAttributes(
    _In_ IMFAttributes* pAttributeStore)
{
    LogToFile(__FUNCTION__);

    HRESULT hr = S_OK;

    if (nullptr == pAttributeStore)
    {
        return E_INVALIDARG;
    }

    RETURN_IF_FAILED_WITH_LOGGING(pAttributeStore->SetGUID(MF_DEVICESTREAM_STREAM_CATEGORY, PINNAME_VIDEO_CAPTURE));
    RETURN_IF_FAILED_WITH_LOGGING(pAttributeStore->SetUINT32(MF_DEVICESTREAM_STREAM_ID, 0));
    RETURN_IF_FAILED_WITH_LOGGING(pAttributeStore->SetUINT32(MF_DEVICESTREAM_FRAMESERVER_SHARED, 1));
    RETURN_IF_FAILED_WITH_LOGGING(pAttributeStore->SetUINT32(MF_DEVICESTREAM_ATTRIBUTE_FRAMESOURCE_TYPES, _MFFrameSourceTypes::MFFrameSourceTypes_Color));

    return hr;
}
