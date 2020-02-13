#include "pch.h"
#include <settings_objects.h>
#include <common.h>
#include "PowerLauncher.h"

// Destroy the powertoy and free memory.
void PowerLauncherModule::destroy()
{
    Trace::Destroy();
    delete this;
}

// Return the display name of the powertoy, this will be cached.
const wchar_t* PowerLauncherModule::get_name()
{
    return m_moduleName.c_str();
}

const wchar_t** PowerLauncherModule::get_events()
{
    return nullptr;
}

// Return JSON with the configuration options.
bool PowerLauncherModule::get_config(_Out_ wchar_t* buffer, _Out_ int* buffer_size)
{
    HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

    // Create a Settings object.
    PowerToysSettings::Settings settings(hinstance, get_name());

    // General Settings.
    settings.set_description(GET_RESOURCE_STRING(IDS_GENERAL_DESCRIPTION));
    return settings.serialize_to_buffer(buffer, buffer_size);
}

// Called by the runner to pass the updated settings values as a serialized JSON.
void PowerLauncherModule::set_config(const wchar_t* config)
{
    try
    {
        PowerToysSettings::PowerToyValues values = PowerToysSettings::PowerToyValues::from_json_string(config);
        values.save_to_settings_file();
    }
    catch (std::exception const& e)
    {
        Trace::SetConfigInvalidJSON(e.what());
    }
}

// Enable the powertoy
void PowerLauncherModule::enable()
{
    Trace::PowerToyIsEnabled();
    this->m_enabled = true;
}

// Disable the powertoy
void PowerLauncherModule::disable()
{
    Trace::PowerToyIsDisabled();
    this->m_enabled = false;
}

// Returns if the powertoys is enabled
bool PowerLauncherModule::is_enabled()
{
    return this->m_enabled;
}

// Handle incoming event, data is event-specific
intptr_t PowerLauncherModule::signal_event(const wchar_t* name, intptr_t data)
{
    return 0;
}

// Load the settings file.
void PowerLauncherModule::init_settings()
{
    try
    {
        // Load and parse the settings file for this PowerToy.
        PowerToysSettings::PowerToyValues settings =
            PowerToysSettings::PowerToyValues::load_from_settings_file(PowerLauncherModule::get_name());
    }
    catch (std::exception const& e)
    {
        Trace::InitSetErrorLoadingFile(e.what());
    }
}