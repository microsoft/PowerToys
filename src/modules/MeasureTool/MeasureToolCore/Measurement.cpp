#include "pch.h"

#include "Measurement.h"

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
                                            const int units) const
{
    PrintResult result;

    auto print = [=, &result](Measurement::Unit unit, const bool paren) {
        if (paren)
        {
            result.strLen += swprintf_s(buf + result.strLen, bufSize - result.strLen, printWidth && printHeight ? L"\n(" : L" (");
        }
        if (printWidth)
        {
            result.strLen += swprintf_s(buf + result.strLen,
                                        bufSize - result.strLen,
                                        L"%.4g",
                                        Width(unit));
            if (printHeight)
            {
                result.crossSymbolPos[paren] = result.strLen + 1;
                result.strLen += swprintf_s(buf + result.strLen,
                                            bufSize - result.strLen,
                                            L" \x00D7 ");
            }
        }
        if (printHeight)
        {
            result.strLen += swprintf_s(buf + result.strLen,
                                        bufSize - result.strLen,
                                        L"%.4g",
                                        Height(unit));
        }
        switch (unit)
        {
        case Measurement::Unit::Pixel:
            result.strLen += swprintf_s(buf + result.strLen,
                                        bufSize - result.strLen,
                                        L" %s",
                                        Measurement::GetUnitAbbreviation(unit));
            break;
        case Measurement::Unit::Inch:
            result.strLen += swprintf_s(buf + result.strLen,
                                        bufSize - result.strLen,
                                        L" %s",
                                        Measurement::GetUnitAbbreviation(unit));
            break;
        case Measurement::Unit::Centimetre:
            result.strLen += swprintf_s(buf + result.strLen,
                                        bufSize - result.strLen,
                                        L" %s",
                                        Measurement::GetUnitAbbreviation(unit));

            break;
        case Measurement::Unit::Millimetre:
            result.strLen += swprintf_s(buf + result.strLen,
                                        bufSize - result.strLen,
                                        L" %s",
                                        Measurement::GetUnitAbbreviation(unit));

            break;
        case Measurement::Unit::Dip:
            result.strLen += swprintf_s(buf + result.strLen,
                                        bufSize - result.strLen,
                                        L" %s",
                                        Measurement::GetUnitAbbreviation(unit));
            break;
        }
        if (paren)
        {
            result.strLen += swprintf_s(buf + result.strLen, bufSize - result.strLen, L")");
        }
    };

    int count = 0;
    const Measurement::Unit allUnits[] = {
        Measurement::Unit::Pixel,
        Measurement::Unit::Dip,
        Measurement::Unit::Millimetre,
        Measurement::Unit::Inch,
        Measurement::Unit::Centimetre,
    };
    // We only use two units at most, it would be to long otherwise.
    for each (Measurement::Unit unit in allUnits)
    {
        if ((unit & units) == unit)
        {
            count += 1;
            if (count > 2)
                break;
            print(unit, count != 1);
        }
    }

    return result;
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

    if (printWidth)
    {
        stream << Width(units);
        if (printHeight)
        {
            stream << L" \x00D7 ";
        }
    }

    if (printHeight)
    {
        stream << Height(units);
    }

    // If the unit is pixels, then the abbreviation will not be saved as it used to be.
    if (units != Measurement::Unit::Pixel)
    {
        stream << L" " << Measurement::GetUnitAbbreviation(units);
    }
}
