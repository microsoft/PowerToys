#pragma once
#include "../interface/powertoy_module_interface.h"
#include "Generated Files/resource.h"

// We support only one instance of the overlay
extern class OverlayWindow* instance;

class OverlayWindow : public PowertoyModuleIface
{
public:
    OverlayWindow();

    virtual const wchar_t* get_name() override;
    virtual const wchar_t* get_key() override;
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override;

    virtual void set_config(const wchar_t* config) override;
    virtual void enable() override;
    virtual void disable() override;
    virtual bool is_enabled() override;
    virtual void destroy() override;
private:
    std::wstring app_name;
    //contains the non localized key of the powertoy
    std::wstring app_key;
    bool _enabled = false;

    void disable(bool trace_event);
};
