#include "stdafx.h"

#include "TextureLoader.h"


#pragma optimize("", off)

HRESULT CopyAttribute(IMFAttributes * pSrc, IMFAttributes * pDest, const GUID & key);


template<class T>
void SafeRelease(T ** ppT)
{
  if(*ppT)
  {
    (*ppT)->Release();
    *ppT = NULL;
  }
}

void DeviceList::Clear()
{
  for(UINT32 i = 0; i < m_cDevices; i++)
  {
    SafeRelease(&m_ppDevices[i]);
  }
  CoTaskMemFree(m_ppDevices);
  m_ppDevices = NULL;

  m_cDevices = 0;
}

HRESULT DeviceList::EnumerateDevices()
{
  HRESULT hr = S_OK;
  IMFAttributes * pAttributes = NULL;

  Clear();

  // Initialize an attribute store. We will use this to
  // specify the enumeration parameters.

  hr = MFCreateAttributes(&pAttributes, 1);

  // Ask for source type = video capture devices
  if(SUCCEEDED(hr))
  {
    hr = pAttributes->SetGUID(
      MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE,
      MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID);
  }

  // Enumerate devices.
  if(SUCCEEDED(hr))
  {
    hr = MFEnumDeviceSources(pAttributes, &m_ppDevices, &m_cDevices);
  }

  SafeRelease(&pAttributes);

  return hr;
}

HRESULT DeviceList::GetDevice(UINT32 index, IMFActivate ** ppActivate)
{
  if(index >= Count())
  {
    return E_INVALIDARG;
  }

  *ppActivate = m_ppDevices[index];
  (*ppActivate)->AddRef();

  return S_OK;
}

HRESULT DeviceList::GetDeviceName(UINT32 index, WCHAR ** ppszName)
{
  if(index >= Count())
  {
    return E_INVALIDARG;
  }

  HRESULT hr = S_OK;

  hr = m_ppDevices[index]->GetAllocatedString(
    MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME,
    ppszName,
    NULL);

  return hr;
}

//-------------------------------------------------------------------
// CopyAttribute
//
// Copy an attribute value from one attribute store to another.
//-------------------------------------------------------------------

HRESULT CopyAttribute(IMFAttributes * pSrc, IMFAttributes * pDest, const GUID & key)
{
  PROPVARIANT var;
  PropVariantInit(&var);

  HRESULT hr = S_OK;

  hr = pSrc->GetItem(key, &var);
  if(SUCCEEDED(hr))
  {
    hr = pDest->SetItem(key, var);
  }

  PropVariantClear(&var);
  return hr;
}

HRESULT ConfigureSourceReader(IMFSourceReader * pReader)
{
  // The list of acceptable types.
  GUID subtypes[] = {
      MFVideoFormat_NV12, MFVideoFormat_YUY2, MFVideoFormat_UYVY, MFVideoFormat_RGB32, MFVideoFormat_RGB24, MFVideoFormat_IYUV
  };

  HRESULT hr = S_OK;
  BOOL bUseNativeType = FALSE;

  GUID subtype = {0};

  IMFMediaType * pType = NULL;

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

  if(FAILED(hr))
  {
    goto done;
  }

  hr = pType->GetGUID(MF_MT_SUBTYPE, &subtype);

  if(FAILED(hr))
  {
    goto done;
  }

  for(UINT32 i = 0; i < ARRAYSIZE(subtypes); i++)
  {
    if(subtype == subtypes[i])
    {
      hr = pReader->SetCurrentMediaType(
        (DWORD)MF_SOURCE_READER_FIRST_VIDEO_STREAM,
        NULL,
        pType);

      bUseNativeType = TRUE;
      break;
    }
  }

  if(!bUseNativeType)
  {
    // None of the native types worked. The camera might offer
    // output a compressed type such as MJPEG or DV.

    // Try adding a decoder.

    for(UINT32 i = 0; i < ARRAYSIZE(subtypes); i++)
    {
      hr = pType->SetGUID(MF_MT_SUBTYPE, subtypes[i]);

      if(FAILED(hr))
      {
        goto done;
      }

      hr = pReader->SetCurrentMediaType(
        (DWORD)MF_SOURCE_READER_FIRST_VIDEO_STREAM,
        NULL,
        pType);

      if(SUCCEEDED(hr))
      {
        break;
      }
    }
  }

done:
  SafeRelease(&pType);
  return hr;
}


HRESULT
SimpleMediaStream::RuntimeClassInitialize(
    _In_ SimpleMediaSource *pSource
    )
{
    HRESULT hr = S_OK;
    ComPtr<IMFMediaTypeHandler> spTypeHandler;
    ComPtr<IMFAttributes> attrs;

    if (nullptr == pSource)
    {
        return E_INVALIDARG;
    }
    RETURN_IF_FAILED (pSource->QueryInterface(IID_PPV_ARGS(&_parent)));

    // Initialize media type and set the video output media type.
    RETURN_IF_FAILED (MFCreateMediaType(&_spMediaType));
    _spMediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
    _spMediaType->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_YUY2);
    _spMediaType->SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive);
    _spMediaType->SetUINT32(MF_MT_ALL_SAMPLES_INDEPENDENT, TRUE);
    const auto w = 640;
    const auto h = 480;
    MFSetAttributeSize(_spMediaType.Get(), MF_MT_FRAME_SIZE, w, h);
    MFSetAttributeRatio(_spMediaType.Get(), MF_MT_FRAME_RATE, 30, 1);
    MFSetAttributeRatio(_spMediaType.Get(), MF_MT_PIXEL_ASPECT_RATIO, 1, 1);

    RETURN_IF_FAILED (MFCreateAttributes(&_spAttributes, 10));
    RETURN_IF_FAILED (this->_SetStreamAttributes(_spAttributes.Get()));
    RETURN_IF_FAILED (MFCreateEventQueue(&_spEventQueue));

    // Initialize stream descriptors
    RETURN_IF_FAILED (MFCreateStreamDescriptor(0, 1, _spMediaType.GetAddressOf(), &_spStreamDesc));

    RETURN_IF_FAILED (_spStreamDesc->GetMediaTypeHandler(&spTypeHandler));
    RETURN_IF_FAILED (spTypeHandler->SetCurrentMediaType(_spMediaType.Get()));
    RETURN_IF_FAILED (this->_SetStreamDescriptorAttributes(_spStreamDesc.Get()));

    _devices.Clear();
    _devices.EnumerateDevices();

    _devices.GetDevice((UINT32)0, &_activate);

    IMFMediaSource * realSource = NULL;

    hr = _activate->ActivateObject(
      __uuidof(IMFMediaSource),
      (void **)&realSource);

    // Get the symbolic link. This is needed to handle device-
    // loss notifications. (See CheckDeviceLost.)

    // TODO: handle it
    //if(SUCCEEDED(hr))
    //{
    //  hr = _activate->GetAllocatedString(
    //    MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_SYMBOLIC_LINK,
    //    &m_pwszSymbolicLink,
    //    NULL);
    //}

    if(SUCCEEDED(hr))
    {
      IMFAttributes * pAttributes = NULL;

      hr = MFCreateAttributes(&pAttributes, 2);

      if(SUCCEEDED(hr))
      {
        //hr = pAttributes->SetUnknown(MF_SOURCE_READER_ASYNC_CALLBACK, this);
      }

      IMFMediaSource * pSource = NULL;


      if(SUCCEEDED(hr))
      {
        hr = MFCreateSourceReaderFromMediaSource(
          realSource,
          pAttributes,
          &m_pReader);
        ConfigureSourceReader(m_pReader);
      }

      SafeRelease(&pAttributes);
      SafeRelease(&realSource);
    }

    return hr;
}

// IMFMediaEventGenerator
IFACEMETHODIMP
SimpleMediaStream::BeginGetEvent(
    _In_ IMFAsyncCallback *pCallback,
    _In_ IUnknown *punkState
    )
{
    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();

    RETURN_IF_FAILED (_CheckShutdownRequiresLock());
    RETURN_IF_FAILED (_spEventQueue->BeginGetEvent(pCallback, punkState));

    return hr;
}

IFACEMETHODIMP
SimpleMediaStream::EndGetEvent(
    _In_ IMFAsyncResult *pResult,
    _COM_Outptr_ IMFMediaEvent **ppEvent
    )
{
    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();

    RETURN_IF_FAILED (_CheckShutdownRequiresLock());
    RETURN_IF_FAILED (_spEventQueue->EndGetEvent(pResult, ppEvent));

    return hr;
}

IFACEMETHODIMP
SimpleMediaStream::GetEvent(
    DWORD dwFlags,
    _COM_Outptr_ IMFMediaEvent **ppEvent
    )
{
    // NOTE:
    // GetEvent can block indefinitely, so we don't hold the lock.
    // This requires some juggling with the event queue pointer.

    HRESULT hr = S_OK;

    ComPtr<IMFMediaEventQueue> spQueue;

    {
        auto lock = _critSec.Lock();

        RETURN_IF_FAILED (_CheckShutdownRequiresLock());
        spQueue = _spEventQueue;
    }

    // Now get the event.
    RETURN_IF_FAILED (_spEventQueue->GetEvent(dwFlags, ppEvent));

    return hr;
}

IFACEMETHODIMP
SimpleMediaStream::QueueEvent(
    MediaEventType eventType,
    REFGUID guidExtendedType,
    HRESULT hrStatus,
    _In_opt_ PROPVARIANT const *pvValue
    )
{
    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();

    RETURN_IF_FAILED (_CheckShutdownRequiresLock());
    RETURN_IF_FAILED (_spEventQueue->QueueEventParamVar(eventType, guidExtendedType, hrStatus, pvValue));

    return hr;
}

// IMFMediaStream
IFACEMETHODIMP
SimpleMediaStream::GetMediaSource(
    _COM_Outptr_ IMFMediaSource **ppMediaSource
    )
{
    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();

    if (ppMediaSource == nullptr)
    {
        return E_POINTER;
    }
    *ppMediaSource = nullptr;

    RETURN_IF_FAILED (_CheckShutdownRequiresLock());

    *ppMediaSource = _parent.Get();
    (*ppMediaSource)->AddRef();

    return hr;
}

IFACEMETHODIMP
SimpleMediaStream::GetStreamDescriptor(
    _COM_Outptr_ IMFStreamDescriptor **ppStreamDescriptor
    )
{
    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();

    if (ppStreamDescriptor == nullptr)
    {
        return E_POINTER;
    }
    *ppStreamDescriptor = nullptr;

    RETURN_IF_FAILED (_CheckShutdownRequiresLock());

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

/*
   Writes to a buffer representing a 2D image.
   Writes a different constant to each line based on row number and current time.

   Assumes top down image, no negative stride and pBuf points to the begnning of the buffer of length len.

   Param:
   pBuf - pointer to beginning of buffer
   pitch - line length in bytes
   len - length of buffer in bytes
*/
HRESULT
WriteSampleData(
    _Inout_updates_bytes_(len) BYTE *pBuf,
    _In_ LONG pitch,
    _In_ DWORD len
    )
{
    if (pBuf == nullptr)
    {
        return E_INVALIDARG;
    }

    const int NUM_ROWS = len / abs(pitch);

    static const auto image = LoadImageFromFile(LR"(P:\wecam_test.jpg)");



    const bool shouldShowNoise = true;
    if(!shouldShowNoise && image)
    {
        memcpy(pBuf, image->buffer.get(), len);
    }
    else
    {
      LONGLONG curSysTimeInS = MFGetSystemTime() / (MFTIME)1000000;
      int offset = (curSysTimeInS % NUM_ROWS) * 10;

      for (int r = 0; r < NUM_ROWS; r++)
      {
          int grayColor = r + offset;
          for(int i = 0; i < pitch; ++i)
          {
            pBuf[r * pitch + i] = rand() % 255;
          }
      }
    }

    return S_OK;
}

IFACEMETHODIMP
SimpleMediaStream::RequestSample(
    _In_ IUnknown *pToken
    )
{
    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();
    ComPtr<IMFSample> sample;
    ComPtr<IMFMediaBuffer> outputBuffer;
    LONG pitch = IMAGE_ROW_SIZE_BYTES;
    BYTE *bufferStart = nullptr; // not used
    DWORD bufferLength = 0;
    BYTE *pbuf = nullptr;
    ComPtr<IMF2DBuffer2> buffer2D;

    RETURN_IF_FAILED (_CheckShutdownRequiresLock());

    const auto nDevices = _devices.Count();



    // Create the sink writer
    //if (SUCCEEDED(hr))
    //{
    //    hr = MFCreateSinkWriterFromURL(
    //        pwszFileName,
    //        NULL,
    //        NULL,
    //        &m_pWriter);
    //}

    // Set up the encoding parameters.
    if(SUCCEEDED(hr))
    {
      //hr = ConfigureCapture(param);
    }


    // Request the first video frame.

    ComPtr<IMFSample> sourceSample;
    DWORD streamFlags = 0;
    hr = m_pReader->ReadSample(
      (DWORD)MF_SOURCE_READER_FIRST_VIDEO_STREAM,
      0,
      NULL,
      &streamFlags,
      NULL,
      &sourceSample);

    RETURN_IF_FAILED (MFCreateSample(&sample));
    RETURN_IF_FAILED (MFCreate2DMediaBuffer(NUM_IMAGE_COLS,
                                            NUM_IMAGE_ROWS,
      D3DFMT_A8B8G8R8,
                                            false,
                                            &outputBuffer));
    RETURN_IF_FAILED (outputBuffer.As(&buffer2D));
    RETURN_IF_FAILED (buffer2D->Lock2DSize(MF2DBuffer_LockFlags_Write,
                                           &pbuf,
                                           &pitch,
                                           &bufferStart,
                                           &bufferLength));
    RETURN_IF_FAILED (WriteSampleData(pbuf, pitch, bufferLength));
    RETURN_IF_FAILED (buffer2D->Unlock2D());
    RETURN_IF_FAILED (sample->AddBuffer(outputBuffer.Get()));
    RETURN_IF_FAILED (sample->SetSampleTime(MFGetSystemTime()));
    RETURN_IF_FAILED (sample->SetSampleDuration(333333));
    if (pToken != nullptr)
    {
        RETURN_IF_FAILED (sample->SetUnknown(MFSampleExtension_Token, pToken));
    }

    const wchar_t shmemEndpoint[] = L"Global\\PowerToysWebcamMuteSwitch";

    const auto noiseToggle = [=]() -> const uint8_t * {

      SECURITY_DESCRIPTOR sd;

      InitializeSecurityDescriptor(&sd, SECURITY_DESCRIPTOR_REVISION);

      SetSecurityDescriptorDacl(&sd, true, 0, false);

      SECURITY_ATTRIBUTES sa;
      sa.nLength = sizeof(sa);
      sa.lpSecurityDescriptor = &sd;
      sa.bInheritHandle = false;

      auto hMapFile{CreateFileMappingW(
        INVALID_HANDLE_VALUE,    // use paging file
        &sa,                    // default security
        PAGE_READWRITE,          // read/write access
        0,                       // maximum object size (high-order DWORD)
        1,                // maximum object size (low-order DWORD)
        shmemEndpoint)};                 // name of mapping object
      if(!hMapFile)
      {
        return nullptr;
      }
      auto shmem = (const uint8_t *)MapViewOfFile(hMapFile,   // handle to map object
        FILE_MAP_READ, // read/write permission
        0,
        0,
        1);
      return shmem;
    }();

    const bool disableWebcam = noiseToggle && *noiseToggle;
    if(sourceSample && !disableWebcam)
    {
      RETURN_IF_FAILED(_spEventQueue->QueueEventParamUnk(MEMediaSample,
        GUID_NULL,
        S_OK,
        sourceSample.Get()));
    }
    else
    {
      RETURN_IF_FAILED(_spEventQueue->QueueEventParamUnk(MEMediaSample,
        GUID_NULL,
        S_OK,
        sample.Get()));
    }
    

    return hr;
}

//////////////////////////////////////////////////////////////////////////////////////////
// IMFMediaStream2
IFACEMETHODIMP
SimpleMediaStream::SetStreamState(
    MF_STREAM_STATE state
    )
{
    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();
    bool runningState = false;

    RETURN_IF_FAILED (_CheckShutdownRequiresLock());

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
    _Out_ MF_STREAM_STATE *pState
    )
{
    HRESULT hr = S_OK;
    auto lock = _critSec.Lock();
    BOOLEAN pauseState = false;

    RETURN_IF_FAILED (_CheckShutdownRequiresLock());

    if (SUCCEEDED(hr))
    {
        *pState = (_isSelected ? MF_STREAM_STATE_RUNNING : MF_STREAM_STATE_STOPPED);
    }

    return hr;
}

HRESULT
SimpleMediaStream::Shutdown(
    )
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
SimpleMediaStream::_CheckShutdownRequiresLock(
    )
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
    _In_ IMFAttributes *pAttributeStore
    )
{
    HRESULT hr = S_OK;

    if (nullptr == pAttributeStore)
    {
        return E_INVALIDARG;
    }

    RETURN_IF_FAILED (pAttributeStore->SetGUID(MF_DEVICESTREAM_STREAM_CATEGORY, PINNAME_VIDEO_CAPTURE));
    RETURN_IF_FAILED (pAttributeStore->SetUINT32(MF_DEVICESTREAM_STREAM_ID, STREAMINDEX));
    RETURN_IF_FAILED (pAttributeStore->SetUINT32(MF_DEVICESTREAM_FRAMESERVER_SHARED, 1));
    RETURN_IF_FAILED (pAttributeStore->SetUINT32(MF_DEVICESTREAM_ATTRIBUTE_FRAMESOURCE_TYPES, _MFFrameSourceTypes::MFFrameSourceTypes_Color));

    return hr;
}

HRESULT
SimpleMediaStream::_SetStreamDescriptorAttributes(
    _In_ IMFAttributes *pAttributeStore
    )
{
    HRESULT hr = S_OK;

    if (nullptr == pAttributeStore)
    {
        return E_INVALIDARG;
    }

    RETURN_IF_FAILED (pAttributeStore->SetGUID(MF_DEVICESTREAM_STREAM_CATEGORY, PINNAME_VIDEO_CAPTURE));
    RETURN_IF_FAILED (pAttributeStore->SetUINT32(MF_DEVICESTREAM_STREAM_ID, STREAMINDEX));
    RETURN_IF_FAILED (pAttributeStore->SetUINT32(MF_DEVICESTREAM_FRAMESERVER_SHARED, 1));
    RETURN_IF_FAILED (pAttributeStore->SetUINT32(MF_DEVICESTREAM_ATTRIBUTE_FRAMESOURCE_TYPES, _MFFrameSourceTypes::MFFrameSourceTypes_Color));

    return hr;
}

