#include "pch.h"
#include "VideoCaptureDeviceList.h"
#include "Logging.h"
#include "MediaFoundationAPIProvider.h"
#include <mfapi.h>
#include <Mfidl.h>

#include <wil/resource.h>
#include <wil/com.h>

void VideoCaptureDeviceList::Clear()
{
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

HRESULT VideoCaptureDeviceList::EnumerateDevices()
{
    HRESULT hr = S_OK;
    wil::com_ptr<IMFAttributes> pAttributes;
    Clear();
    auto mfplatAPI = mfplatAPIProvider::create();
    auto mfAPI = mfAPIProvider::create();
    if (!mfplatAPI || !mfAPI)
    {
        return ERROR_FILE_NOT_FOUND;
    }

    // Initialize an attribute store. We will use this to
    // specify the enumeration parameters.

    hr = mfplatAPI->MFCreateAttributes(&pAttributes, 1);

    // Ask for source type = video capture devices
    if (SUCCEEDED(hr))
    {
        hr = pAttributes->SetGUID(
            MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE,
            MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID);
    }
    else
    {
        LOG("VideoCaptureDeviceList::EnumerateDevices(): Couldn't MFCreateAttributes");
    }
    // Enumerate devices.
    if (SUCCEEDED(hr))
    {
        hr = mfAPI->MFEnumDeviceSources(pAttributes.get(), &m_ppDevices, &m_numberDevices);
    }
    else
    {
        LOG("VideoCaptureDeviceList::EnumerateDevices(): Couldn't SetGUID");
    }

    if (FAILED(hr))
    {
        LOG("VideoCaptureDeviceList::EnumerateDevices(): MFEnumDeviceSources failed");
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

HRESULT VideoCaptureDeviceList::GetDevice(UINT32 index, IMFActivate** ppActivate)
{
    if (index >= Count())
    {
        return E_INVALIDARG;
    }

    *ppActivate = m_ppDevices[index];
    (*ppActivate)->AddRef();

    return S_OK;
}

std::wstring_view VideoCaptureDeviceList::GetDeviceName(UINT32 index)
{
    if (index >= Count())
    {
        return {};
    }

    return m_deviceFriendlyNames[index];
}
