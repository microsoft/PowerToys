#pragma once

#include "pch.h"

#include "template_folder.h"
#include "new_utilities.h"
#include "shell_context_sub_menu_item.h"

using namespace Microsoft::WRL;
using namespace newplus;

// // Sub context menu command enumerator
class shell_context_sub_menu final : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IEnumExplorerCommand>
{
public:
    shell_context_sub_menu(const ComPtr<IUnknown> site_of_folder);

    // IEnumExplorerCommand
    IFACEMETHODIMP Next(ULONG celt, __out_ecount_part(celt, *pceltFetched) IExplorerCommand** apUICommand, __out_opt ULONG* pceltFetched);
    IFACEMETHODIMP Skip(ULONG);
    IFACEMETHODIMP Reset();
    IFACEMETHODIMP Clone(__deref_out IEnumExplorerCommand** ppenum);

protected:
    std::vector<ComPtr<IExplorerCommand>> explorer_menu_item_commands;
    std::vector<ComPtr<IExplorerCommand>>::const_iterator current_command;
    template_folder* templates;
    ComPtr<IUnknown> site_of_folder;
};
