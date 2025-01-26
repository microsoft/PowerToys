#include "pch.h"
#include "cpplib.h"

int Add()
{
    RemapBuffer remapBuffer;

    // Remap A to B and B to C
    remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)0x41, (DWORD)0x42 }), std::wstring() });
    //remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)0x42, (DWORD)0x43 }), std::wstring() });
    remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)0x41, (DWORD)0 }), std::wstring() });

    auto result = LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer);
    // print the result of LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer) to the console
    // std::cout << "CheckIfRemappingsAreValid(remapBuffer) = " << static_cast<int>(result) << std::endl;

    // return the result of LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer) which is a enum as a string
    if (result == ShortcutErrorType::NoError)
    {
        return 123;
    }

    return 456;
}