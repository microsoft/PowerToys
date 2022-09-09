#include "pch.h"

#include "ExplorerCommand.h"

IFACEMETHODIMP ExplorerCommand::QueryInterface(REFIID riid, void** ppv)
{
    static const QITAB qit[] = {
        QITABENT(ExplorerCommand, IExplorerCommand),
        { 0, 0 },
    };
    return QISearch(this, qit, riid, ppv);
}

IFACEMETHODIMP_(ULONG) ExplorerCommand::AddRef()
{
    return ++m_ref_count;
}

IFACEMETHODIMP_(ULONG) ExplorerCommand::Release()
{
    auto result = --m_ref_count;
    if (result == 0)
    {
        delete this;
    }
    return result;
}
