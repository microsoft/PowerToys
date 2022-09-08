Explorer Command Verb Sample
================================
Demonstrates how implement a shell verb using the ExplorerCommand and ExplorerCommandState methods. 


Sample Language Implementations
===============================
C++

Files:
=============================================
dll.cpp
dll.def
dll.h
ExplorerCommandStateHandler.cpp
ExplorerCommandVerb.cpp
ExplorerCommandVerb.sln
ExplorerCommandVerb.vcproj
RegisterExtension.cpp
RegisterExtension.h
ShellHelpers.h


To build the sample using the command prompt:
=============================================
     1. Open the Command Prompt window and navigate to the ExplorerCommandVerb directory.
     2. Type msbuild ExplorerCommandVerb.sln.


To build the sample using Visual Studio (preferred method):
===========================================================
     1. Open Windows Explorer and navigate to the ExplorerCommandVerb directory.
     2. Double-click the icon for the ExplorerCommandVerb.sln file to open the file in Visual Studio.
     3. In the Build menu, select Build Solution. The application will be built in the default \Debug or \Release directory.


To run the sample:
=================
     1. Navigate to the directory that contains ExplorerCommandVerb.dll using the command prompt. Make sure you use 64-bit dll on 64-bit Windows.
     2. Type regsvr32 ExplorerCommandVerb.dll.
     3. Two new verbs will be shown on the context menu when you right-click a .txt file.
