#include "pch.h"
#include "shell_context_sub_menu.h"
#include "trace.h"
#include "new_utilities.h"

using namespace Microsoft::WRL;

// // Sub context menu command enumerator
shell_context_sub_menu::shell_context_sub_menu(const ComPtr<IUnknown> site_of_folder)
{
    this->site_of_folder = site_of_folder;

    // Determine the New+ Template folder location
    const std::filesystem::path root = utilities::get_new_template_folder_location();

    // Create the New+ Template folder location if it doesn't exist (very rare scenario)
    utilities::create_folder_if_not_exist(root);

    // Scan the folder for any files and folders (the templates)
    templates = new template_folder(root);
    templates->rescan_template_folder();

    // Add template items to context menu
    const auto number_of_templates = templates->list_of_templates.size();
    int index = 0;
    for (int i = 0; i < number_of_templates; i++)
    {
        explorer_menu_item_commands.push_back(Make<shell_context_sub_menu_item>(templates->get_template_item(i), site_of_folder));
    }

    // Add separator to context menu
    explorer_menu_item_commands.push_back(Make<separator_context_menu_item>());

    // Add "Open templates" item to context menu
    explorer_menu_item_commands.push_back(Make<template_folder_context_menu_item>(root));

    current_command = explorer_menu_item_commands.cbegin();

    // Save how many item templates we have so it can be sent later when we do something with New+.
    // We don't send it here or it would send an event every time we open a context menu.
    newplus::utilities::set_saved_number_of_templates(static_cast<size_t>(number_of_templates));
}

// IEnumExplorerCommand
IFACEMETHODIMP shell_context_sub_menu::Next(ULONG celt, __out_ecount_part(celt, *pceltFetched) IExplorerCommand** apUICommand, __out_opt ULONG* pceltFetched)
{
    ULONG fetched{ 0 };

    if (pceltFetched)
    {
        *pceltFetched = 0ul;
    }

    for (ULONG i = 0; (i < celt) && (current_command != explorer_menu_item_commands.cend()); i++)
    {
        current_command->CopyTo(&apUICommand[0]);
        current_command++;
        fetched++;
    }

    if (pceltFetched)
    {
        *pceltFetched = fetched;
    }

    return (fetched == celt) ? S_OK : S_FALSE;
}

IFACEMETHODIMP shell_context_sub_menu::Skip(ULONG)
{
    return E_NOTIMPL;
}
IFACEMETHODIMP shell_context_sub_menu::Reset()
{
    current_command = explorer_menu_item_commands.cbegin();
    return S_OK;
}
IFACEMETHODIMP shell_context_sub_menu::Clone(__deref_out IEnumExplorerCommand** ppenum)
{
    *ppenum = nullptr;
    return E_NOTIMPL;
}
