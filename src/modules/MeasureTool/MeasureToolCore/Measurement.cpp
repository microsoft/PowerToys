#include "pch.h"

#include "Measurement.h"

#include <array>
#include <iostream>

Measurement::Measurement(RECT winRect, float px2mmRatio, float monitorDpi) :
    px2mmRatio{ px2mmRatio }, monitorDpi{ monitorDpi }
{
    rect.left = static_cast<float>(winRect.left);
    rect.right = static_cast<float>(winRect.right);
    rect.top = static_cast<float>(winRect.top);
    rect.bottom = static_cast<float>(winRect.bottom);
}

Measurement::Measurement(D2D1_RECT_F d2dRect, float px2mmRatio, float monitorDpi) :
    rect{ d2dRect }, px2mmRatio{ px2mmRatio }, monitorDpi{ monitorDpi }
{
}

winrt::hstring Measurement::abbreviations[5]{};

inline float Measurement::Width(const Unit units) const
{
    return MeasurementLogic::Convert(rect.right - rect.left + 1.f, units, px2mmRatio, monitorDpi);
}

inline float Measurement::Height(const Unit units) const
{
    return MeasurementLogic::Convert(rect.bottom - rect.top + 1.f, units, px2mmRatio, monitorDpi);
}

void Measurement::InitResources()
{
    auto rm = winrt::ResourceManager{};
    auto mm = rm.MainResourceMap();
    abbreviations[0] = mm.GetValue(L"Resources/MeasurementUnitAbbrPixel").ValueAsString();
    abbreviations[1] = mm.GetValue(L"Resources/MeasurementUnitAbbrInch").ValueAsString();
    abbreviations[2] = mm.GetValue(L"Resources/MeasurementUnitAbbrCentimetre").ValueAsString();
    abbreviations[3] = mm.GetValue(L"Resources/MeasurementUnitAbbrMillimetre").ValueAsString();
    abbreviations[4] = mm.GetValue(L"Resources/MeasurementUnitAbbrDip").ValueAsString();
}

const wchar_t* Measurement::GetUnitAbbreviation(Measurement::Unit units)
{
    switch (units)
    {
    case Unit::Pixel:
        return abbreviations[0].c_str();
    case Unit::Inch:
        return abbreviations[1].c_str();
    case Unit::Centimetre:
        return abbreviations[2].c_str();
    case Unit::Millimetre:
        return abbreviations[3].c_str();
    case Unit::Dip:
        return abbreviations[4].c_str();
    default:
        return L"??";
    }
}

Measurement::PrintResult Measurement::Print(wchar_t* buf,
                                            const size_t bufSize,
                                            const bool printWidth,
                                            const bool printHeight,
                                            const Unit units) const
{
    return MeasurementLogic::Format(buf,
                                    bufSize,
                                    printWidth,
                                    printHeight,
                                    Width(units),
                                    Height(units),
                                    GetUnitAbbreviation(units));
}

void Measurement::PrintToStream(std::wostream& stream,
                                const bool prependNewLine,
                                const bool printWidth,
                                const bool printHeight,
                                const Unit units) const
{
    if (prependNewLine)
    {
        stream << std::endl;
    }

    std::array<wchar_t, 128> buffer{};
    const auto result = Print(buffer.data(), buffer.size(), printWidth, printHeight, units);
    stream.write(buffer.data(), result.strLen);
}
