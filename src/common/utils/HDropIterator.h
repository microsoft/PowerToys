#pragma once

#include <objidl.h>
#include <shellapi.h>

class HDropIterator
{
public:
    explicit HDropIterator(IDataObject* dataObject);
    ~HDropIterator();

    void First();
    void Next();
    [[nodiscard]] bool IsDone() const;
    [[nodiscard]] LPTSTR CurrentItem() const;

private:
    UINT _listCount = 0;
    STGMEDIUM m_medium{};
    UINT _current = 0;
};
