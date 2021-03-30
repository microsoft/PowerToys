#include "DirectShowUtils.h"

void MyFreeMediaTypeHelper(AM_MEDIA_TYPE& mt)
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

void MyDeleteMediaTypeHelper(AM_MEDIA_TYPE* pmt)
{
    if (!pmt)
    {
        return;
    }
    MyFreeMediaTypeHelper(*pmt);
    CoTaskMemFree(const_cast<AM_MEDIA_TYPE*>(pmt));
}
