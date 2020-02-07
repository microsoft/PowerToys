#include "pch.h"
#include "BaseSettingsClassTest.h"
#include <powerpreview/settings.h>
#include <common.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

BaseSettingsClassTest::BaseSettingsClassTest() :
	FileExplorerPreviewSettings(
		false,
		GET_RESOURCE_STRING(IDS_PREVPANE_MD_BOOL_TOGGLE_CONTROLL),
		GET_RESOURCE_STRING(IDS_PREVPANE_MD_SETTINGS_DESCRIPTION),
		L"{test-guid}",
		TEXT("Test Handler\0")) {}

void BaseSettingsClassTest::EnablePreview() {}

void BaseSettingsClassTest::DisabledPreview() {}
