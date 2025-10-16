#pragma once

#include <initguid.h>

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <dshow.h>

#include <winrt/base.h>
#include <wil/resource.h>
#include <wil/com.h>

#include <CameraStateUpdateChannels.h>
#include <SerializedSharedMemory.h>

#include "VideoCaptureDevice.h"

#include <mutex>
#include <condition_variable>

struct VideoCaptureProxyPin;
struct IMFSample;
struct IMFMediaType;

inline const wchar_t CAMERA_NAME[] = L"PowerToys VideoConference Mute";

struct VideoCaptureProxyFilter : winrt::implements<VideoCaptureProxyFilter, IBaseFilter, IAMFilterMiscFlags>
{
    // BLOCK START: member accessed concurrently
    wil::com_ptr_nothrow<VideoCaptureProxyPin> _outPin;
    IMediaSample* _pending_frame = nullptr;
    std::atomic_bool _shutdown_request = false;
    std::optional<SerializedSharedMemory> _settingsUpdateChannel;
    std::optional<std::wstring> _currentSourceCameraName;
    wil::com_ptr_nothrow<IMFSample> _blankImage;
    wil::com_ptr_nothrow<IMFSample> _overlayImage;
    wil::com_ptr_nothrow<IMFMediaType> _targetMediaType;
    // BLOCK END: member accessed concurrently

    std::mutex _worker_mutex;
    std::condition_variable _worker_cv;

    FILTER_STATE _state = State_Stopped;
    wil::com_ptr_nothrow<IReferenceClock> _clock;
    IFilterGraph* _graph = nullptr;
    std::optional<VideoCaptureDevice> _captureDevice;

    std::thread _worker_thread;

    VideoCaptureProxyFilter();
    ~VideoCaptureProxyFilter();

    struct SyncedSettings
    {
        bool webcamDisabled = false;
        std::wstring newCameraName;
        wil::com_ptr_nothrow<IStream> overlayImage;
    };

    SyncedSettings SyncCurrentSettings();

    HRESULT STDMETHODCALLTYPE Stop(void) override;
    HRESULT STDMETHODCALLTYPE Pause(void) override;
    HRESULT STDMETHODCALLTYPE Run(REFERENCE_TIME tStart) override;
    HRESULT STDMETHODCALLTYPE GetState(DWORD dwMilliSecsTimeout, FILTER_STATE* State) override;
    HRESULT STDMETHODCALLTYPE SetSyncSource(IReferenceClock* pClock) override;
    HRESULT STDMETHODCALLTYPE GetSyncSource(IReferenceClock** pClock) override;
    HRESULT STDMETHODCALLTYPE EnumPins(IEnumPins** ppEnum) override;
    HRESULT STDMETHODCALLTYPE FindPin(LPCWSTR Id, IPin** ppPin) override;
    HRESULT STDMETHODCALLTYPE QueryFilterInfo(FILTER_INFO* pInfo) override;
    HRESULT STDMETHODCALLTYPE JoinFilterGraph(IFilterGraph* pGraph, LPCWSTR pName) override;
    HRESULT STDMETHODCALLTYPE QueryVendorInfo(LPWSTR* pVendorInfo) override;

    HRESULT STDMETHODCALLTYPE GetClassID(CLSID* pClassID) override;
    ULONG STDMETHODCALLTYPE GetMiscFlags(void) override;
};
struct VideoCaptureProxyPin : winrt::implements<VideoCaptureProxyPin, IPin, IAMStreamConfig, IKsPropertySet>
{
    VideoCaptureProxyFilter* _owningFilter = nullptr;
    wil::com_ptr_nothrow<IPin> _connectedInputPin;
    unique_media_type_ptr _mediaFormat;
    std::atomic_bool _flushing = false;

    HRESULT STDMETHODCALLTYPE Connect(IPin* pReceivePin, const AM_MEDIA_TYPE* pmt) override;
    HRESULT STDMETHODCALLTYPE ReceiveConnection(IPin* pConnector, const AM_MEDIA_TYPE* pmt) override;
    HRESULT STDMETHODCALLTYPE Disconnect(void) override;
    HRESULT STDMETHODCALLTYPE ConnectedTo(IPin** pPin) override;
    HRESULT STDMETHODCALLTYPE ConnectionMediaType(AM_MEDIA_TYPE* pmt) override;
    HRESULT STDMETHODCALLTYPE QueryPinInfo(PIN_INFO* pInfo) override;
    HRESULT STDMETHODCALLTYPE QueryDirection(PIN_DIRECTION* pPinDir) override;
    HRESULT STDMETHODCALLTYPE QueryId(LPWSTR* Id) override;
    HRESULT STDMETHODCALLTYPE QueryAccept(const AM_MEDIA_TYPE* pmt) override;
    HRESULT STDMETHODCALLTYPE EnumMediaTypes(IEnumMediaTypes** ppEnum) override;
    HRESULT STDMETHODCALLTYPE QueryInternalConnections(IPin** apPin, ULONG* nPin) override;
    HRESULT STDMETHODCALLTYPE EndOfStream(void) override;
    HRESULT STDMETHODCALLTYPE BeginFlush(void) override;
    HRESULT STDMETHODCALLTYPE EndFlush(void) override;
    HRESULT STDMETHODCALLTYPE NewSegment(REFERENCE_TIME tStart, REFERENCE_TIME tStop, double dRate) override;

    HRESULT STDMETHODCALLTYPE SetFormat(AM_MEDIA_TYPE* pmt) override;
    HRESULT STDMETHODCALLTYPE GetFormat(AM_MEDIA_TYPE** ppmt) override;
    HRESULT STDMETHODCALLTYPE GetNumberOfCapabilities(int* piCount, int* piSize) override;
    HRESULT STDMETHODCALLTYPE GetStreamCaps(int iIndex, AM_MEDIA_TYPE** ppmt, BYTE* pSCC) override;
    HRESULT STDMETHODCALLTYPE Set(REFGUID guidPropSet,
                                  DWORD dwPropID,
                                  LPVOID pInstanceData,
                                  DWORD cbInstanceData,
                                  LPVOID pPropData,
                                  DWORD cbPropData) override;
    HRESULT STDMETHODCALLTYPE Get(REFGUID guidPropSet,
                                  DWORD dwPropID,
                                  LPVOID pInstanceData,
                                  DWORD cbInstanceData,
                                  LPVOID pPropData,
                                  DWORD cbPropData,
                                  DWORD* pcbReturned) override;
    HRESULT STDMETHODCALLTYPE QuerySupported(REFGUID guidPropSet, DWORD dwPropID, DWORD* pTypeSupport) override;

    wil::com_ptr_nothrow<IMemAllocator> FindAllocator();
};
