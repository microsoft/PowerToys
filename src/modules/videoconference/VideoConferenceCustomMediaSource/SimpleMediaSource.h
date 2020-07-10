#pragma once

#include "stdafx.h"

class SimpleMediaStream;

class __declspec(uuid("{8a6954dc-7baa-486b-a7a3-a3cc09246487}"))
    SimpleMediaSource : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IMFMediaEventGenerator, IMFMediaSource, IMFMediaSourceEx, IMFGetService, IKsControl>
{
    enum class SourceState
    {
        Invalid,
        Stopped,
        Started,
        Shutdown
    };

public:
    // IMFMediaEventGenerator
    IFACEMETHOD(BeginGetEvent)
    (_In_ IMFAsyncCallback* pCallback, _In_ IUnknown* punkState);
    IFACEMETHOD(EndGetEvent)
    (_In_ IMFAsyncResult* pResult, _Out_ IMFMediaEvent** ppEvent);
    IFACEMETHOD(GetEvent)
    (DWORD dwFlags, _Out_ IMFMediaEvent** ppEvent);
    IFACEMETHOD(QueueEvent)
    (MediaEventType met, REFGUID guidExtendedType, HRESULT hrStatus, _In_ const PROPVARIANT* pvValue);

    // IMFMediaSource
    IFACEMETHOD(CreatePresentationDescriptor)
    (_Out_ IMFPresentationDescriptor** ppPresentationDescriptor);
    IFACEMETHOD(GetCharacteristics)
    (_Out_ DWORD* pdwCharacteristics);
    IFACEMETHOD(Pause)
    ();
    IFACEMETHOD(Shutdown)
    ();
    IFACEMETHOD(Start)
    (_In_ IMFPresentationDescriptor* pPresentationDescriptor, _In_ const GUID* pguidTimeFormat, _In_ const PROPVARIANT* pvarStartPosition);
    IFACEMETHOD(Stop)
    ();

    // IMFMediaSourceEx
    IFACEMETHOD(GetSourceAttributes)
    (_COM_Outptr_ IMFAttributes** ppAttributes);
    IFACEMETHOD(GetStreamAttributes)
    (DWORD dwStreamIdentifier, _COM_Outptr_ IMFAttributes** ppAttributes);
    IFACEMETHOD(SetD3DManager)
    (_In_opt_ IUnknown* pManager);

    // IMFGetService
    IFACEMETHOD(GetService)
    (_In_ REFGUID guidService, _In_ REFIID riid, _Out_ LPVOID* ppvObject);

    // IKsControl
    IFACEMETHOD(KsProperty)
    (_In_reads_bytes_(ulPropertyLength) PKSPROPERTY pProperty,
     _In_ ULONG ulPropertyLength,
     _Inout_updates_to_(ulDataLength, *pBytesReturned) LPVOID pPropertyData,
     _In_ ULONG ulDataLength,
     _Out_ ULONG* pBytesReturned);
    IFACEMETHOD(KsMethod)
    (_In_reads_bytes_(ulMethodLength) PKSMETHOD pMethod,
     _In_ ULONG ulMethodLength,
     _Inout_updates_to_(ulDataLength, *pBytesReturned) LPVOID pMethodData,
     _In_ ULONG ulDataLength,
     _Out_ ULONG* pBytesReturned);
    IFACEMETHOD(KsEvent)
    (_In_reads_bytes_opt_(ulEventLength) PKSEVENT pEvent,
     _In_ ULONG ulEventLength,
     _Inout_updates_to_(ulDataLength, *pBytesReturned) LPVOID pEventData,
     _In_ ULONG ulDataLength,
     _Out_opt_ ULONG* pBytesReturned);

public:
    HRESULT RuntimeClassInitialize();

private:
    HRESULT _CheckShutdownRequiresLock();
    HRESULT _ValidatePresentationDescriptor(_In_ IMFPresentationDescriptor* pPresentationDescriptor);

    CriticalSection _critSec;
    SourceState _sourceState{ SourceState::Invalid };
    ComPtr<IMFMediaEventQueue> _spEventQueue;
    ComPtr<IMFPresentationDescriptor> _spPresentationDescriptor;
    ComPtr<IMFAttributes> _spAttributes;

    bool _wasStreamPreviouslySelected; // maybe makes more sense as a property of the stream
    ComPtr<SimpleMediaStream> _stream;
};

CoCreatableClass(SimpleMediaSource);
