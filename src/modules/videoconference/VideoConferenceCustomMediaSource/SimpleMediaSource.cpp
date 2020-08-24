#include "stdafx.h"

#include "SimpleMediaSource.h"
#include "SimpleMediaStream.h"

#include "Logging.h"

HRESULT
SimpleMediaSource::RuntimeClassInitialize()
{
    VERBOSE_LOG;
    HRESULT hr = S_OK;

    RETURN_IF_FAILED_WITH_LOGGING(MFCreateAttributes(&_spAttributes, 10));
    RETURN_IF_FAILED_WITH_LOGGING(MFCreateEventQueue(&_spEventQueue));
    RETURN_IF_FAILED_WITH_LOGGING(MakeAndInitialize<SimpleMediaStream>(&_stream, this));
    {
        ComPtr<IMFStreamDescriptor> streamDescriptor(_stream.Get()->_spStreamDesc.Get());
        RETURN_IF_FAILED_WITH_LOGGING(MFCreatePresentationDescriptor(1, streamDescriptor.GetAddressOf(), &_spPresentationDescriptor));
    }
    _wasStreamPreviouslySelected = false;
    _sourceState = SourceState::Stopped;

    return hr;
}

// IMFMediaEventGenerator methods.
IFACEMETHODIMP
SimpleMediaSource::BeginGetEvent(
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
SimpleMediaSource::EndGetEvent(
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
SimpleMediaSource::GetEvent(
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
SimpleMediaSource::QueueEvent(
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

// IMFMediaSource methods
IFACEMETHODIMP
SimpleMediaSource::CreatePresentationDescriptor(
    _COM_Outptr_ IMFPresentationDescriptor** ppPresentationDescriptor)
{
    VERBOSE_LOG;
    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();

    if (ppPresentationDescriptor == nullptr)
    {
        return E_POINTER;
    }
    *ppPresentationDescriptor = nullptr;

    RETURN_IF_FAILED_WITH_LOGGING(_CheckShutdownRequiresLock());
    RETURN_IF_FAILED_WITH_LOGGING(_spPresentationDescriptor->Clone(ppPresentationDescriptor));

    return hr;
}

IFACEMETHODIMP
SimpleMediaSource::GetCharacteristics(
    _Out_ DWORD* pdwCharacteristics)
{
    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();

    if (nullptr == pdwCharacteristics)
    {
        return E_POINTER;
    }
    *pdwCharacteristics = 0;

    RETURN_IF_FAILED_WITH_LOGGING(_CheckShutdownRequiresLock());
    *pdwCharacteristics = MFMEDIASOURCE_IS_LIVE;

    return hr;
}

IFACEMETHODIMP
SimpleMediaSource::Pause()
{
    // Pause() not required/needed
    return MF_E_INVALID_STATE_TRANSITION;
}

IFACEMETHODIMP
SimpleMediaSource::Shutdown()
{
    VERBOSE_LOG;
    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();

    _sourceState = SourceState::Shutdown;

    _spAttributes.Reset();
    _spPresentationDescriptor.Reset();

    if (_spEventQueue != nullptr)
    {
        _spEventQueue->Shutdown();
        _spEventQueue.Reset();
    }

    if (_stream != nullptr)
    {
        _stream.Get()->Shutdown();
        _stream.Reset();
    }

    return hr;
}

IFACEMETHODIMP
SimpleMediaSource::Start(
    _In_ IMFPresentationDescriptor* pPresentationDescriptor,
    _In_opt_ const GUID* pguidTimeFormat,
    _In_ const PROPVARIANT* pvarStartPos)
{
    VERBOSE_LOG;
    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();
    DWORD count = 0;
    PROPVARIANT startTime;
    BOOL selected = false;
    ComPtr<IMFStreamDescriptor> streamDesc;
    DWORD streamIndex = 0;

    if (pPresentationDescriptor == nullptr || pvarStartPos == nullptr)
    {
        return E_INVALIDARG;
    }
    else if (pguidTimeFormat != nullptr && *pguidTimeFormat != GUID_NULL)
    {
        return MF_E_UNSUPPORTED_TIME_FORMAT;
    }

    RETURN_IF_FAILED_WITH_LOGGING(_CheckShutdownRequiresLock());

    if (!(_sourceState != SourceState::Stopped || _sourceState != SourceState::Shutdown))
    {
        return MF_E_INVALID_STATE_TRANSITION;
    }

    _sourceState = SourceState::Started;

    // This checks the passed in PresentationDescriptor matches the member of streams we
    // have defined internally and that at least one stream is selected
    RETURN_IF_FAILED_WITH_LOGGING(_ValidatePresentationDescriptor(pPresentationDescriptor));
    RETURN_IF_FAILED_WITH_LOGGING(pPresentationDescriptor->GetStreamDescriptorCount(&count));
    RETURN_IF_FAILED_WITH_LOGGING(InitPropVariantFromInt64(MFGetSystemTime(), &startTime));

    // Send event that the source started. Include error code in case it failed.
    RETURN_IF_FAILED_WITH_LOGGING(_spEventQueue->QueueEventParamVar(MESourceStarted,
                                                                    GUID_NULL,
                                                                    hr,
                                                                    &startTime));

    // We're hardcoding this to the first descriptor
    // since this sample is a single stream sample.  For
    // multiple streams, we need to walk the list of streams
    // and for each selected stream, send the MEUpdatedStream
    // or MENewStream event along with the MEStreamStarted
    // event.
    RETURN_IF_FAILED_WITH_LOGGING(pPresentationDescriptor->GetStreamDescriptorByIndex(0,
                                                                                      &selected,
                                                                                      &streamDesc));
    RETURN_IF_FAILED_WITH_LOGGING(streamDesc->GetStreamIdentifier(&streamIndex));
    if (streamIndex >= 1)
    {
        return MF_E_INVALIDSTREAMNUMBER;
    }

    if (selected)
    {
        ComPtr<IUnknown> spunkStream;
        MediaEventType met = (_wasStreamPreviouslySelected ? MEUpdatedStream : MENewStream);

        // Update our internal PresentationDescriptor
        RETURN_IF_FAILED_WITH_LOGGING(_spPresentationDescriptor->SelectStream(streamIndex));
        RETURN_IF_FAILED_WITH_LOGGING(_stream.Get()->SetStreamState(MF_STREAM_STATE_RUNNING));
        RETURN_IF_FAILED_WITH_LOGGING(_stream.As(&spunkStream));

        // Send the MEUpdatedStream/MENewStream to our source event
        // queue.
        RETURN_IF_FAILED_WITH_LOGGING(_spEventQueue->QueueEventParamUnk(met,
                                                                        GUID_NULL,
                                                                        S_OK,
                                                                        spunkStream.Get()));

        // But for our stream started (MEStreamStarted), we post to our
        // stream event queue.
        RETURN_IF_FAILED_WITH_LOGGING(_stream.Get()->QueueEvent(MEStreamStarted,
                                                                GUID_NULL,
                                                                S_OK,
                                                                &startTime));
    }
    _wasStreamPreviouslySelected = selected;

    return hr;
}

IFACEMETHODIMP
SimpleMediaSource::Stop()
{
    VERBOSE_LOG;
    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();
    PROPVARIANT stopTime;
    DWORD count = 0;
    MF_STREAM_STATE state;

    if (_sourceState != SourceState::Started)
    {
        return MF_E_INVALID_STATE_TRANSITION;
    }

    _sourceState = SourceState::Stopped;

    RETURN_IF_FAILED_WITH_LOGGING(_CheckShutdownRequiresLock());
    RETURN_IF_FAILED_WITH_LOGGING(InitPropVariantFromInt64(MFGetSystemTime(), &stopTime));
    RETURN_IF_FAILED_WITH_LOGGING(_spPresentationDescriptor->GetStreamDescriptorCount(&count));

    // Deselect the streams and send the stream stopped events.
    RETURN_IF_FAILED_WITH_LOGGING(_stream.Get()->GetStreamState(&state));
    _wasStreamPreviouslySelected = (state == MF_STREAM_STATE_RUNNING);
    RETURN_IF_FAILED_WITH_LOGGING(_stream.Get()->SetStreamState(MF_STREAM_STATE_STOPPED));
    _spPresentationDescriptor->DeselectStream(0);
    RETURN_IF_FAILED_WITH_LOGGING(_stream.Get()->QueueEvent(MEStreamStopped, GUID_NULL, hr, &stopTime));
    RETURN_IF_FAILED_WITH_LOGGING(_spEventQueue->QueueEventParamVar(MESourceStopped, GUID_NULL, hr, &stopTime));

    return hr;
}

// IMFMediaSourceEx
IFACEMETHODIMP
SimpleMediaSource::GetSourceAttributes(
    _COM_Outptr_ IMFAttributes** sourceAttributes)
{
    HRESULT hr = S_OK;

    auto lock = _critSec.Lock();

    if (nullptr == sourceAttributes)
    {
        return E_POINTER;
    }

    RETURN_IF_FAILED_WITH_LOGGING(_CheckShutdownRequiresLock());

    *sourceAttributes = nullptr;
    if (_spAttributes.Get() == nullptr)
    {
        ComPtr<IMFSensorProfileCollection> profileCollection;
        ComPtr<IMFSensorProfile> profile;

        // Create our source attribute store.
        RETURN_IF_FAILED_WITH_LOGGING(MFCreateAttributes(_spAttributes.GetAddressOf(), 1));

        // Create an empty profile collection...
        RETURN_IF_FAILED_WITH_LOGGING(MFCreateSensorProfileCollection(&profileCollection));

        // In this example since we have just one stream, we only have one
        // pin to add:  Pin0.

        // Legacy profile is mandatory.  This is to ensure non-profile
        // aware applications can still function, but with degraded
        // feature sets.
        RETURN_IF_FAILED_WITH_LOGGING(MFCreateSensorProfile(KSCAMERAPROFILE_Legacy, 0, nullptr, profile.ReleaseAndGetAddressOf()));
        RETURN_IF_FAILED_WITH_LOGGING(profile->AddProfileFilter(0, L"((RES==;FRT<=30,1;SUT==))"));
        RETURN_IF_FAILED_WITH_LOGGING(profileCollection->AddProfile(profile.Get()));

        // High Frame Rate profile will only allow >=60fps.
        RETURN_IF_FAILED_WITH_LOGGING(MFCreateSensorProfile(KSCAMERAPROFILE_HighFrameRate, 0, nullptr, profile.ReleaseAndGetAddressOf()));
        RETURN_IF_FAILED_WITH_LOGGING(profile->AddProfileFilter(0, L"((RES==;FRT>=60,1;SUT==))"));
        RETURN_IF_FAILED_WITH_LOGGING(profileCollection->AddProfile(profile.Get()));

        // Se the profile collection to the attribute store of the IMFTransform.
        RETURN_IF_FAILED_WITH_LOGGING(_spAttributes->SetUnknown(MF_DEVICEMFT_SENSORPROFILE_COLLECTION,
                                                                profileCollection.Get()));
    }

    return _spAttributes.CopyTo(sourceAttributes);
}

IFACEMETHODIMP
SimpleMediaSource::GetStreamAttributes(
    DWORD dwStreamIdentifier,
    _COM_Outptr_ IMFAttributes** ppAttributes)
{
    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();

    if (ppAttributes == nullptr)
    {
        return E_POINTER;
    }
    *ppAttributes = nullptr;

    RETURN_IF_FAILED_WITH_LOGGING(_CheckShutdownRequiresLock());
    if (dwStreamIdentifier >= 1)
    {
        return MF_E_INVALIDSTREAMNUMBER;
    }
    else
    {
        *ppAttributes = _stream.Get()->_spAttributes.Get();
        (*ppAttributes)->AddRef();
    }

    return hr;
}

IFACEMETHODIMP
SimpleMediaSource::SetD3DManager(
    _In_opt_ IUnknown* /*pManager*/
)
{
    // Return code is ignored by the frame work, this is a
    // best effort attempt to inform the media source of the
    // DXGI manager to use if DX surface support is available.

    return E_NOTIMPL;
}
struct __declspec(uuid("a1f58958-a5aa-412f-af20-1b7f1242dba0")) IMFDeviceController;

// IMFGetService methods
_Use_decl_annotations_
    IFACEMETHODIMP
    SimpleMediaSource::GetService(
        _In_ REFGUID,
        _In_ REFIID iid,
        _Out_ LPVOID* ppvObject)
{
    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();

    const bool wantsIMFDeviceController = IsEqualIID(iid, __uuidof(IMFDeviceController));
    auto stream = _stream.Get();
    const bool goingToRestart = wantsIMFDeviceController && _wasStreamPreviouslySelected && stream && stream->_isSelected && _sourceState == SourceState::Started;
    if (goingToRestart)
    {
        // GetService w /IMFDeviceController is called when we're already started -> stopping to prepare for restart
        stream->SetStreamState(MF_STREAM_STATE_STOPPED);
        return E_POINTER;
    }

    RETURN_IF_FAILED_WITH_LOGGING(_CheckShutdownRequiresLock());

    if (!ppvObject)
    {
        return E_POINTER;
    }
    *ppvObject = nullptr;

    // We have no supported service, just return
    // MF_E_UNSUPPORTED_SERVICE for all calls.

    return MF_E_UNSUPPORTED_SERVICE;
}

// IKsControl methods
_Use_decl_annotations_
    IFACEMETHODIMP
    SimpleMediaSource::KsProperty(
        _In_reads_bytes_(ulPropertyLength) PKSPROPERTY,
        _In_ ULONG,
        _Inout_updates_to_(ulDataLength, *pBytesReturned) LPVOID,
        _In_ ULONG,
        _Out_ ULONG*)
{
    // ERROR_SET_NOT_FOUND is the standard error code returned
    // by the AV Stream driver framework when a miniport
    // driver does not register a handler for a KS operation.
    // We want to mimic the driver behavior here if we don't
    // support controls.
    return HRESULT_FROM_WIN32(ERROR_SET_NOT_FOUND);
}

_Use_decl_annotations_
    IFACEMETHODIMP
    SimpleMediaSource::KsMethod(
        _In_reads_bytes_(ulMethodLength) PKSMETHOD,
        _In_ ULONG,
        _Inout_updates_to_(ulDataLength, *pBytesReturned) LPVOID,
        _In_ ULONG,
        _Out_ ULONG*)
{
    Shutdown();
    return HRESULT_FROM_WIN32(ERROR_SET_NOT_FOUND);
}

_Use_decl_annotations_
    IFACEMETHODIMP
    SimpleMediaSource::KsEvent(
        _In_reads_bytes_opt_(ulEventLength) PKSEVENT,
        _In_ ULONG,
        _Inout_updates_to_(ulDataLength, *pBytesReturned) LPVOID,
        _In_ ULONG,
        _Out_opt_ ULONG*)
{
    return HRESULT_FROM_WIN32(ERROR_SET_NOT_FOUND);
}

/// Internal methods.
HRESULT
SimpleMediaSource::_CheckShutdownRequiresLock()
{
    if (_sourceState == SourceState::Shutdown)
    {
        return MF_E_SHUTDOWN;
    }

    if (_spEventQueue == nullptr || _stream == nullptr)
    {
        return E_UNEXPECTED;
    }

    return S_OK;
}

HRESULT
SimpleMediaSource::_ValidatePresentationDescriptor(
    _In_ IMFPresentationDescriptor* pPD)
{
    HRESULT hr = S_OK;
    DWORD cStreams = 0;
    bool anySelected = false;

    if (pPD == nullptr)
    {
        return E_INVALIDARG;
    }

    // The caller's PD must have the same number of streams as ours.
    RETURN_IF_FAILED_WITH_LOGGING(pPD->GetStreamDescriptorCount(&cStreams));
    if (SUCCEEDED(hr) && (cStreams != 1))
    {
        return E_INVALIDARG;
    }

    // The caller must select at least one stream.
    for (UINT32 i = 0; i < cStreams; ++i)
    {
        ComPtr<IMFStreamDescriptor> spSD;
        BOOL fSelected = FALSE;
        DWORD dwId = 0;

        RETURN_IF_FAILED_WITH_LOGGING(pPD->GetStreamDescriptorByIndex(i, &fSelected, &spSD));

        anySelected |= !!fSelected;

        RETURN_IF_FAILED_WITH_LOGGING(spSD->GetStreamIdentifier(&dwId));
        if (dwId >= 1)
        {
            return E_INVALIDARG;
        }
    }

    if (!anySelected)
    {
        return E_INVALIDARG;
    }

    return hr;
}
