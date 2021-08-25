#include "pch.h"
#include "App.h"
#include "App.g.cpp"
using namespace winrt;
using namespace Windows::UI::Xaml;
namespace winrt::PowerRenameUI_new::implementation
{
    App::App()
    {
        Initialize();
        AddRef();
        m_inner.as<::IUnknown>()->Release();
    }
    App::~App()
    {
        Close();
    }
}
