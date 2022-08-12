#pragma once

#include "BGRATextureView.h"

RECT DetectEdges(const BGRATextureView& texture, const POINT centerPoint, const uint8_t tolerance, const bool continuousCapture);