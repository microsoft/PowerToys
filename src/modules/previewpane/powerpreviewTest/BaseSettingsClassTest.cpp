#include "pch.h"
#include "BaseSettingsClassTest.h"
#include <powerpreview/settings.h>
#include <common.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

BaseSettingsClassTest::BaseSettingsClassTest() :
    FileExplorerPreviewSettings(false)
{
    this->m_name = GET_RESOURCE_STRING(IDS_PREVPANE_MD_BOOL_TOGGLE_CONTROLL);
    this->m_description = GET_RESOURCE_STRING(IDS_PREVPANE_MD_SETTINGS_DESCRIPTION);
}

void BaseSettingsClassTest::EnablePreview() {}

void BaseSettingsClassTest::DisabledPreview() {}
