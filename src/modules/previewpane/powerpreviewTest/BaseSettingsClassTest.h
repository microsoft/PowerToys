#include "pch.h"
#include <powerpreview/settings.h>
#include <atlstr.h>

using namespace PowerPreviewSettings;

class BaseSettingsClassTest : public FileExplorerPreviewSettings
{
public:
    BaseSettingsClassTest();

	virtual void EnablePreview();

    virtual void DisablePreview();
};
