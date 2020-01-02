#include "StdAfx.h"
#include "HDropIterator.h"

HDropIterator::HDropIterator(IDataObject *pdtobj)
{
	_current = 0;

	FORMATETC formatetc =
	{
		CF_HDROP,
		NULL,
		DVASPECT_CONTENT,
		-1,
		TYMED_HGLOBAL
	};

	pdtobj->GetData(&formatetc, &m_medium);

	_listCount = DragQueryFile((HDROP)m_medium.hGlobal, 0xFFFFFFFF, NULL, 0);
}

HDropIterator::~HDropIterator()
{
	ReleaseStgMedium(&m_medium);
}

void HDropIterator::First()
{
	_current = 0;
}

void HDropIterator::Next()
{
	_current++;
}

bool HDropIterator::IsDone() const
{
	return _current >= _listCount;
}

LPTSTR HDropIterator::CurrentItem() const
{
	UINT cch = DragQueryFile((HDROP)m_medium.hGlobal, _current, NULL, 0) + 1;
	LPTSTR pszPath = (LPTSTR)malloc(sizeof(TCHAR) * cch);

	DragQueryFile((HDROP)m_medium.hGlobal, _current, pszPath, cch);

	return pszPath;
}
