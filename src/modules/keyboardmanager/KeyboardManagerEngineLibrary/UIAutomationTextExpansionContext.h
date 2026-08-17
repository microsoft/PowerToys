#pragma once

#include <memory>

#include "ITextExpansionTextContext.h"

std::unique_ptr<ITextExpansionTextContext> CreateUIAutomationTextExpansionContext();
