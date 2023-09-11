#include <initguid.h>
#define WIN32_LEAN_AND_MEAN

#include <windows.h>
#include <dshow.h>
#include <cguid.h>

// disable warning 26471 - Don't use reinterpret_cast. A cast from void* can use static_cast
// disable warning 26492 - Don't use const_cast to cast away const
// disable warning 26493 - Don't use C-style casts
// Disable 26497 for winrt - This function function-name could be marked constexpr if compile-time evaluation is desired.
// disable 26403 - Reset or explicitly delete an owner<T> pointer
#pragma warning(push)
#pragma warning(disable : 26471 26492 26493 26497 26403)
#include <wil/com.h>
#pragma warning(pop)

#include <wil/resource.h>

#include <iostream>
#include <fstream>
#include <sstream>
#include <string>
#include <Shlobj.h> 
#include <Shlobj_core.h> 

#include "DirectShowUtils.h"

std::ofstream& log()
{
    static std::ofstream report = []{
        char buffer[MAX_PATH]{};
        if (SHGetSpecialFolderPathA(HWND_DESKTOP, buffer, CSIDL_DESKTOP, false))
        {
            std::string path = buffer;
            path += "\\WebcamReport.txt";
            return std::ofstream{ path };
        }
        else
        {
            return std::ofstream{ "WebcamReport.txt" };
        }
    }();
    return report;
}

std::string GetMediaSubTypeString(const GUID& guid)
{
    if (guid == MEDIASUBTYPE_RGB24)
    {
        return "MEDIASUBTYPE_RGB24";
    }
    else if (guid == MEDIASUBTYPE_RGB32)
    {
        return "MEDIASUBTYPE_RGB32";
    }
    else if (guid == MEDIASUBTYPE_YUY2)
    {
        return "MEDIASUBTYPE_YUY2";
    }
    else if (guid == MEDIASUBTYPE_MJPG)
    {
        return "MEDIASUBTYPE_MJPG";
    }
    else if (guid == MEDIASUBTYPE_NV12)
    {
        return "MEDIASUBTYPE_NV12";
    }
    else if (guid == MEDIASUBTYPE_NV11)
    {
        return "MEDIASUBTYPE_NV11";
    }
    else if (guid == MEDIASUBTYPE_YV12)
    {
        return "MEDIASUBTYPE_YV12";
    }
    else if (guid == MEDIASUBTYPE_YUYV)
    {
        return "MEDIASUBTYPE_YUYV";
    }
    else
    {
        OLECHAR* guidString = nullptr;
        StringFromCLSID(guid, &guidString);
        if (guidString)
        {
            std::wstring_view wideView{guidString};
            std::string result;
            for (const auto c :wideView)
            {
                result += static_cast<char>(c);
            }
            ::CoTaskMemFree(guidString);
            return result;
        }
        else
        {
            return "MEDIASUBTYPE_UNKNOWN";
        }
    }
}

std::string asString(RECT r)
{
  std::ostringstream s;
  if (r.left == r.right && r.bottom == r.right && r.top == r.right && r.right == 0)
      return "[ZEROES]";
  s << '(' << r.left << ", " << r.top << ") - (" << r.right << ", " << r.bottom << ")";
  return s.str();
}

void LogMediaTypes(wil::com_ptr_nothrow<IPin>& pin)
{
    wil::com_ptr_nothrow<IEnumMediaTypes> mediaTypeEnum;
    if (pin->EnumMediaTypes(&mediaTypeEnum); !mediaTypeEnum)
    {
        return;
    }
    ULONG _ = 0;
    unique_media_type_ptr mt;
    log() << "Supported formats:\n";
    while (mediaTypeEnum->Next(1, wil::out_param(mt), &_) == S_OK)
    {
        if (mt->majortype != MEDIATYPE_Video)
        {
            continue;
        }
        auto format = reinterpret_cast<VIDEOINFOHEADER*>(mt->pbFormat);
        if (!format->AvgTimePerFrame)
        {
            continue;
        }
        const auto formatAvgFPS = 10000000LL / format->AvgTimePerFrame;
        log() << GetMediaSubTypeString(mt->subtype) << '\t' << format->bmiHeader.biWidth << "x"
              << format->bmiHeader.biHeight << " - " << formatAvgFPS << "fps src rect: " << asString(format->rcSource) 
              << " dst rect: " << asString(format->rcSource) 
              << " flipped: " << std::boolalpha << (format->bmiHeader.biHeight < 0) << '\n';
    }
    log() << '\n';
}

void ReportAllWebcams()
{
    auto enumeratorFactory = wil::CoCreateInstanceNoThrow<ICreateDevEnum>(CLSID_SystemDeviceEnum);
    if (!enumeratorFactory)
    {
        LOG("Couldn't create devenum factory");
        return;
    }

    wil::com_ptr_nothrow<IEnumMoniker> enumMoniker;
    enumeratorFactory->CreateClassEnumerator(CLSID_VideoInputDeviceCategory, &enumMoniker, CDEF_DEVMON_PNP_DEVICE);
    if (!enumMoniker)
    {
        LOG("Couldn't create class enumerator");
        return;
    }

    ULONG _ = 0;
    wil::com_ptr_nothrow<IMoniker> moniker;
    while (enumMoniker->Next(1, &moniker, &_) == S_OK)
    {
        wil::com_ptr_nothrow<IPropertyBag> propertyData;
        moniker->BindToStorage(nullptr, nullptr, IID_IPropertyBag, reinterpret_cast<void**>(&propertyData));
        if (!propertyData)
        {
            LOG("BindToStorage failed");
            continue;
        }

        wil::unique_variant propVal;
        propVal.vt = VT_BSTR;

        if (FAILED(propertyData->Read(L"FriendlyName", &propVal, nullptr)))
        {
            LOG("Couldn't obtain FriendlyName property");
            continue;
        }
        std::wstring wideFriendlyName = { propVal.bstrVal, SysStringLen(propVal.bstrVal) };
        std::string friendlyName;
        for (wchar_t c : wideFriendlyName)
        {
            friendlyName += static_cast<char>(c);
        }
        log() << "Webcam " << friendlyName << '\n';

        propVal.reset();
        propVal.vt = VT_BSTR;

        if (FAILED(propertyData->Read(L"DevicePath", &propVal, nullptr)))
        {
            LOG("Couldn't obtain DevicePath property");
            continue;
        }
        wil::com_ptr_nothrow<IBaseFilter> filter;
        moniker->BindToObject(nullptr, nullptr, IID_IBaseFilter, reinterpret_cast<void**>(&filter));
        if (!filter)
        {
            LOG("Couldn't BindToObject");
            continue;
        }

        wil::com_ptr_nothrow<IEnumPins> pinsEnum;
        if (FAILED(filter->EnumPins(&pinsEnum)))
        {
            LOG("BindToObject EnumPins");
            continue;
        }
        wil::com_ptr_nothrow<IPin> pin;

        while (pinsEnum->Next(1, &pin, &_) == S_OK)
        {
            // Skip pins which do not belong to capture category
            GUID category{};
            DWORD __;
            if (auto props = pin.try_copy<IKsPropertySet>();
                !props ||
                FAILED(props->Get(AMPROPSETID_Pin, AMPROPERTY_PIN_CATEGORY, nullptr, 0, &category, sizeof(GUID), &__)) ||
                category != PIN_CATEGORY_CAPTURE)
            {
                continue;
            }

            // Skip non-output pins
            if (PIN_DIRECTION direction = {}; FAILED(pin->QueryDirection(&direction)) || direction != PINDIR_OUTPUT)
            {
                continue;
            }
            LogMediaTypes(pin);
        }
    }
}

int main()
{
    auto comCtx = wil::CoInitializeEx();
    log() << "Report started\n";
    ReportAllWebcams();
    return 0;
}
