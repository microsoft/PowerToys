#pragma once

#include "stdafx.h"

#include <SerializedSharedMemory.h>
#include <CameraStateUpdateChannels.h>

class SimpleMediaSource;

class DeviceList
{
    UINT32 m_numberDevices;
    IMFActivate** m_ppDevices = nullptr;
    wchar_t** m_deviceFriendlyNames = nullptr;

public:
    DeviceList() :
        m_ppDevices(NULL), m_numberDevices(0)
    {
    }
    ~DeviceList()
    {
        Clear();
    }

    UINT32 Count() const { return m_numberDevices; }

    void Clear();
    HRESULT EnumerateDevices();
    HRESULT GetDevice(UINT32 index, IMFActivate** ppActivate);
    std::wstring_view GetDeviceName(UINT32 index);
};

class SimpleMediaStream : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IMFMediaEventGenerator, IMFMediaStream, IMFMediaStream2>
{
    friend class SimpleMediaSource;

public:
    // IMFMediaEventGenerator
    IFACEMETHOD(BeginGetEvent)
    (IMFAsyncCallback* pCallback, IUnknown* punkState);
    IFACEMETHOD(EndGetEvent)
    (IMFAsyncResult* pResult, IMFMediaEvent** ppEvent);
    IFACEMETHOD(GetEvent)
    (DWORD dwFlags, IMFMediaEvent** ppEvent);
    IFACEMETHOD(QueueEvent)
    (MediaEventType met, REFGUID guidExtendedType, HRESULT hrStatus, const PROPVARIANT* pvValue);

    // IMFMediaStream
    IFACEMETHOD(GetMediaSource)
    (IMFMediaSource** ppMediaSource);
    IFACEMETHOD(GetStreamDescriptor)
    (IMFStreamDescriptor** ppStreamDescriptor);
    IFACEMETHOD(RequestSample)
    (IUnknown* pToken);

    // IMFMediaStream2
    IFACEMETHOD(SetStreamState)
    (MF_STREAM_STATE state);
    IFACEMETHOD(GetStreamState)
    (_Out_ MF_STREAM_STATE* pState);

    // Non-interface methods.
    HRESULT RuntimeClassInitialize(_In_ SimpleMediaSource* pSource);
    HRESULT Shutdown();

protected:
    struct SyncedSettings
    {
        bool webcamDisabled = false;
        std::wstring newCameraName;
        ComPtr<IStream> overlayImage;
    };

    HRESULT UpdateSourceCamera(std::wstring_view newCameraName);
    SyncedSettings SyncCurrentSettings();

    HRESULT _CheckShutdownRequiresLock();
    HRESULT _SetStreamAttributes(IMFAttributes* pAttributeStore);
    HRESULT _SetStreamDescriptorAttributes(IMFAttributes* pAttributeStore);

    CriticalSection _critSec;

    ComPtr<IMFMediaSource> _parent;
    ComPtr<IMFMediaEventQueue> _spEventQueue;
    ComPtr<IMFAttributes> _spAttributes;
    ComPtr<IMFMediaType> _spMediaType;
    ComPtr<IMFStreamDescriptor> _spStreamDesc;
    bool _isShutdown = false;
    bool _isSelected = false;

    DeviceList _cameraList;
    ComPtr<IMFSourceReader> _sourceCamera;
    ComPtr<IMFSample> _overlayImage;

    ComPtr<IMFSample> _blackImage;

    std::optional<SerializedSharedMemory> _settingsUpdateChannel;
    std::optional<std::wstring> _currentSourceCameraName;
};
