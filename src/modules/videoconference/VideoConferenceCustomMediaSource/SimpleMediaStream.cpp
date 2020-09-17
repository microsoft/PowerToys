#include "stdafx.h"

#include "ImageLoader.h"

#include <vector>
#include <algorithm>
#include <thread>

#include "SimpleMediaSource.h"
#include "SimpleMediaStream.h"
#include <common\user.h>

#include "Logging.h"

HRESULT CopyAttribute(IMFAttributes* pSrc, IMFAttributes* pDest, const GUID& key);

const static std::wstring_view MODULE_NAME = L"Video Conference";
const static std::wstring_view VIRTUAL_CAMERA_NAME = L"PowerToys VideoConference";

namespace
{
    constexpr std::array<unsigned char, 3> blackColor = { 0, 0, 0 };
    // clang-format off
    unsigned char bmpPixelData[58] = {
	      0x42, 0x4D, 0x3A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x36, 0x00,
	      0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00,
	      0x00, 0x00, 0x01, 0x00, 0x18, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00,
	      0x00, 0x00, 0xC4, 0x0E, 0x00, 0x00, 0xC4, 0x0E, 0x00, 0x00, 0x00, 0x00,
	      0x00, 0x00, 0x00, 0x00, 0x00, 0x00, blackColor[0], blackColor[1], blackColor[2], 0x00
    };
    // clang-format on
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

bool areSame(double lhs, double rhs)
{
    const double EPSILON = 0.00000001;
    return (fabs(lhs - rhs) < EPSILON);
}

ComPtr<IMFMediaType> SelectBestMediaType(IMFSourceReader* reader)
{
    VERBOSE_LOG;
    std::vector<ComPtr<IMFMediaType>> supportedMediaTypes;

    auto typeFramerate = [](IMFMediaType* type) {
        UINT32 framerateNum = 0, framerateDenum = 1;
        MFGetAttributeRatio(type, MF_MT_FRAME_RATE, &framerateNum, &framerateDenum);
        const float framerate = static_cast<float>(framerateNum) / framerateDenum;
        return framerate;
    };

    bool is16by9RatioAvailable = false;

    for (DWORD tyIdx = 0;; ++tyIdx)
    {
        IMFMediaType* nextType = nullptr;
        HRESULT hr = reader->GetNativeMediaType((DWORD)MF_SOURCE_READER_FIRST_VIDEO_STREAM, tyIdx, &nextType);
        if (!nextType)
        {
            break;
        }

        UINT32 width = 0;
        UINT32 height = 0;
        MFGetAttributeSize(nextType, MF_MT_FRAME_SIZE, &width, &height);

        double aspectRatio = static_cast<double>(width) / height;

        GUID subtype{};
        nextType->GetGUID(MF_MT_SUBTYPE, &subtype);

        //LogToFile(std::string("Available format: ") +
        //          toMediaTypeString(subtype) +
        //          std::string(", width=") +
        //          std::to_string(width) +
        //          std::string(", height=") +
        //          std::to_string(height) +
        //          std::string(", aspect ratio=") +
        //          std::to_string(aspectRatio));

        if (subtype != MFVideoFormat_YUY2 &&
            subtype != MFVideoFormat_RGB24 &&
            subtype != MFVideoFormat_MJPG &&
            subtype != MFVideoFormat_NV12)
        {
            continue;
        }

        if (areSame(aspectRatio, 16. / 9.))
        {
            is16by9RatioAvailable = true;
        }

        constexpr float minimalAcceptableFramerate = 15.f;
        // Skip low frame types
        if (typeFramerate(nextType) < minimalAcceptableFramerate)
        {
            continue;
        }

        supportedMediaTypes.emplace_back(nextType);

        if (hr == MF_E_NO_MORE_TYPES || FAILED(hr))
        {
            break;
        }
    }

    if (is16by9RatioAvailable)
    {
        // Remove all types with non 16 by 9 ratio
        supportedMediaTypes.erase(std::remove_if(begin(supportedMediaTypes), end(supportedMediaTypes), [](ComPtr<IMFMediaType>& ptr) {
                                      UINT32 width = 0, height = 0;
                                      MFGetAttributeSize(ptr.Get(), MF_MT_FRAME_SIZE, &width, &height);

                                      double ratio = static_cast<double>(width) / height;
                                      return !areSame(ratio, 16. / 9.);
                                  }),
                                  end(supportedMediaTypes));
    }

    UINT64 maxResolution = 0;

    for (auto& type : supportedMediaTypes)
    {
        UINT32 width = 0;
        UINT32 height = 0;

        MFGetAttributeSize(type.Get(), MF_MT_FRAME_SIZE, &width, &height);
        const UINT64 curResolutionMult = static_cast<UINT64>(width) * height;
        if (curResolutionMult >= maxResolution)
        {
            maxResolution = curResolutionMult;
        }
    }

    // Remove all types with non-optimal resolution
    supportedMediaTypes.erase(std::remove_if(begin(supportedMediaTypes), end(supportedMediaTypes), [maxResolution](ComPtr<IMFMediaType>& ptr) {
                                  UINT32 w = 0, h = 0;
                                  MFGetAttributeSize(ptr.Get(), MF_MT_FRAME_SIZE, &w, &h);
                                  const UINT64 curResolutionMult = static_cast<UINT64>(w) * h;
                                  return curResolutionMult != maxResolution;
                              }),
                              end(supportedMediaTypes));

    // Desc-sort by frame_rate
    std::sort(begin(supportedMediaTypes), end(supportedMediaTypes), [typeFramerate](ComPtr<IMFMediaType>& lhs, ComPtr<IMFMediaType>& rhs) {
        return typeFramerate(lhs.Get()) > typeFramerate(rhs.Get());
    });

    return std::move(supportedMediaTypes[0]);
}

HRESULT
SimpleMediaStream::RuntimeClassInitialize(
    _In_ SimpleMediaSource* pSource)
{
    VERBOSE_LOG;
    HRESULT hr = S_OK;

    if (nullptr == pSource)
    {
        return E_INVALIDARG;
    }
    RETURN_IF_FAILED_WITH_LOGGING(pSource->QueryInterface(IID_PPV_ARGS(&_parent)));

    const auto newSettings = SyncCurrentSettings();
    UpdateSourceCamera(newSettings.newCameraName);
    ComPtr<IStream> blackBMPImage = SHCreateMemStream(bmpPixelData, sizeof(bmpPixelData));
    if (!blackBMPImage || !_spMediaType)
    {
        return E_FAIL;
    }

    if (_spMediaType)
    {
        if (newSettings.overlayImage)
        {
            _overlayImage = LoadImageAsSample(newSettings.overlayImage, _spMediaType.Get());
        }
        _blackImage = LoadImageAsSample(blackBMPImage, _spMediaType.Get());
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
    auto lock = _critSec.Lock();
    HRESULT hr{};
    RETURN_IF_FAILED_WITH_LOGGING(_CheckShutdownRequiresLock());

    const auto syncedSettings = SyncCurrentSettings();

    // Source camera is updated, we must shutdown ourselves, since we can't modify presentation descriptor while running
    if (!syncedSettings.newCameraName.empty())
    {
        std::thread{ [this] {
            auto lock = _critSec.Lock();
            SetStreamState(MF_STREAM_STATE_STOPPED);
        } }.detach();
    }
    else if (syncedSettings.overlayImage)
    {
        _overlayImage = LoadImageAsSample(syncedSettings.overlayImage, _spMediaType.Get());
    }

    // Request the first video frame.

    ComPtr<IMFSample> sample;
    DWORD streamFlags = 0;

    const auto readSampleResult = _sourceCamera->ReadSample(
        (DWORD)MF_SOURCE_READER_FIRST_VIDEO_STREAM,
        0,
        nullptr,
        &streamFlags,
        nullptr,
        &sample);

    IMFSample* outputSample = syncedSettings.webcamDisabled ? _overlayImage.Get() : sample.Get();
    const bool noSampleAvailable = !outputSample;

    // use black image instead, it should be always available
    if (noSampleAvailable)
    {
        outputSample = _blackImage.Get();
    }
    RETURN_IF_FAILED_WITH_LOGGING(outputSample->SetSampleTime(MFGetSystemTime()));
    RETURN_IF_FAILED_WITH_LOGGING(outputSample->SetSampleDuration(333333));
    if (pToken != nullptr)
    {
        RETURN_IF_FAILED_WITH_LOGGING(outputSample->SetUnknown(MFSampleExtension_Token, pToken));
    }

    if (FAILED(readSampleResult) && syncedSettings.newCameraName.empty())
    {
        // Try to reinit webcamera, since it could've been unavailable due to concurrent access from 3rd-party apps
        UpdateSourceCamera(_currentSourceCameraName ? *_currentSourceCameraName : L"");
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
        _parent->Shutdown();
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

    RETURN_IF_FAILED_WITH_LOGGING(_CheckShutdownRequiresLock());

    *pState = (_isSelected ? MF_STREAM_STATE_RUNNING : MF_STREAM_STATE_STOPPED);

    return hr;
}

HRESULT
SimpleMediaStream::Shutdown()
{
    VERBOSE_LOG;
    HRESULT hr = S_OK;

    if (_settingsUpdateChannel.has_value())
    {
        _settingsUpdateChannel->access([this](auto settingsMemory) {
            auto settings = reinterpret_cast<CameraSettingsUpdateChannel*>(settingsMemory._data);

            settings->cameraInUse = false;
        });
    }
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
    VERBOSE_LOG;
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

SimpleMediaStream::SyncedSettings SimpleMediaStream::SyncCurrentSettings()
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
