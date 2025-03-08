#include "pch.h"
#include <shellapi.h>
#include "template_folder.h"

using namespace newplus;

template_folder::template_folder(){};

template_folder::template_folder(const std::filesystem::path newplus_template_folder)
{
    this->template_folder_path = newplus_template_folder;
}

template_folder::~template_folder()
{
    list_of_templates.clear();
}

void template_folder::init()
{
    rescan_template_folder();
}

void template_folder::rescan_template_folder()
{
    list_of_templates.clear();

    std::list<std::pair<std::wstring, template_item*>> dirs;
    std::list<std::pair<std::wstring, template_item*>> files;
    for (const auto& entry : std::filesystem::directory_iterator(template_folder_path))
    {
        if (entry.is_directory())
        {
            dirs.push_back({ entry.path().wstring(), new template_item(entry) });
        }
        else
        {
            if (!helpers::filesystem::is_hidden(entry.path()))
            {
                files.push_back({ entry.path().wstring(), new template_item(entry) });
            }
        }
    }

    // List of templates are sorted, with template-directories/folders first then followed by template-files
    dirs.sort();
    files.sort();
    list_of_templates = dirs;
    list_of_templates.splice(list_of_templates.end(), files);
}

template_item* template_folder::get_template_item(const int index) const
{
    auto it = list_of_templates.begin();
    std::advance(it, index);
    return it->second;
}
