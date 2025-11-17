#pragma once
#include <shellapi.h>

class HDropIterator
{
public:
    HDropIterator(IDataObject* pDataObject)
    {
        _current = 0;
        _listCount = 0;

        FORMATETC formatetc = {
            CF_HDROP,
            NULL,
            DVASPECT_CONTENT,
            -1,
            TYMED_HGLOBAL
        };

        if (SUCCEEDED(pDataObject->GetData(&formatetc, &m_medium)))
        {
            _listCount = DragQueryFile(static_cast<HDROP>(m_medium.hGlobal), 0xFFFFFFFF, NULL, 0);
        }
        else
        {
            m_medium = {};
        }
    }

    ~HDropIterator()
    {
        if (m_medium.tymed)
        {
            ReleaseStgMedium(&m_medium);
        }
    }

    void First()
    {
        _current = 0;
    }

    void Next()
    {
        _current++;
    }

    bool IsDone() const
    {
        return _current >= _listCount;
    }

    LPTSTR CurrentItem() const
    {
        UINT cch = DragQueryFile(static_cast<HDROP>(m_medium.hGlobal), _current, NULL, 0) + 1;
        LPTSTR pszPath = static_cast<LPTSTR>(malloc(sizeof(TCHAR) * cch));

        DragQueryFile(static_cast<HDROP>(m_medium.hGlobal), _current, pszPath, cch);

        return pszPath;
    }

private:
    UINT _listCount;
    STGMEDIUM m_medium;
    UINT _current;
};
