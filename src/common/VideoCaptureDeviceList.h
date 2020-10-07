#pragma once

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <Windows.h>
#include <mfobjects.h>
#include <string_view>

class VideoCaptureDeviceList
{
    UINT32 m_numberDevices;
    // TODO: use wil
    IMFActivate** m_ppDevices = nullptr;
    wchar_t** m_deviceFriendlyNames = nullptr;

public:
    VideoCaptureDeviceList() :
        m_ppDevices(NULL), m_numberDevices(0)
    {
    }
    ~VideoCaptureDeviceList()
    {
        Clear();
    }

    UINT32 Count() const { return m_numberDevices; }

    void Clear();
    HRESULT EnumerateDevices();
    HRESULT GetDevice(UINT32 index, IMFActivate** ppActivate);
    std::wstring_view GetDeviceName(UINT32 index);
};
