#pragma once

#include "pch.h"
#include <filesystem>
#include <iostream>
#include <string>
#include <list>
#include <memory>
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
        std::list<std::pair<std::wstring, std::shared_ptr<template_item>>> list_of_templates;

        std::shared_ptr<template_item> get_template_item(const int index) const;

    protected:
        template_folder();
        void init();
    };

}