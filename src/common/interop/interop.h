#pragma once

#include <common\keyboard_layout.h>
using namespace System;

//https://docs.microsoft.com/en-us/cpp/dotnet/how-to-wrap-native-class-for-use-by-csharp?view=vs-2019
namespace interop
{
public ref class LayoutMapManaged
    {
    public:
        LayoutMapManaged() :
            _map(new LayoutMap) {}

        ~LayoutMapManaged()
        {
            delete _map;
        }

        String ^ GetKeyName(DWORD key) 
        {
            return gcnew String(_map->GetKeyName(key).c_str());
        }

        void Updatelayout()
        {
            _map->UpdateLayout();
        }

    protected:
        !LayoutMapManaged()
        {
            delete _map;
        }

    private:
        LayoutMap* _map;
    };
}

