#include "pch.h"
#include "registry.h"

namespace registry
{
    namespace install_scope
    {
        const InstallScope get_current_install_scope()
        {
            // Open HKLM key
            HKEY perMachineKey{};
            if (RegOpenKeyExW(HKEY_LOCAL_MACHINE,
                              INSTALL_SCOPE_REG_KEY,
                              0,
                              KEY_READ,
                              &perMachineKey) != ERROR_SUCCESS)
            {
                // Open HKCU key
                HKEY perUserKey{};
                if (RegOpenKeyExW(HKEY_CURRENT_USER,
                                  INSTALL_SCOPE_REG_KEY,
                                  0,
                                  KEY_READ,
                                  &perUserKey) != ERROR_SUCCESS)
                {
                    // both keys are missing
                    return InstallScope::PerMachine;
                }
                else
                {
                    DWORD dataSize{};
                    if (RegGetValueW(
                        perUserKey,
                        nullptr,
                        L"InstallScope",
                        RRF_RT_REG_SZ,
                        nullptr,
                        nullptr,
                        &dataSize) != ERROR_SUCCESS)
                    {
                        // HKCU key is missing
                        RegCloseKey(perUserKey);
                        return InstallScope::PerMachine;
                    }

                    std::wstring data;
                    data.resize(dataSize / sizeof(wchar_t));

                    if (RegGetValueW(
                            perUserKey,
                            nullptr,
                            L"InstallScope",
                            RRF_RT_REG_SZ,
                            nullptr,
                            &data[0],
                            &dataSize) != ERROR_SUCCESS)
                    {
                        // HKCU key is missing
                        RegCloseKey(perUserKey);
                        return InstallScope::PerMachine;
                    }
                    RegCloseKey(perUserKey);

                    if (data.contains(L"perUser"))
                    {
                        return InstallScope::PerUser;
                    }
                }
            }

            return InstallScope::PerMachine;
        }
    }

    namespace detail
    {
        const wchar_t* getScopeName(HKEY scope)
        {
            if (scope == HKEY_LOCAL_MACHINE)
            {
                return L"HKLM";
            }
            else if (scope == HKEY_CURRENT_USER)
            {
                return L"HKCU";
            }
            else if (scope == HKEY_CLASSES_ROOT)
            {
                return L"HKCR";
            }
            else
            {
                return L"HK??";
            }
        }
    }

    std::wstring ValueChange::toString() const
    {
        using namespace detail;

        std::wstring value_str;
        std::visit(overloaded{ [&](DWORD value) {
                                  std::wostringstream oss;
                                  oss << value;
                                  value_str = oss.str();
                              },
                               [&](const std::wstring& value) { value_str = value; } },
                   value);

        return fmt::format(L"{}\\{}\\{}:{}", detail::getScopeName(scope), path, name ? *name : L"Default", value_str);
    }

    bool ValueChange::isApplied() const
    {
        HKEY key{};
        if (auto res = RegOpenKeyExW(scope, path.c_str(), 0, KEY_READ, &key); res != ERROR_SUCCESS)
        {
            Logger::info(L"isApplied of {}: RegOpenKeyExW failed: {}", toString(), get_last_error_or_default(res));
            return false;
        }
        detail::on_exit closeKey{ [key] { RegCloseKey(key); } };

        const DWORD expectedType = valueTypeToWinapiType(value);

        DWORD retrievedType{};
        wchar_t buffer[VALUE_BUFFER_SIZE];
        DWORD valueSize = sizeof(buffer);
        if (auto res = RegQueryValueExW(key,
                                        name.has_value() ? name->c_str() : nullptr,
                                        0,
                                        &retrievedType,
                                        reinterpret_cast<LPBYTE>(&buffer),
                                        &valueSize);
            res != ERROR_SUCCESS)
        {
            Logger::info(L"isApplied of {}: RegQueryValueExW failed: {}", toString(), get_last_error_or_default(res));
            return false;
        }

        if (expectedType != retrievedType)
        {
            return false;
        }

        if (const auto retrievedValue = bufferToValue(buffer, valueSize, retrievedType))
        {
            return value == retrievedValue;
        }
        else
        {
            return false;
        }
    }

    bool ValueChange::apply() const
    {
        HKEY key{};

        if (auto res = RegCreateKeyExW(scope, path.c_str(), 0, nullptr, REG_OPTION_NON_VOLATILE, KEY_WRITE, nullptr, &key, nullptr); res !=
                                                                                                                                     ERROR_SUCCESS)
        {
            Logger::error(L"apply of {}: RegCreateKeyExW failed: {}", toString(), get_last_error_or_default(res));
            return false;
        }
        detail::on_exit closeKey{ [key] { RegCloseKey(key); } };

        wchar_t buffer[VALUE_BUFFER_SIZE];
        DWORD valueSize;
        DWORD valueType;

        valueToBuffer(value, buffer, valueSize, valueType);
        if (auto res = RegSetValueExW(key,
                                      name.has_value() ? name->c_str() : nullptr,
                                      0,
                                      valueType,
                                      reinterpret_cast<BYTE*>(buffer),
                                      valueSize);
            res != ERROR_SUCCESS)
        {
            Logger::error(L"apply of {}: RegSetValueExW failed: {}", toString(), get_last_error_or_default(res));
            return false;
        }

        return true;
    }

    bool ValueChange::unApply() const
    {
        HKEY key{};
        if (auto res = RegOpenKeyExW(scope, path.c_str(), 0, KEY_ALL_ACCESS, &key); res != ERROR_SUCCESS)
        {
            Logger::error(L"unApply of {}: RegOpenKeyExW failed: {}", toString(), get_last_error_or_default(res));
            return false;
        }
        detail::on_exit closeKey{ [key] { RegCloseKey(key); } };

        // delete the value itself
        if (auto res = RegDeleteKeyValueW(scope, path.c_str(), name.has_value() ? name->c_str() : nullptr); res != ERROR_SUCCESS)
        {
            Logger::error(L"unApply of {}: RegDeleteKeyValueW failed: {}", toString(), get_last_error_or_default(res));
            return false;
        }

        // Check if the path doesn't contain anything and delete it if so
        DWORD nValues = 0;
        DWORD maxValueLen = 0;
        const auto ok =
            RegQueryInfoKeyW(
                key, nullptr, nullptr, nullptr, nullptr, nullptr, nullptr, &nValues, nullptr, &maxValueLen, nullptr, nullptr) ==
            ERROR_SUCCESS;

        if (ok && (!nValues || !maxValueLen))
        {
            RegDeleteTreeW(scope, path.c_str());
        }
        return true;
    }

    DWORD ValueChange::valueTypeToWinapiType(const value_t& v)
    {
        return std::visit(
            [](auto&& arg) {
                using T = std::decay_t<decltype(arg)>;
                if constexpr (std::is_same_v<T, DWORD>)
                    return REG_DWORD;
                else if constexpr (std::is_same_v<T, std::wstring>)
                    return REG_SZ;
                else
                    static_assert(always_false_v<T>, "support for this registry type is not implemented");
            },
            v);
    }

    void ValueChange::valueToBuffer(const value_t& value, wchar_t buffer[VALUE_BUFFER_SIZE], DWORD& valueSize, DWORD& type)
    {
        using detail::overloaded;

        std::visit(overloaded{ [&](DWORD value) {
                                  *reinterpret_cast<DWORD*>(buffer) = value;
                                  type = REG_DWORD;
                                  valueSize = sizeof(value);
                              },
                               [&](const std::wstring& value) {
                                   assert(value.size() < VALUE_BUFFER_SIZE);
                                   value.copy(buffer, value.size());
                                   type = REG_SZ;
                                   valueSize = static_cast<DWORD>(sizeof(wchar_t) * value.size());
                               } },
                   value);
    }

    std::optional<ValueChange::value_t> ValueChange::bufferToValue(const wchar_t buffer[VALUE_BUFFER_SIZE],
                                                const DWORD valueSize,
                                                const DWORD type)
    {
        switch (type)
        {
        case REG_DWORD:
            return *reinterpret_cast<const DWORD*>(buffer);
        case REG_SZ:
        {
            if (!valueSize)
            {
                return std::wstring{};
            }
            std::wstring result{ buffer, valueSize / sizeof(wchar_t) };
            while (result[result.size() - 1] == L'\0')
            {
                result.resize(result.size() - 1);
            }
            return result;
        }
        default:
            return std::nullopt;
        }
    }

    bool ChangeSet::isApplied() const
    {
        for (const auto& c : changes)
        {
            if (c.required && !c.isApplied())
            {
                return false;
            }
        }
        return true;
    }

    bool ChangeSet::apply() const
    {
        bool ok = true;
        for (const auto& c : changes)
        {
            ok = (c.apply()||!c.required) && ok;
        }
        return ok;
    }

    bool ChangeSet::unApply() const
    {
        bool ok = true;
        for (const auto& c : changes)
        {
            ok = (c.unApply()||!c.required) && ok;
        }
        return ok;
    }

    namespace shellex
    {
        registry::ChangeSet generatePreviewHandler(const PreviewHandlerType handlerType,
                                                      const bool perUser,
                                                      std::wstring handlerClsid,
                                                      std::wstring powertoysVersion,
                                                      std::wstring fullPathToHandler,
                                                      std::wstring className,
                                                      std::wstring displayName,
                                                      std::vector<std::wstring> fileTypes,
                                                      std::wstring perceivedType,
                                                      std::wstring fileKindType)
        {
            const HKEY scope = perUser ? HKEY_CURRENT_USER : HKEY_LOCAL_MACHINE;

            std::wstring clsidPath = L"Software\\Classes\\CLSID";
            clsidPath += L'\\';
            clsidPath += handlerClsid;

            std::wstring inprocServerPath = clsidPath;
            inprocServerPath += L'\\';
            inprocServerPath += L"InprocServer32";

            std::wstring assemblyKeyValue;
            if (const auto lastDotPos = className.rfind(L'.'); lastDotPos != std::wstring::npos)
            {
                assemblyKeyValue = L"PowerToys." + className.substr(lastDotPos + 1);
            }
            else
            {
                assemblyKeyValue = L"PowerToys." + className;
            }

            assemblyKeyValue += L", Version=";
            assemblyKeyValue += powertoysVersion;
            assemblyKeyValue += L", Culture=neutral";

            std::wstring versionPath = inprocServerPath;
            versionPath += L'\\';
            versionPath += powertoysVersion;

            using vec_t = std::vector<registry::ValueChange>;
            // TODO: verify that we actually need all of those
            vec_t changes = { { scope, clsidPath, L"DisplayName", displayName },
                              { scope, clsidPath, std::nullopt, className },
                              { scope, inprocServerPath, std::nullopt, fullPathToHandler },
                              { scope, inprocServerPath, L"Assembly", assemblyKeyValue },
                              { scope, inprocServerPath, L"Class", className },
                              { scope, inprocServerPath, L"ThreadingModel", L"Apartment" } };

            for (const auto& fileType : fileTypes)
            {
                std::wstring fileTypePath = L"Software\\Classes\\" + fileType;
                std::wstring fileAssociationPath = fileTypePath + L"\\shellex\\";
                fileAssociationPath += handlerType == PreviewHandlerType::preview ? IPREVIEW_HANDLER_CLSID : ITHUMBNAIL_PROVIDER_CLSID;
                changes.push_back({ scope, fileAssociationPath, std::nullopt, handlerClsid });
                if (!fileKindType.empty())
                {
                    // Registering a file type as a kind needs to be done at the HKEY_LOCAL_MACHINE level.
                    // Make it optional as well so that we don't fail registering the handler if we can't write to HKEY_LOCAL_MACHINE.
                    std::wstring kindMapPath = L"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\KindMap";
                    changes.push_back({ HKEY_LOCAL_MACHINE, kindMapPath, fileType, fileKindType, false});
                }
                if (!perceivedType.empty())
                {
                    changes.push_back({ scope, fileTypePath, L"PerceivedType", perceivedType });
                }
                if (handlerType == PreviewHandlerType::preview && fileType == L".reg")
                {
                    // this regfile registry key has precedence over Software\Classes\.reg for .reg files
                    std::wstring regfilePath = L"Software\\Classes\\regfile\\shellex\\" + IPREVIEW_HANDLER_CLSID + L"\\";
                    changes.push_back({ scope, regfilePath, std::nullopt, handlerClsid });
                }
            }

            if (handlerType == PreviewHandlerType::preview)
            {
                const std::wstring previewHostClsid = L"{6d2b5079-2f0b-48dd-ab7f-97cec514d30b}";
                const std::wstring previewHandlerListPath = LR"(Software\Microsoft\Windows\CurrentVersion\PreviewHandlers)";

                changes.push_back({ scope, clsidPath, L"AppID", previewHostClsid });
                changes.push_back({ scope, previewHandlerListPath, handlerClsid, displayName });
            }

            return registry::ChangeSet{ .changes = std::move(changes) };
        }
    }
}
