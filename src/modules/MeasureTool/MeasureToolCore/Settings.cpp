#include "pch.h"

#include <common/SettingsAPI/settings_helpers.h>
#include <common/utils/color.h>

#include "Settings.h"

namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_VALUE[] = L"value";

    const wchar_t JSON_KEY_CONTINUOUS_CAPTURE[] = L"ContinuousCapture";
    const wchar_t JSON_KEY_DRAW_FEET_ON_CROSS[] = L"DrawFeetOnCross";
    const wchar_t JSON_KEY_PIXEL_TOLERANCE[] = L"PixelTolerance";
    const wchar_t JSON_KEY_PER_COLOR_CHANNEL_EDGE_DETECTION[] = L"PerColorChannelEdgeDetection";
    const wchar_t JSON_KEY_MEASURE_CROSS_COLOR[] = L"MeasureCrossColor";
    const wchar_t JSON_KEY_UNITS_OF_MEASURE[] = L"UnitsOfMeasure";
}

Settings Settings::LoadFromFile()
{
    Settings result;

    try
    {
        auto props = PTSettingsHelper::load_module_settings(L"Measure Tool").GetNamedObject(JSON_KEY_PROPERTIES);

        try
        {
            result.continuousCapture = props.GetNamedObject(JSON_KEY_CONTINUOUS_CAPTURE).GetNamedBoolean(JSON_KEY_VALUE);
        }
        catch (...)
        {
        }

        try
        {
            result.drawFeetOnCross = props.GetNamedObject(JSON_KEY_DRAW_FEET_ON_CROSS).GetNamedBoolean(JSON_KEY_VALUE);
        }
        catch (...)
        {
        }

        try
        {
            result.pixelTolerance = static_cast<uint8_t>(props.GetNamedObject(JSON_KEY_PIXEL_TOLERANCE).GetNamedNumber(JSON_KEY_VALUE));
        }
        catch (...)
        {
        }

        try
        {
            const auto colorString = props.GetNamedObject(JSON_KEY_MEASURE_CROSS_COLOR).GetNamedString(JSON_KEY_VALUE);
            checkValidRGB(colorString, &result.lineColor[0], &result.lineColor[1], &result.lineColor[2]);
        }
        catch (...)
        {
        }

        try
        {
            result.perColorChannelEdgeDetection = props.GetNamedObject(JSON_KEY_PER_COLOR_CHANNEL_EDGE_DETECTION).GetNamedBoolean(JSON_KEY_VALUE);
        }
        catch (...)
        {
        }

        try
        {
            result.units = static_cast<Measurement::Unit>(props.GetNamedObject(JSON_KEY_UNITS_OF_MEASURE).GetNamedNumber(JSON_KEY_VALUE));
        }
        catch (...)
        {
        }
    }
    catch (...)
    {
    }

    return result;
}
