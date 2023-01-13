#include "DirectShowUtils.h"

void FreeMediaTypeHelper(AM_MEDIA_TYPE& mt)
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

void DeleteMediaTypeHelper(AM_MEDIA_TYPE* pmt)
{
    if (!pmt)
    {
        return;
    }
    FreeMediaTypeHelper(*pmt);
    CoTaskMemFree(const_cast<AM_MEDIA_TYPE*>(pmt));
}
