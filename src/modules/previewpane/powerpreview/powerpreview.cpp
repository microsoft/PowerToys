#include "pch.h"
#include "powerpreview.h"

// Load the settings file.
void PowerPreviewModule::init_settings()
{
    try
    {
        // Load and parse the settings file for this PowerToy.
        PowerToysSettings::PowerToyValues settings =
            PowerToysSettings::PowerToyValues::load_from_settings_file(PowerPreviewModule::get_name());

        // Load settings states.
        explrSVGSettings.LoadState(settings);
        prevPaneSVGSettings.LoadState(settings);
        prevPaneMDSettings.LoadState(settings);
    }
    catch (std::exception const& e)
    {
        Trace::InitSetErrorLoadingFile(e.what());
    }
}