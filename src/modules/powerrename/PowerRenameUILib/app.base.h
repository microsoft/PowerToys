#pragma once
namespace winrt::PowerRenameUILib::implementation
{
    template<typename D, typename... I>
    struct App_baseWithProvider : public App_base<D, ::winrt::Windows::UI::Xaml::Markup::IXamlMetadataProvider>
    {
        using IXamlType = ::winrt::Windows::UI::Xaml::Markup::IXamlType;
        IXamlType GetXamlType(::winrt::Windows::UI::Xaml::Interop::TypeName const& type)
        {
            return AppProvider()->GetXamlType(type);
        }
        IXamlType GetXamlType(::winrt::hstring const& fullName)
        {
            return AppProvider()->GetXamlType(fullName);
        }
        ::winrt::com_array<::winrt::Windows::UI::Xaml::Markup::XmlnsDefinition> GetXmlnsDefinitions()
        {
            return AppProvider()->GetXmlnsDefinitions();
        }

    private:
        bool _contentLoaded{ false };
        winrt::com_ptr<XamlMetaDataProvider> _appProvider;
        winrt::com_ptr<XamlMetaDataProvider> AppProvider()
        {
            if (!_appProvider)
            {
                _appProvider = winrt::make_self<XamlMetaDataProvider>();
            }
            return _appProvider;
        }
    };
    template<typename D, typename... I>
    using AppT2 = App_baseWithProvider<D, I...>;
}
