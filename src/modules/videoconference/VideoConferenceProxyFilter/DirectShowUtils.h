#pragma once
#include <initguid.h>

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <dshow.h>

#include <wil/com.h>
#include <winrt/Windows.Foundation.h>

#include <vector>

#include "Logging.h"

void MyDeleteMediaType(AM_MEDIA_TYPE* pmt);

using unique_media_type_ptr =
    wistd::unique_ptr<AM_MEDIA_TYPE, wil::function_deleter<decltype(&MyDeleteMediaType), MyDeleteMediaType>>;

unique_media_type_ptr CopyMediaType(const AM_MEDIA_TYPE* source);

template<typename ObjectInterface, typename EnumeratorInterface>
struct ObjectEnumerator : public winrt::implements<ObjectEnumerator<ObjectInterface, EnumeratorInterface>, EnumeratorInterface>
{
    std::vector<wil::com_ptr_nothrow<ObjectInterface>> _objects;
    ULONG _pos = 0;

    HRESULT STDMETHODCALLTYPE Next(ULONG cObjects, ObjectInterface** outObjects, ULONG* pcFetched) override
    {
        if (!outObjects)
        {
            return E_POINTER;
        }

        ULONG fetched = 0;
        ULONG toFetch = cObjects;
        while (toFetch-- && _pos < _objects.size())
        {
            _objects[_pos++].copy_to(&outObjects[fetched++]);
        }

        if (pcFetched)
        {
            *pcFetched = fetched;
        }

        return fetched == cObjects ? S_OK : S_FALSE;
    }

    HRESULT STDMETHODCALLTYPE Skip(ULONG cObjects) override
    {
        _pos += cObjects;
        return _pos < _objects.size() ? S_OK : S_FALSE;
    }

    HRESULT STDMETHODCALLTYPE Reset() override
    {
        _pos = 0;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE Clone(EnumeratorInterface** ppEnum) override
    {
        auto cloned = winrt::make_self<ObjectEnumerator>();
        cloned->_objects = _objects;
        cloned->_pos = _pos;
        cloned.as<EnumeratorInterface>().copy_to(ppEnum);
        return S_OK;
    }

    virtual ~ObjectEnumerator() = default;
};

struct MediaTypeEnumerator : public winrt::implements<MediaTypeEnumerator, IEnumMediaTypes>
{
    std::vector<unique_media_type_ptr> _objects;
    ULONG _pos = 0;

    HRESULT STDMETHODCALLTYPE Next(ULONG cObjects, AM_MEDIA_TYPE** outObjects, ULONG* pcFetched) override;
    HRESULT STDMETHODCALLTYPE Skip(ULONG cObjects) override;
    HRESULT STDMETHODCALLTYPE Reset() override;
    HRESULT STDMETHODCALLTYPE Clone(IEnumMediaTypes** ppEnum) override;

    virtual ~MediaTypeEnumerator() = default;
};

wil::com_ptr_nothrow<IMemAllocator> GetPinAllocator(wil::com_ptr_nothrow<IPin>& inputPin);
