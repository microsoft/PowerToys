# Coding Guidance

## Working With Strings

**YOU SHOULD NOT** have hardcoded UI display strings in your C++ code. Instead, use the following guidelines to add strings to your code. Add the ID of your string to the resource file. XXX must be a unique int in the list (mostly the int ID of the last string id plus one):

- `resource.h`:

```cpp
#define IDS_PREVPANE_XYZ_SETTINGS_DISPLAYNAME                    XXX
```

- `valid.rc` under strings table:

```cpp
IDS_PREVPANE_XYZ_SETTINGS_DISPLAYNAME               L"XYZ Preview Handler"
```

- Use the `GET_RESOURCE_STRING(UINT resource_id)` method to consume strings in your code.
```cpp
#include <common.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

std::wstring GET_RESOURCE_STRING(IDS_PREVPANE_XYZ_SETTINGS_DISPLAYNAME)
```

## More On Coding Guidance
Please review these brief docs below relating to our coding standards etc.

> 👉 If you find something missing from these docs, feel free to contribute to any of our documentation files anywhere in the repository (or make some new ones\!)

* [Coding Style](./style.md)
* [Code Organization](./readme.md)