#pragma once

class HDropIterator
{
public:
	HDropIterator(IDataObject *pDataObject);
	~HDropIterator();
	void First();
	void Next();
	bool IsDone() const;
	LPTSTR CurrentItem() const;

private:
	UINT _listCount;
	STGMEDIUM m_medium;
	UINT _current;
};
