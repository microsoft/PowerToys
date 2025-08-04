========================================================================
    DarkMode Project Overview
========================================================================

This project demonstrates how to get started writing WinUI3 apps directly
with standard C++, using the Windows App SDK and C++/WinRT packages and
XAML compiler support to generate implementation headers from interface
(IDL) files. These headers can then be used to implement the local
Windows Runtime classes referenced in the app's XAML pages.

Steps:
1. Create an interface (IDL) file to define any local Windows Runtime
    classes referenced in the app's XAML pages.
2. Build the project once to generate implementation templates under
    the "Generated Files" folder, as well as skeleton class definitions
    under "Generated Files\sources".
3. Use the skeleton class definitions for reference to implement your
    Windows Runtime classes.

========================================================================
Learn more about Windows App SDK here:
https://docs.microsoft.com/windows/apps/windows-app-sdk/
Learn more about WinUI3 here:
https://docs.microsoft.com/windows/apps/winui/winui3/
Learn more about C++/WinRT here:
http://aka.ms/cppwinrt/
========================================================================
