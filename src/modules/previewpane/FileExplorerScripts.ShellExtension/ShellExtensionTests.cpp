// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <windows.h>
#include <shlobj.h>
#include <shlwapi.h>
#include <string>
#include <fstream>
#include <iostream>
#include <memory>

namespace
{
    std::wstring localAppDataOverride;

    HRESULT TestKnownFolderPath(REFKNOWNFOLDERID folder, DWORD flags, HANDLE token, PWSTR* path)
    {
        if (!localAppDataOverride.empty() && folder == FOLDERID_LocalAppData)
        {
            return SHStrDupW(localAppDataOverride.c_str(), path);
        }
        return SHGetKnownFolderPath(folder, flags, token, path);
    }
}

#define SHGetKnownFolderPath TestKnownFolderPath
#include "ShellExtension.cpp"
#undef SHGetKnownFolderPath

namespace
{
    class CatalogueFixture
    {
    public:
        CatalogueFixture()
        {
            GUID id{};
            winrt::check_hresult(CoCreateGuid(&id));
            wchar_t name[40]{};
            StringFromGUID2(id, name, ARRAYSIZE(name));
            root = std::filesystem::temp_directory_path() / (L"PowerToysExplorer-" + std::wstring(name));
            std::filesystem::create_directory(root);
            localAppDataOverride = root.wstring();
            std::filesystem::create_directories(DataDirectory());
            const auto updated = std::chrono::duration_cast<std::chrono::seconds>(std::chrono::system_clock::now().time_since_epoch()).count();
            std::ofstream cache(DataDirectory() / L"catalogue.json", std::ios::binary);
            cache.exceptions(std::ios::badbit | std::ios::failbit);
            cache << "{\"enabled\":true,\"updated\":" << updated << R"(,"scripts":[
                {"id":"files","name":"Files - fixture","kind":"files","min":1,"max":1024,"defaults":true},
                {"id":"image","name":"Image - fixture","kind":"image","min":1,"max":1,"defaults":true,"mimeTypes":["image/png"]},
                {"id":"audio","name":"Audio - fixture","kind":"audio","min":1,"max":1,"defaults":true,"mimeTypes":["audio/wav"]}
            ]})";
        }

        ~CatalogueFixture()
        {
            localAppDataOverride.clear();
            std::filesystem::remove_all(root);
        }

        std::filesystem::path root;
    };

    void VerifyCacheSharing()
    {
        const auto path = DataDirectory() / L"catalogue.json";
        const auto replacement = DataDirectory() / L"replacement.json";
        std::filesystem::copy_file(path, replacement);
        const auto reader = OpenCatalogue(path);
        if (!reader)
        {
            winrt::throw_last_error();
        }
        winrt::check_bool(ReplaceFileW(path.c_str(), replacement.c_str(), nullptr, 0, nullptr, nullptr));
        if (!ReadCatalogue().GetNamedBoolean(L"enabled", false))
        {
            throw std::runtime_error("Catalogue replacement must remain readable while a native snapshot is open.");
        }
        std::wcout << L"PASS atomic catalogue replacement with a live native reader\n";
    }

    void VerifySelectionContract(const std::filesystem::path& root)
    {
        const auto real = root / L"real";
        const auto nested = real / L"nested";
        std::filesystem::create_directories(nested);
        const auto file = nested / L"sample.txt";
        std::ofstream(file) << "fixture";
        if (!IsSupportedFile(file.wstring()) || IsSupportedFile(nested.wstring()))
        {
            throw std::runtime_error("Selection must accept local regular files, not folders.");
        }
        for (const auto name : { L"", L".", L"..", L"name.", L"name ", L"CON.txt", L"con .txt", L"COM0", L"lpt\x00b9.txt", L"name:stream", L"bad?name", L"\x2003", L"\x0085\x00a0" })
        {
            if (IsSafePathComponent(name))
            {
                throw std::runtime_error("Native filename eligibility differs from the managed contract.");
            }
        }
        for (const auto name : { L".config", L"file.txt", L"COM10.txt", L"some value" })
        {
            if (!IsSafePathComponent(name))
            {
                throw std::runtime_error("Safe filename must remain eligible.");
            }
        }
        for (const auto& path : { (nested / L".." / L"nested" / L"sample.txt").wstring(), file.wstring() + L":stream", L"\\\\?\\" + file.wstring() })
        {
            if (IsSupportedFile(path))
            {
                throw std::runtime_error("Traversal, stream and device paths must not be eligible.");
            }
        }

        const auto link = root / L"linked";
        winrt::check_bool(CreateSymbolicLinkW(link.c_str(), real.c_str(), SYMBOLIC_LINK_FLAG_DIRECTORY | SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE));
        if (IsSupportedFile((link / L"nested" / L"sample.txt").wstring()))
        {
            throw std::runtime_error("Selection must reject reparse points anywhere in its ancestry.");
        }
        const auto fileLink = root / L"linked.txt";
        winrt::check_bool(CreateSymbolicLinkW(fileLink.c_str(), file.c_str(), SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE));
        if (IsSupportedFile(fileLink.wstring()))
        {
            throw std::runtime_error("Selection must reject file reparse points.");
        }

        const auto longest = nested / std::wstring(259 - nested.wstring().size() - 1, L'a');
        std::ofstream boundary(longest);
        boundary.exceptions(std::ios::badbit | std::ios::failbit);
        boundary << "boundary";
        boundary.close();
        if (!IsSupportedFile(longest.wstring()))
        {
            throw std::runtime_error("The supported 259-character boundary must remain eligible.");
        }
        const auto tooLong = longest.wstring() + L"b";
        const auto extended = L"\\\\?\\" + tooLong;
        const auto raw = CreateFileW(extended.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW, FILE_ATTRIBUTE_NORMAL, nullptr);
        if (raw == INVALID_HANDLE_VALUE)
        {
            winrt::throw_last_error();
        }
        CloseHandle(raw);
        const auto rejected = !IsSupportedFile(tooLong);
        winrt::check_bool(DeleteFileW(extended.c_str()));
        if (!rejected)
        {
            throw std::runtime_error("Selection must reject paths beyond the managed 259-character limit.");
        }
        std::wcout << L"PASS native selection grammar, files/folders, reparse ancestors and 259/260 boundaries\n";
    }

    ComPtr<IExplorerCommand> CreateCommand(IClassFactory* factory)
    {
        ComPtr<IExplorerCommand> command;
        winrt::check_hresult(factory->CreateInstance(nullptr, IID_PPV_ARGS(&command)));
        return command;
    }

    std::vector<std::wstring> Titles(IExplorerCommand* command)
    {
        ComPtr<IEnumExplorerCommand> enumerator;
        winrt::check_hresult(command->EnumSubCommands(&enumerator));
        std::vector<std::wstring> titles;
        ComPtr<IExplorerCommand> child;
        while (enumerator->Next(1, &child, nullptr) == S_OK)
        {
            PWSTR title = nullptr;
            winrt::check_hresult(child->GetTitle(nullptr, &title));
            titles.emplace_back(title);
            CoTaskMemFree(title);
            child.Reset();
        }
        return titles;
    }

    void Verify(IClassFactory* factory, IExplorerCommand* classic, const wchar_t* path, const wchar_t* mediaPrefix)
    {
        ComPtr<IShellItem> item;
        winrt::check_hresult(SHCreateItemFromParsingName(path, nullptr, IID_PPV_ARGS(&item)));
        ComPtr<IShellItemArray> selection;
        winrt::check_hresult(SHCreateShellItemArrayFromShellItem(item.Get(), IID_PPV_ARGS(&selection)));
        const auto modern = CreateCommand(factory);
        EXPCMDSTATE state{};
        const auto fast = modern->GetState(selection.Get(), FALSE, &state);
        if (fast != S_OK && fast != E_PENDING)
        {
            winrt::check_hresult(fast);
        }
        if (mediaPrefix && fast != E_PENDING)
        {
            throw std::runtime_error("Unverified media must defer classification on the fast state path.");
        }
        winrt::check_hresult(modern->GetState(selection.Get(), TRUE, &state));
        const auto expected = Titles(modern.Get());
        if (mediaPrefix && std::none_of(expected.begin(), expected.end(), [&](const auto& title) { return title.starts_with(mediaPrefix); }))
        {
            throw std::runtime_error("The catalogue must include applicable image/audio demo scripts.");
        }
        if (!mediaPrefix && std::any_of(expected.begin(), expected.end(), [](const auto& title) { return title.starts_with(L"Image -") || title.starts_with(L"Audio -"); }))
        {
            throw std::runtime_error("An invalid/non-media selection exposes media scripts.");
        }

        ComPtr<IObjectWithSelection> stored;
        winrt::check_hresult(classic->QueryInterface(IID_PPV_ARGS(&stored)));
        winrt::check_hresult(stored->SetSelection(selection.Get()));
        if (Titles(classic) != expected)
        {
            throw std::runtime_error("Classic enumeration without GetState differs from the modern menu.");
        }
        winrt::check_hresult(classic->GetState(nullptr, TRUE, &state));
        winrt::check_hresult(stored->SetSelection(selection.Get()));
        if (Titles(classic) != expected)
        {
            throw std::runtime_error("Reapplying the selection drops applicable media scripts.");
        }
        std::wcout << L"PASS classic/modern parity: " << path << L" (" << expected.size() << L" commands)\n";

        std::vector<double> fastTimes;
        std::vector<double> verifiedTimes;
        std::vector<double> classicTimes;
        for (int i = 0; i < 110; ++i)
        {
            const auto command = CreateCommand(factory);
            const auto start = std::chrono::steady_clock::now();
            const auto result = command->GetState(selection.Get(), FALSE, &state);
            const auto fastEnd = std::chrono::steady_clock::now();
            if (result != S_OK && result != E_PENDING)
            {
                winrt::check_hresult(result);
            }
            winrt::check_hresult(command->GetState(selection.Get(), TRUE, &state));
            const auto verifiedEnd = std::chrono::steady_clock::now();
            winrt::check_hresult(stored->SetSelection(selection.Get()));
            const auto classicStart = std::chrono::steady_clock::now();
            if (Titles(classic) != expected)
            {
                throw std::runtime_error("Repeated enumeration changed menu contents.");
            }
            const auto end = std::chrono::steady_clock::now();
            if (i >= 10)
            {
                fastTimes.push_back(std::chrono::duration<double, std::milli>(fastEnd - start).count());
                verifiedTimes.push_back(std::chrono::duration<double, std::milli>(verifiedEnd - fastEnd).count());
                classicTimes.push_back(std::chrono::duration<double, std::milli>(end - classicStart).count());
            }
        }
        std::sort(fastTimes.begin(), fastTimes.end());
        std::sort(verifiedTimes.begin(), verifiedTimes.end());
        std::sort(classicTimes.begin(), classicTimes.end());
        std::wcout << L"P95 menu latency: fast=" << fastTimes[95] << L"ms; verified=" << verifiedTimes[95]
                   << L"ms; classic=" << classicTimes[95] << L"ms\n";
        if (fastTimes[95] >= 10 || verifiedTimes[95] >= 20 || classicTimes[95] >= 20)
        {
            throw std::runtime_error("Menu latency exceeded 10ms fast / 20ms verified or classic thresholds.");
        }
    }
}

int wmain(int argc, wchar_t** argv)
{
    if (argc < 4)
    {
        std::cerr << "Usage: ShellExtensionTests.exe <--isolated | shell DLL> <valid PNG> <valid WAV> [invalid/non-media paths...]\n";
        return 2;
    }
    try
    {
        winrt::init_apartment(winrt::apartment_type::single_threaded);
        const auto isolated = std::wstring_view(argv[1]) == L"--isolated";
        std::unique_ptr<CatalogueFixture> fixture;
        if (isolated)
        {
            fixture = std::make_unique<CatalogueFixture>();
            VerifyCacheSharing();
            VerifySelectionContract(fixture->root);
        }
        const std::unique_ptr<std::remove_pointer_t<HMODULE>, decltype(&FreeLibrary)> extension{ isolated ? nullptr : LoadLibraryW(argv[1]), FreeLibrary };
        if (!isolated && !extension)
        {
            winrt::throw_last_error();
        }
        const auto getClass = isolated ? DllGetClassObject : reinterpret_cast<HRESULT(__stdcall*)(REFCLSID, REFIID, void**)>(GetProcAddress(extension.get(), "DllGetClassObject"));
        if (!getClass)
        {
            winrt::throw_last_error();
        }
        ComPtr<IClassFactory> factory;
        winrt::check_hresult(getClass(__uuidof(ScriptCommand), IID_PPV_ARGS(&factory)));
        const auto classic = CreateCommand(factory.Get());
        Verify(factory.Get(), classic.Get(), argv[2], L"Image -");
        Verify(factory.Get(), classic.Get(), argv[3], L"Audio -");
        for (int i = 4; i < argc; ++i)
        {
            Verify(factory.Get(), classic.Get(), argv[i], nullptr);
        }
        const auto parse = [](const wchar_t* path) {
            PIDLIST_ABSOLUTE raw = nullptr;
            winrt::check_hresult(SHParseDisplayName(path, nullptr, &raw, 0, nullptr));
            return std::unique_ptr<std::remove_pointer_t<PIDLIST_ABSOLUTE>, decltype(&CoTaskMemFree)>{ raw, CoTaskMemFree };
        };
        const auto image = parse(argv[2]);
        const auto audio = parse(argv[3]);
        PCIDLIST_ABSOLUTE items[]{ image.get(), audio.get() };
        ComPtr<IShellItemArray> multiple;
        winrt::check_hresult(SHCreateShellItemArrayFromIDLists(2, items, &multiple));
        const auto modern = CreateCommand(factory.Get());
        EXPCMDSTATE state{};
        winrt::check_hresult(modern->GetState(multiple.Get(), TRUE, &state));
        const auto expected = Titles(modern.Get());
        ComPtr<IObjectWithSelection> stored;
        winrt::check_hresult(classic.As(&stored));
        winrt::check_hresult(stored->SetSelection(multiple.Get()));
        if (Titles(classic.Get()) != expected ||
            std::any_of(expected.begin(), expected.end(), [](const auto& title) { return title.starts_with(L"Image -") || title.starts_with(L"Audio -"); }))
        {
            throw std::runtime_error("Multiple selections must preserve file-only matching in both menus.");
        }
        std::wcout << L"PASS multiple selections preserve file-only matching\n";
        winrt::check_hresult(stored->SetSelection(nullptr));
        winrt::check_hresult(classic->GetState(nullptr, TRUE, &state));
        if (state != ECS_HIDDEN || !Titles(classic.Get()).empty())
        {
            throw std::runtime_error("Clearing the selection must hide the menu and remove all commands.");
        }
        std::wcout << L"PASS clearing selection removes all commands\n";
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << '\n';
        return 1;
    }
    catch (const winrt::hresult_error& error)
    {
        std::cerr << "HRESULT 0x" << std::hex << error.code().value << ": " << winrt::to_string(error.message()) << '\n';
        return 1;
    }
}
