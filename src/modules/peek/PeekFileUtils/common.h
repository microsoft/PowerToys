#pragma once

#include <string>

// typedef String based on compilation configuration
#ifndef UNICODE
typedef std::string String;
#else
typedef std::wstring String;
#endif
