#pragma once

#include "Measurement.h"

#include <string_view>
#include <vector>

void SetClipBoardToText(const std::wstring_view text);

void SetClipboardToMeasurements(const std::vector<Measurement>& measurements,
                                bool printWidth,
                                bool printHeight,
                                Measurement::Unit units);