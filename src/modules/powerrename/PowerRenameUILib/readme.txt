========================================================================
    C++/WinRT PowerRenameUILib Project Overview
========================================================================

This project demonstrates how to get started writing XAML apps directly
with standard C++, using the C++/WinRT SDK component and XAML compiler 
support to generate implementation headers from interface (IDL) files.
These headers can then be used to implement the local Windows Runtime 
classes referenced in the app's XAML pages.

Steps:
1. Create an interface (IDL) file to define any local Windows Runtime 
    classes referenced in the app's XAML pages.
2. Build the project once to generate implementation templates under 
    the "Generated Files" folder, as well as skeleton class definitions 
    under "Generated Files\sources".  
3. Use the skeleton class definitions for reference to implement your
    Windows Runtime classes.

========================================================================
Learn more about C++/WinRT here:
http://aka.ms/cppwinrt/
========================================================================
