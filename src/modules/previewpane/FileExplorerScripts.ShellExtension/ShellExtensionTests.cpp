// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "ShellExtension.cpp"
#include <iostream>
#include <memory>

namespace
{
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
            throw std::runtime_error("The live catalogue must include applicable image/audio demo scripts.");
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
        std::cerr << "Usage: ShellExtensionTests.exe <shell DLL> <valid PNG> <valid WAV> [invalid/non-media paths...]\n";
        return 2;
    }
    try
    {
        winrt::init_apartment(winrt::apartment_type::single_threaded);
        const std::unique_ptr<std::remove_pointer_t<HMODULE>, decltype(&FreeLibrary)> extension{ LoadLibraryW(argv[1]), FreeLibrary };
        if (!extension)
        {
            winrt::throw_last_error();
        }
        const auto getClass = reinterpret_cast<HRESULT(__stdcall*)(REFCLSID, REFIID, void**)>(GetProcAddress(extension.get(), "DllGetClassObject"));
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
        std::wcerr << error.message().c_str() << '\n';
        return 1;
    }
}
