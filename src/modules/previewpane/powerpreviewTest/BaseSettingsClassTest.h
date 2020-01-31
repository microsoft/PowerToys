#include "pch.h"
#include <powerpreview/settings.h>


using namespace PowerPreviewSettings;

class BaseSettingsClassTest : public FileExplorerPreviewSettings
{
public:
    BaseSettingsClassTest();

	virtual void EnablePreview();

    virtual void DisabledPreview();
};
