#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <interface/lowlevel_keyboard_event_data.h>
#include <interface/win_hook_event_data.h>
#include "trace.h"
#include <common/settings_objects.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        Trace::RegisterProvider();
        break;
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
        break;
    case DLL_PROCESS_DETACH:
        Trace::UnregisterProvider();
        break;
    }
    return TRUE;
}

// PowerToy Winodws Explore File Preview Settings.
struct SampleSettings
{
    bool svgPreview_in_explr_IsEnabled = true;
    bool svgPreview_in_prevpane_IsEnabled = true;
    bool mdPreview_in_prevpane_IsEnabled = true;

    const std::wstring EXPLR_RENDRSVG_BOOL_TOGGLE = L"explr_rendrsvg_bool_toggle";
    const std::wstring PREVPANE_RENDRSVG_BOOL_TOGGLE = L"prevpane_rendrsvg_bool_toggle";
    const std::wstring PREVPANE_RENDMD_BOOL_TOGGLE = L"prevpane_rendrmd_bool_toggle";

} PowerPreviewSettings;

// Implement the PowerToy Module Interface and all the required methods.
class FileExplorer : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;

    // Load and save Settings.
    void init_settings();

public:
    FileExplorer()
    {
        init_settings();
    };

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        delete this;
        Trace::FilePreviewerIsDisabled();
    }

    // Return the display name of the powertoy, this will be cached
    virtual const wchar_t* get_name() override
    {
        return L"PowerPreview";
    }

    virtual const wchar_t** get_events() override
    {
        return nullptr;
    }

    // Return JSON with the configuration options.
    virtual bool get_config(_Out_ wchar_t* buffer, _Out_ int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(L"These settings allows you to manage your Windows File Explorer Addons.");

        // Add a toggle to manage the renders for the svg icons in the explorer.
        settings.add_bool_toogle(
            PowerPreviewSettings.EXPLR_RENDRSVG_BOOL_TOGGLE, // property name.
            L"Explorer Icons: Render SVG images", 
            PowerPreviewSettings.svgPreview_in_explr_IsEnabled);

        // Add a toggle to manage the render for the SVG preview pane.
        settings.add_bool_toogle(
            PowerPreviewSettings.PREVPANE_RENDRSVG_BOOL_TOGGLE, 
            L"Preview Pane: Show SVG", 
            PowerPreviewSettings.svgPreview_in_prevpane_IsEnabled);

        // Add a toggle to manage the render for the mark down preview pane.
        settings.add_bool_toogle(
            PowerPreviewSettings.PREVPANE_RENDMD_BOOL_TOGGLE, 
            L"Preview Pane: Show Markdown", 
            PowerPreviewSettings.mdPreview_in_prevpane_IsEnabled);

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Called by the runner to pass the updated settings values as a serialized JSON.
    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config);

            // Update settings for SVG icon render in Windows File Explorer. 
            auto explr_rendrsvg_bool_toggle = values.get_bool_value(PowerPreviewSettings.EXPLR_RENDRSVG_BOOL_TOGGLE);
            PowerPreviewSettings.svgPreview_in_explr_IsEnabled = explr_rendrsvg_bool_toggle.value();
            if (PowerPreviewSettings.svgPreview_in_explr_IsEnabled)
            {
                Trace::ExplorerSVGRenderEnabled();
            }
            else
            {
                Trace::ExplorerSVGRenderDisabled();
            }

            // Update settings for SVG file render in Preview Pane.
            auto prevpane_rendrsvg_bool_toggle = values.get_bool_value(PowerPreviewSettings.PREVPANE_RENDRSVG_BOOL_TOGGLE);
            PowerPreviewSettings.svgPreview_in_prevpane_IsEnabled = prevpane_rendrsvg_bool_toggle.value();
            if (PowerPreviewSettings.svgPreview_in_prevpane_IsEnabled)
            {
                Trace::PreviewPaneMarkDownRenderEnabled();
            }
            else
            {
                Trace::PreviewPaneMarkDownRenderDisabled();
            }

            // Update settings for Mark Down render in preview pane.
            auto prevpane_rendrmd_bool_toggle = values.get_bool_value(PowerPreviewSettings.PREVPANE_RENDMD_BOOL_TOGGLE);
            PowerPreviewSettings.mdPreview_in_prevpane_IsEnabled = prevpane_rendrmd_bool_toggle.value();
            if (PowerPreviewSettings.mdPreview_in_prevpane_IsEnabled)
            {
                Trace::PreviewPaneMarkDownRenderEnabled();
            }
            else
            {
                Trace::PreviewPaneMarkDownRenderDisabled();
            }

            values.save_to_settings_file();
        }
        catch (std::exception const& e)
        {
            Trace::SetConfigInvalidJSON(e.what());
        }
    }

    // Enable the powertoy
    virtual void enable()
    {
        m_enabled = true;
        Trace::FilePreviewerIsEnabled();
    }

    // Disable the powertoy
    virtual void disable()
    {
        m_enabled = false;
        Trace::FilePreviewerIsDisabled();
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // Handle incoming event, data is event-specific
    virtual intptr_t signal_event(const wchar_t* name, intptr_t data) override
    {
        if (wcscmp(name, ll_keyboard) == 0)
        {
            auto& event = *(reinterpret_cast<LowlevelKeyboardEvent*>(data));
            // Return 1 if the keypress is to be suppressed (not forwarded to Windows),
            // otherwise return 0.
            return 0;
        }
        else if (wcscmp(name, win_hook_event) == 0)
        {
            auto& event = *(reinterpret_cast<WinHookEvent*>(data));
            // Return value is ignored
            return 0;
        }
        return 0;
    }

    virtual void register_system_menu_helper(PowertoySystemMenuIface* helper) override {}
    virtual void signal_system_menu_action(const wchar_t* name) override {}
};

// Load the settings file.
void FileExplorer::init_settings()
{
    try
    {
        // Load and parse the settings file for this PowerToy.
        PowerToysSettings::PowerToyValues settings =
            PowerToysSettings::PowerToyValues::load_from_settings_file(FileExplorer::get_name());

        // Load the explorer svg icon render state.
        auto explr_rendrsvg_bool_toggle = settings.get_bool_value(PowerPreviewSettings.EXPLR_RENDRSVG_BOOL_TOGGLE);
        if (explr_rendrsvg_bool_toggle)
        {
            PowerPreviewSettings.svgPreview_in_explr_IsEnabled = explr_rendrsvg_bool_toggle.value();
        }

        // Load the preview pane svg render state.
        auto prevpane_rendrsvg_bool_toggle = settings.get_bool_value(PowerPreviewSettings.PREVPANE_RENDRSVG_BOOL_TOGGLE);
        if (prevpane_rendrsvg_bool_toggle)
        {
            PowerPreviewSettings.svgPreview_in_prevpane_IsEnabled = prevpane_rendrsvg_bool_toggle.value();
        }

        // Load preview pane mark down render state.
        auto prevpane_rendrmd_bool_toggle = settings.get_bool_value(PowerPreviewSettings.PREVPANE_RENDMD_BOOL_TOGGLE);
        if (prevpane_rendrmd_bool_toggle)
        {
            PowerPreviewSettings.mdPreview_in_prevpane_IsEnabled = prevpane_rendrmd_bool_toggle.value();
        }
    }
    catch (std::exception const& e)
    {
        Trace::InitSetErrorLoadingFile(e.what());
    }
}

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new FileExplorer();
}
