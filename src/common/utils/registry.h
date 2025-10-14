#pragma once

#include <Windows.h>

#include <functional>
#include <string>
#include <variant>
#include <vector>
#include <optional>
#include <cassert>
#include <sstream>

#include "../logger/logger.h"
#include "../utils/winapi_error.h"
#include "../version/version.h"

namespace registry
{
    namespace install_scope
    {
        const wchar_t INSTALL_SCOPE_REG_KEY[] = L"Software\\Classes\\powertoys\\";

        enum class InstallScope
        {
            PerMachine = 0,
            PerUser,
        };

        const InstallScope get_current_install_scope();
    }

    template<class>
    inline constexpr bool always_false_v = false;

    namespace detail
    {
        struct on_exit
        {
            std::function<void()> f;

            on_exit(std::function<void()> f) :
                f{ std::move(f) } {}
            ~on_exit() { f(); }
        };

        template<class... Ts>
        struct overloaded : Ts...
        {
            using Ts::operator()...;
        };

        template<class... Ts>
        overloaded(Ts...) -> overloaded<Ts...>;

        const wchar_t* getScopeName(HKEY scope);
    }

    struct ValueChange
    {
        using value_t = std::variant<DWORD, std::wstring>;
        static constexpr size_t VALUE_BUFFER_SIZE = 512;

        HKEY scope{};
        std::wstring path;
        std::optional<std::wstring> name; // none == default
        value_t value;
        bool required = true;

        ValueChange(const HKEY scope, std::wstring path, std::optional<std::wstring> name, value_t value, bool required = true) :
            scope{ scope }, path{ std::move(path) }, name{ std::move(name) }, value{ std::move(value) }, required{ required }
        {
        }

        std::wstring toString() const;
        bool isApplied() const;
        bool apply() const;
        bool unApply() const;

        bool requiresElevation() const { return scope == HKEY_LOCAL_MACHINE; }

    private:
        static DWORD valueTypeToWinapiType(const value_t& v);
        static void valueToBuffer(const value_t& value, wchar_t buffer[VALUE_BUFFER_SIZE], DWORD& valueSize, DWORD& type);
        static std::optional<value_t> bufferToValue(const wchar_t buffer[VALUE_BUFFER_SIZE],
                                                    const DWORD valueSize,
                                                    const DWORD type);
    };

    struct ChangeSet
    {
        std::vector<ValueChange> changes;

        bool isApplied() const;
        bool apply() const;
        bool unApply() const;
    };

    const inline std::wstring DOTNET_COMPONENT_CATEGORY_CLSID = L"{62C8FE65-4EBB-45E7-B440-6E39B2CDBF29}";
    const inline std::wstring ITHUMBNAIL_PROVIDER_CLSID = L"{E357FCCD-A995-4576-B01F-234630154E96}";
    const inline std::wstring IPREVIEW_HANDLER_CLSID = L"{8895b1c6-b41f-4c1c-a562-0d564250836f}";

    namespace shellex
    {
        enum PreviewHandlerType
        {
            preview,
            thumbnail
        };

        registry::ChangeSet generatePreviewHandler(const PreviewHandlerType handlerType,
                                                          const bool perUser,
                                                          std::wstring handlerClsid,
                                                          std::wstring powertoysVersion,
                                                          std::wstring fullPathToHandler,
                                                          std::wstring className,
                                                          std::wstring displayName,
                                                          std::vector<std::wstring> fileTypes,
                                                          std::wstring perceivedType = L"",
                                                          std::wstring fileKindType = L"");
    }
}
