#pragma once

#include "BGRATextureView.h"

RECT DetectEdges(const BGRATextureView& texture,
                 const POINT centerPoint,
                 const bool perChannel,
                 const uint8_t tolerance);