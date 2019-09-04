# C++ Rest SDK - JSON library

This JSON library is taken from the C++ REST SDK in https://github.com/microsoft/cpprestsdk

Based in the [v2.10.13 release](https://github.com/microsoft/cpprestsdk/tree/v2.10.13/Release), it consists of the needed files to build and use the JSON classes described in `include/cpprest/json.h`.

Changes made to the files in order to build in the PowerToys project:
- Removal of `#include` references to files that are not needed.
- `#include "pch.h"` instead of `#include "stdafx.h"` to use the PowerToys pre-compiled header.
- `#define _NO_ASYNCRTIMP` in [`include/cpprest/details/cpprest_compat.h`](./include/cpprest/details/cpprest_compat.h) since this class will be statically linked.

The contents of the C++ Rest SDK license are included in [license.txt](./license.txt).
