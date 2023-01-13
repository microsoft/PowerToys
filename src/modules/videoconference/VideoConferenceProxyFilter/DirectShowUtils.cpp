#include "DirectShowUtils.h"

#include <algorithm>

unique_media_type_ptr CopyMediaType(const AM_MEDIA_TYPE* source)
{
    unique_media_type_ptr target{ static_cast<AM_MEDIA_TYPE*>(CoTaskMemAlloc(sizeof(AM_MEDIA_TYPE))) };
    *target = *source;
    if (source->cbFormat)
    {
        target->pbFormat = static_cast<BYTE*>(CoTaskMemAlloc(source->cbFormat));
        std::copy(source->pbFormat, source->pbFormat + source->cbFormat, target->pbFormat);
    }

    if (target->pUnk)
    {
        target->pUnk->AddRef();
    }

    return target;
}

wil::com_ptr_nothrow<IMemAllocator> GetPinAllocator(wil::com_ptr_nothrow<IPin>& inputPin)
{
    if (!inputPin)
    {
        return nullptr;
    }
    wil::com_ptr_nothrow<IMemAllocator> allocator;
    if (auto memInput = inputPin.try_query<IMemInputPin>(); memInput)
    {
        memInput->GetAllocator(&allocator);
        return allocator;
    }

    return nullptr;
}

void MyFreeMediaType(AM_MEDIA_TYPE& mt)
{
    if (mt.cbFormat != 0)
    {
        CoTaskMemFree(mt.pbFormat);
        mt.cbFormat = 0;
        mt.pbFormat = nullptr;
    }

    if (mt.pUnk != nullptr)
    {
        mt.pUnk->Release();
        mt.pUnk = nullptr;
    }
}

void MyDeleteMediaType(AM_MEDIA_TYPE* pmt)
{
    if (!pmt)
    {
        return;
    }

    MyFreeMediaType(*pmt);
    CoTaskMemFree(const_cast<AM_MEDIA_TYPE*>(pmt));
}

HRESULT MediaTypeEnumerator::Next(ULONG cObjects, AM_MEDIA_TYPE** outObjects, ULONG* pcFetched)
{
    if (!outObjects)
    {
        return E_POINTER;
    }

    ULONG fetched = 0;
    ULONG toFetch = cObjects;
    while (toFetch-- && _pos < _objects.size())
    {
        auto copy = CopyMediaType(_objects[_pos++].get());
        outObjects[fetched++] = copy.release();
    }

    if (pcFetched)
    {
        *pcFetched = fetched;
    }

    return fetched == cObjects ? S_OK : S_FALSE;
}

HRESULT MediaTypeEnumerator::Skip(ULONG cObjects)
{
    _pos += cObjects;
    return _pos < _objects.size() ? S_OK : S_FALSE;
}

HRESULT MediaTypeEnumerator::Reset()
{
    _pos = 0;
    return S_OK;
}

HRESULT MediaTypeEnumerator::Clone(IEnumMediaTypes** ppEnum)
{
    auto cloned = winrt::make_self<MediaTypeEnumerator>();
    cloned->_objects.resize(_objects.size());
    for (size_t i = 0; i < _objects.size(); ++i)
    {
        cloned->_objects[i] = CopyMediaType(_objects[i].get());
    }

    cloned->_pos = _pos;
    cloned.as<IEnumMediaTypes>().copy_to(ppEnum);
    return S_OK;
}