#include "pch.h"
#include "HDropIterator.h"

#include <cstdlib>

HDropIterator::HDropIterator(IDataObject* dataObject)
{
    FORMATETC formatetc{
        CF_HDROP,
        nullptr,
        DVASPECT_CONTENT,
        -1,
        TYMED_HGLOBAL
    };

    if (dataObject && SUCCEEDED(dataObject->GetData(&formatetc, &m_medium)))
    {
        _listCount = DragQueryFile(static_cast<HDROP>(m_medium.hGlobal), 0xFFFFFFFF, nullptr, 0);
    }
    else
    {
        m_medium = {};
    }
}

HDropIterator::~HDropIterator()
{
    if (m_medium.tymed)
    {
        ReleaseStgMedium(&m_medium);
    }
}

void HDropIterator::First()
{
    _current = 0;
}

void HDropIterator::Next()
{
    ++_current;
}

bool HDropIterator::IsDone() const
{
    return _current >= _listCount;
}

LPTSTR HDropIterator::CurrentItem() const
{
    const UINT cch = DragQueryFile(static_cast<HDROP>(m_medium.hGlobal), _current, nullptr, 0) + 1;
    LPTSTR path = static_cast<LPTSTR>(malloc(sizeof(TCHAR) * cch));
    if (!path)
    {
        return nullptr;
    }

    DragQueryFile(static_cast<HDROP>(m_medium.hGlobal), _current, path, cch);
    return path;
}
