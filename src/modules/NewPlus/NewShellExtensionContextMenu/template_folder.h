#pragma once

#include "pch.h"
#include <filesystem>
#include <iostream>
#include <string>
#include <list>
#include "template_item.h"

namespace newplus
{
    class template_folder
    {
    public:
        template_folder(const std::filesystem::path newplus_template_folder);
        ~template_folder();

        void rescan_template_folder();

        std::filesystem::path template_folder_path;
        std::list<std::pair<std::wstring, template_item*>> list_of_templates;

        template_item* get_template_item(const int index) const;

    protected:
        template_folder();
        void init();
    };

}