#pragma once
#include <shellapi.h>

class HDropIterator
{
public:
    HDropIterator(IDataObject* pDataObject)
    {
        _current = 0;

        FORMATETC formatetc = {
            CF_HDROP,
            NULL,
            DVASPECT_CONTENT,
            -1,
            TYMED_HGLOBAL
        };

        pDataObject->GetData(&formatetc, &m_medium);

        _listCount = DragQueryFile((HDROP)m_medium.hGlobal, 0xFFFFFFFF, NULL, 0);
    }

    ~HDropIterator()
    {
        ReleaseStgMedium(&m_medium);
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
        UINT cch = DragQueryFile((HDROP)m_medium.hGlobal, _current, NULL, 0) + 1;
        LPTSTR pszPath = (LPTSTR)malloc(sizeof(TCHAR) * cch);

        DragQueryFile((HDROP)m_medium.hGlobal, _current, pszPath, cch);

        return pszPath;
    }

private:
    UINT _listCount;
    STGMEDIUM m_medium;
    UINT _current;
};
