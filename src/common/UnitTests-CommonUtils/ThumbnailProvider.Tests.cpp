#include "pch.h"
#include "TestHelpers.h"
#include "ThumbnailProviderTestProtocol.h"

#include <TlHelp32.h>
#include <Shlwapi.h>
#include <thumbnail_provider.h>
#include <wil/com.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    namespace
    {
        class NoProgressStream : public IStream
        {
        public:
            IFACEMETHODIMP QueryInterface(REFIID riid, void** object) override
            {
                if (!object)
                {
                    return E_POINTER;
                }

                if (riid == IID_IUnknown || riid == IID_ISequentialStream || riid == IID_IStream)
                {
                    *object = static_cast<IStream*>(this);
                    AddRef();
                    return S_OK;
                }

                *object = nullptr;
                return E_NOINTERFACE;
            }

            IFACEMETHODIMP_(ULONG)
            AddRef() override
            {
                return InterlockedIncrement(&m_referenceCount);
            }

            IFACEMETHODIMP_(ULONG)
            Release() override
            {
                return InterlockedDecrement(&m_referenceCount);
            }

            long reference_count() const
            {
                return m_referenceCount;
            }

            IFACEMETHODIMP Read(void*, ULONG, ULONG* bytesRead) override
            {
                if (bytesRead)
                {
                    *bytesRead = 0;
                }

                return S_OK;
            }

            IFACEMETHODIMP Write(const void*, ULONG, ULONG*) override
            {
                return E_NOTIMPL;
            }

            IFACEMETHODIMP Seek(LARGE_INTEGER, DWORD, ULARGE_INTEGER*) override
            {
                return E_NOTIMPL;
            }

            IFACEMETHODIMP SetSize(ULARGE_INTEGER) override
            {
                return E_NOTIMPL;
            }

            IFACEMETHODIMP CopyTo(IStream*, ULARGE_INTEGER, ULARGE_INTEGER*, ULARGE_INTEGER*) override
            {
                return E_NOTIMPL;
            }

            IFACEMETHODIMP Commit(DWORD) override
            {
                return E_NOTIMPL;
            }

            IFACEMETHODIMP Revert() override
            {
                return E_NOTIMPL;
            }

            IFACEMETHODIMP LockRegion(ULARGE_INTEGER, ULARGE_INTEGER, DWORD) override
            {
                return E_NOTIMPL;
            }

            IFACEMETHODIMP UnlockRegion(ULARGE_INTEGER, ULARGE_INTEGER, DWORD) override
            {
                return E_NOTIMPL;
            }

            IFACEMETHODIMP Stat(STATSTG*, DWORD) override
            {
                return E_NOTIMPL;
            }

            IFACEMETHODIMP Clone(IStream**) override
            {
                return E_NOTIMPL;
            }

        private:
            long m_referenceCount = 1;
        };

        std::filesystem::path get_cmd_path()
        {
            wchar_t systemDirectory[MAX_PATH]{};
            GetSystemDirectoryW(systemDirectory, ARRAYSIZE(systemDirectory));
            return std::filesystem::path{ systemDirectory } / L"cmd.exe";
        }

        std::filesystem::path get_ping_path()
        {
            wchar_t systemDirectory[MAX_PATH]{};
            GetSystemDirectoryW(systemDirectory, ARRAYSIZE(systemDirectory));
            return std::filesystem::path{ systemDirectory } / L"ping.exe";
        }

        std::wstring quote_argument(const std::filesystem::path& argument)
        {
            return L"\"" + argument.wstring() + L"\"";
        }

        struct contained_processes
        {
            DWORD parent_id = 0;
            DWORD descendant_id = 0;
        };

        std::optional<contained_processes> find_contained_processes(
            const std::wstring& uniqueParentExecutable,
            const wchar_t* descendantExecutable)
        {
            wil::unique_handle snapshot{ CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0) };
            if (!snapshot || snapshot.get() == INVALID_HANDLE_VALUE)
            {
                return std::nullopt;
            }

            std::vector<PROCESSENTRY32W> processes;
            PROCESSENTRY32W entry{ sizeof(entry) };
            if (!Process32FirstW(snapshot.get(), &entry))
            {
                return std::nullopt;
            }

            do
            {
                processes.push_back(entry);
            } while (Process32NextW(snapshot.get(), &entry));

            for (const auto& parent : processes)
            {
                if (_wcsicmp(parent.szExeFile, uniqueParentExecutable.c_str()) != 0)
                {
                    continue;
                }

                for (const auto& child : processes)
                {
                    if (child.th32ParentProcessID == parent.th32ProcessID &&
                        _wcsicmp(child.szExeFile, descendantExecutable) == 0)
                    {
                        return contained_processes{ parent.th32ProcessID, child.th32ProcessID };
                    }
                }
            }

            return std::nullopt;
        }

        std::filesystem::path get_process_image(HANDLE process)
        {
            std::wstring imagePath(32'768, L'\0');
            DWORD length = static_cast<DWORD>(imagePath.size());
            if (!QueryFullProcessImageNameW(process, 0, imagePath.data(), &length))
            {
                return {};
            }

            imagePath.resize(length);
            return imagePath;
        }

        struct process_termination_observation
        {
            DWORD initial_wait = WAIT_FAILED;
            bool fallback_termination_used = false;
            bool exit_code_read = false;
            DWORD exit_code = STILL_ACTIVE;
        };

        process_termination_observation observe_process_termination(HANDLE process) noexcept
        {
            process_termination_observation observation;
            if (!process)
            {
                return observation;
            }

            observation.initial_wait = WaitForSingleObject(process, 2'000);
            if (observation.initial_wait != WAIT_OBJECT_0)
            {
                observation.fallback_termination_used =
                    TerminateProcess(process, ERROR_TIMEOUT) != FALSE;
                WaitForSingleObject(process, 2'000);
            }

            observation.exit_code_read =
                GetExitCodeProcess(process, &observation.exit_code) != FALSE;
            return observation;
        }

        class nonthrowing_temp_directory
        {
        public:
            nonthrowing_temp_directory()
            {
                wchar_t tempPath[MAX_PATH]{};
                Assert::IsTrue(GetTempPathW(ARRAYSIZE(tempPath), tempPath) != 0);

                wchar_t uniquePath[MAX_PATH]{};
                Assert::IsTrue(GetTempFileNameW(tempPath, L"PTP", 0, uniquePath) != 0);
                m_path = uniquePath;

                std::error_code error;
                std::filesystem::remove(m_path, error);
                if (error)
                {
                    cleanup();
                    Assert::Fail(L"Could not prepare the test-owned timeout directory.");
                }

                error.clear();
                std::filesystem::create_directory(m_path, error);
                if (error)
                {
                    cleanup();
                    Assert::Fail(L"Could not create the test-owned timeout directory.");
                }
            }

            ~nonthrowing_temp_directory() noexcept
            {
                cleanup();
            }

            nonthrowing_temp_directory(const nonthrowing_temp_directory&) = delete;
            nonthrowing_temp_directory& operator=(const nonthrowing_temp_directory&) = delete;

            const std::filesystem::path& path() const noexcept
            {
                return m_path;
            }

            bool cleanup() noexcept
            {
                if (m_path.empty())
                {
                    return true;
                }

                for (int attempt = 0; attempt < 10; ++attempt)
                {
                    std::error_code error;
                    std::filesystem::remove_all(m_path, error);
                    error.clear();
                    const auto stillExists = std::filesystem::exists(m_path, error);
                    if (!error && !stillExists)
                    {
                        m_path.clear();
                        return true;
                    }

                    Sleep(50);
                }

                return false;
            }

        private:
            std::filesystem::path m_path;
        };

        struct provider_case
        {
            const wchar_t* executable;
            const wchar_t* sample;
            const wchar_t* extension;
        };

        constexpr provider_case providers[] = {
            { L"PowerToys.SvgThumbnailProvider.exe", L"src\\modules\\previewpane\\UnitTests-SvgThumbnailProvider\\HelperFiles\\file1.svg", L".svg" },
            { L"PowerToys.PdfThumbnailProvider.exe", L"src\\modules\\previewpane\\UnitTests-PdfThumbnailProvider\\HelperFiles\\sample.pdf", L".pdf" },
            { L"PowerToys.GcodeThumbnailProvider.exe", L"src\\modules\\previewpane\\UnitTests-GcodeThumbnailProvider\\HelperFiles\\sample.gcode", L".gcode" },
            { L"PowerToys.BgcodeThumbnailProvider.exe", L"src\\modules\\previewpane\\UnitTests-BgcodeThumbnailProvider\\HelperFiles\\sample.bgcode", L".bgcode" },
            { L"PowerToys.QoiThumbnailProvider.exe", L"src\\modules\\previewpane\\UnitTests-QoiThumbnailProvider\\HelperFiles\\sample.qoi", L".qoi" },
            { L"PowerToys.StlThumbnailProvider.exe", L"src\\modules\\previewpane\\UnitTests-StlThumbnailProvider\\HelperFiles\\sample.stl", L".stl" },
        };

        enum class configuration_source
        {
            unresolved,
            environment,
            self_discovery,
        };

        struct integration_configuration
        {
            std::filesystem::path module;
            std::filesystem::path executable_directory;
            std::filesystem::path repository_root;
            configuration_source executable_directory_source = configuration_source::unresolved;
            configuration_source repository_root_source = configuration_source::unresolved;
        };

        const wchar_t* configuration_source_name(configuration_source source)
        {
            switch (source)
            {
            case configuration_source::environment:
                return L"environment override";
            case configuration_source::self_discovery:
                return L"self-discovery";
            default:
                return L"unresolved";
            }
        }

        const wchar_t* launch_status_name(thumbnail_provider::launch_status status)
        {
            switch (status)
            {
            case thumbnail_provider::launch_status::completed:
                return L"completed";
            case thumbnail_provider::launch_status::timed_out:
                return L"timed_out";
            default:
                return L"failed";
            }
        }

        char module_anchor;

        std::filesystem::path get_test_module_path()
        {
            HMODULE module = nullptr;
            if (!GetModuleHandleExW(
                    GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                    reinterpret_cast<LPCWSTR>(&module_anchor),
                    &module))
            {
                return {};
            }

            std::wstring modulePath(MAX_PATH, L'\0');
            while (true)
            {
                SetLastError(ERROR_SUCCESS);
                const auto length =
                    GetModuleFileNameW(module, modulePath.data(), static_cast<DWORD>(modulePath.size()));
                if (length == 0)
                {
                    return {};
                }

                if (length < modulePath.size())
                {
                    modulePath.resize(length);
                    return modulePath;
                }

                if (modulePath.size() >= 32'768)
                {
                    return {};
                }

                modulePath.resize(modulePath.size() * 2);
            }
        }

        std::optional<std::pair<std::filesystem::path, std::filesystem::path>>
        discover_build_layout(const std::filesystem::path& modulePath)
        {
            auto candidate = modulePath.parent_path();
            while (!candidate.empty())
            {
                const auto configuration = candidate.filename().wstring();
                const auto platform = candidate.parent_path().filename().wstring();
                const auto supportedConfiguration =
                    _wcsicmp(configuration.c_str(), L"Release") == 0 ||
                    _wcsicmp(configuration.c_str(), L"Debug") == 0;
                const auto supportedPlatform =
                    _wcsicmp(platform.c_str(), L"x64") == 0 ||
                    _wcsicmp(platform.c_str(), L"ARM64") == 0;
                if (supportedConfiguration && supportedPlatform)
                {
                    return std::pair{
                        candidate.lexically_normal(),
                        candidate.parent_path().parent_path().lexically_normal()
                    };
                }

                const auto parent = candidate.parent_path();
                if (parent == candidate)
                {
                    break;
                }

                candidate = parent;
            }

            return std::nullopt;
        }

        integration_configuration resolve_integration_configuration(
            const std::filesystem::path& modulePath,
            const std::filesystem::path& executableDirectoryOverride,
            const std::filesystem::path& repositoryRootOverride)
        {
            integration_configuration configuration;
            configuration.module = modulePath.lexically_normal();
            const auto discovered = discover_build_layout(modulePath);

            if (!executableDirectoryOverride.empty())
            {
                configuration.executable_directory = executableDirectoryOverride.lexically_normal();
                configuration.executable_directory_source = configuration_source::environment;
            }
            else if (discovered)
            {
                configuration.executable_directory = discovered->first;
                configuration.executable_directory_source = configuration_source::self_discovery;
            }

            if (!repositoryRootOverride.empty())
            {
                configuration.repository_root = repositoryRootOverride.lexically_normal();
                configuration.repository_root_source = configuration_source::environment;
            }
            else if (discovered)
            {
                configuration.repository_root = discovered->second;
                configuration.repository_root_source = configuration_source::self_discovery;
            }

            return configuration;
        }

        void append_configuration_issue(std::wstring& diagnostics, const std::wstring& issue)
        {
            if (!diagnostics.empty())
            {
                diagnostics += L"\n";
            }

            diagnostics += L"- ";
            diagnostics += issue;
        }

        std::wstring widen_ascii(const std::string& value)
        {
            return { value.begin(), value.end() };
        }

        std::wstring validate_integration_configuration(const integration_configuration& configuration)
        {
            std::wstring diagnostics;
            std::error_code error;
            bool executableDirectoryValid = false;
            bool repositoryRootValid = false;

            if (configuration.module.empty() ||
                !configuration.module.is_absolute() ||
                !std::filesystem::is_regular_file(configuration.module, error))
            {
                append_configuration_issue(
                    diagnostics,
                    L"Loaded Common.Utils.UnitTests module path is unavailable or invalid: " +
                        configuration.module.wstring());
            }

            error.clear();
            if (configuration.executable_directory.empty())
            {
                append_configuration_issue(
                    diagnostics,
                    L"Could not derive the x64/ARM64 Release/Debug executable directory from the loaded test module.");
            }
            else if (!configuration.executable_directory.is_absolute() ||
                     !std::filesystem::is_directory(configuration.executable_directory, error))
            {
                append_configuration_issue(
                    diagnostics,
                    L"Thumbnail-provider executable directory is unavailable or invalid: " +
                        configuration.executable_directory.wstring());
            }
            else
            {
                executableDirectoryValid = true;
            }

            error.clear();
            if (configuration.repository_root.empty())
            {
                append_configuration_issue(
                    diagnostics,
                    L"Could not derive the repository root from the loaded test module.");
            }
            else if (!configuration.repository_root.is_absolute() ||
                     !std::filesystem::is_regular_file(configuration.repository_root / L"PowerToys.slnx", error))
            {
                append_configuration_issue(
                    diagnostics,
                    L"Repository root is unavailable or does not contain PowerToys.slnx: " +
                        configuration.repository_root.wstring());
            }
            else
            {
                repositoryRootValid = true;
            }

            for (const auto& provider : providers)
            {
                if (executableDirectoryValid)
                {
                    error.clear();
                    const auto application = configuration.executable_directory / provider.executable;
                    if (!std::filesystem::is_regular_file(application, error))
                    {
                        append_configuration_issue(
                            diagnostics,
                            L"Missing provider executable: " + application.wstring());
                    }
                }

                if (repositoryRootValid)
                {
                    error.clear();
                    const auto sample = configuration.repository_root / provider.sample;
                    if (!std::filesystem::is_regular_file(sample, error))
                    {
                        append_configuration_issue(
                            diagnostics,
                            L"Missing provider sample: " + sample.wstring());
                    }
                }
            }

            return diagnostics;
        }

        std::filesystem::path get_environment_path(const wchar_t* name)
        {
            const auto length = GetEnvironmentVariableW(name, nullptr, 0);
            if (length == 0)
            {
                return {};
            }

            std::wstring value(length, L'\0');
            if (GetEnvironmentVariableW(name, value.data(), length) != length - 1)
            {
                return {};
            }

            value.resize(length - 1);
            return value;
        }
    }

    TEST_CLASS (ThumbnailProviderTests)
    {
    public:
        TEST_METHOD (CopySvgStream_ZeroByteSuccessfulRead_ReturnsReadFault)
        {
            TestHelpers::TempFile outputFile{ L"", L".svg" };
            NoProgressStream stream;

            const auto result = thumbnail_provider::copy_stream_to_file(&stream, outputFile.path());

            Assert::AreEqual(HRESULT_FROM_WIN32(ERROR_READ_FAULT), result);
        }

        TEST_METHOD (CopyStream_ForwardProgress_PreservesBytes)
        {
            const BYTE expected[] = { 0x3c, 0x73, 0x76, 0x67, 0x3e };
            wil::com_ptr<IStream> stream;
            stream.attach(SHCreateMemStream(expected, ARRAYSIZE(expected)));
            Assert::IsNotNull(stream.get());
            TestHelpers::TempFile outputFile{ L"", L".svg" };

            const auto result = thumbnail_provider::copy_stream_to_file(stream.get(), outputFile.path());

            Assert::AreEqual(S_OK, result);
            std::ifstream file(outputFile.path(), std::ios::binary);
            const std::vector<BYTE> actual(
                (std::istreambuf_iterator<char>(file)),
                std::istreambuf_iterator<char>());
            Assert::AreEqual(ARRAYSIZE(expected), actual.size());
            Assert::IsTrue(std::equal(std::begin(expected), std::end(expected), actual.begin()));
        }

        TEST_METHOD (ParseTimeout_UsesConfiguredBoundedValue)
        {
            Assert::AreEqual<DWORD>(thumbnail_provider::default_timeout_ms, thumbnail_provider::parse_timeout(L""));
            Assert::AreEqual<DWORD>(thumbnail_provider::minimum_timeout_ms, thumbnail_provider::parse_timeout(L"1000"));
            Assert::AreEqual<DWORD>(45'000, thumbnail_provider::parse_timeout(L"45000"));
            Assert::AreEqual<DWORD>(thumbnail_provider::maximum_timeout_ms, thumbnail_provider::parse_timeout(L"300000"));
            Assert::AreEqual<DWORD>(thumbnail_provider::default_timeout_ms, thumbnail_provider::parse_timeout(L"0"));
            Assert::AreEqual<DWORD>(thumbnail_provider::default_timeout_ms, thumbnail_provider::parse_timeout(L"999"));
            Assert::AreEqual<DWORD>(thumbnail_provider::default_timeout_ms, thumbnail_provider::parse_timeout(L"300001"));
            Assert::AreEqual<DWORD>(thumbnail_provider::default_timeout_ms, thumbnail_provider::parse_timeout(L"4294967296"));
            Assert::AreEqual<DWORD>(thumbnail_provider::default_timeout_ms, thumbnail_provider::parse_timeout(L"45000ms"));
            Assert::AreEqual<DWORD>(thumbnail_provider::default_timeout_ms, thumbnail_provider::parse_timeout(L"invalid"));
        }

        TEST_METHOD (ReleaseStream_NullAndValidPointersAreSafe)
        {
            IStream* nullStream = nullptr;
            thumbnail_provider::release_stream(nullStream);
            Assert::IsNull(nullStream);

            NoProgressStream stream;
            stream.AddRef();
            IStream* streamPointer = &stream;

            thumbnail_provider::release_stream(streamPointer);

            Assert::IsNull(streamPointer);
            Assert::AreEqual<long>(1, stream.reference_count());
        }

        TEST_METHOD (ResolveIntegrationConfiguration_SelfDiscoversSupportedBuildLayouts)
        {
            const wchar_t* configurations[] = { L"Release", L"Debug" };
            const wchar_t* platforms[] = { L"x64", L"ARM64" };
            for (const auto platformName : platforms)
            {
                for (const auto configurationName : configurations)
                {
                    const auto repositoryRoot = std::filesystem::path{ L"C:\\repo" };
                    const auto executableDirectory =
                        repositoryRoot / platformName / configurationName;
                    const auto module =
                        executableDirectory /
                        L"tests\\UnitTestsCommonUtils\\Common.Utils.UnitTests.dll";

                    const auto configuration =
                        resolve_integration_configuration(module, {}, {});

                    Assert::AreEqual(
                        executableDirectory.c_str(),
                        configuration.executable_directory.c_str());
                    Assert::AreEqual(
                        repositoryRoot.c_str(),
                        configuration.repository_root.c_str());
                    Assert::IsTrue(
                        configuration.executable_directory_source ==
                        configuration_source::self_discovery);
                    Assert::IsTrue(
                        configuration.repository_root_source ==
                        configuration_source::self_discovery);
                }
            }
        }

        TEST_METHOD (ResolveIntegrationConfiguration_EnvironmentOverridesSelfDiscovery)
        {
            const auto module = std::filesystem::path{
                L"C:\\repo\\x64\\Release\\tests\\UnitTestsCommonUtils\\Common.Utils.UnitTests.dll"
            };
            const auto executableOverride =
                std::filesystem::path{ L"D:\\provider-build" };
            const auto repositoryOverride =
                std::filesystem::path{ L"E:\\provider-source" };

            const auto configuration = resolve_integration_configuration(
                module,
                executableOverride,
                repositoryOverride);

            Assert::AreEqual(
                executableOverride.c_str(),
                configuration.executable_directory.c_str());
            Assert::AreEqual(
                repositoryOverride.c_str(),
                configuration.repository_root.c_str());
            Assert::IsTrue(
                configuration.executable_directory_source ==
                configuration_source::environment);
            Assert::IsTrue(
                configuration.repository_root_source ==
                configuration_source::environment);
        }

        TEST_METHOD (LaunchInJob_BuiltProviderExecutablesCompleteWithinDefaultBudget)
        {
            constexpr wchar_t executableDirectoryEnvironmentVariable[] =
                L"POWERTOYS_THUMBNAIL_PROVIDER_EXECUTABLE_DIRECTORY";
            constexpr wchar_t repositoryRootEnvironmentVariable[] =
                L"POWERTOYS_REPOSITORY_ROOT";

            const auto configuration = resolve_integration_configuration(
                get_test_module_path(),
                get_environment_path(executableDirectoryEnvironmentVariable),
                get_environment_path(repositoryRootEnvironmentVariable));

            const auto configurationMessage =
                L"Executable integration configuration: test module=" +
                configuration.module.wstring() +
                L"; executable directory source=" +
                configuration_source_name(configuration.executable_directory_source) +
                L"; executable directory=" +
                configuration.executable_directory.wstring() +
                L"; repository root source=" +
                configuration_source_name(configuration.repository_root_source) +
                L"; repository root=" +
                configuration.repository_root.wstring();
            Logger::WriteMessage(configurationMessage.c_str());

            const auto configurationIssues =
                validate_integration_configuration(configuration);
            if (!configurationIssues.empty())
            {
                const auto message =
                    L"Executable thumbnail-provider integration configuration is invalid:\n" +
                    configurationIssues +
                    L"\nExpected the loaded Common.Utils.UnitTests module under "
                    L"<repository>\\x64|ARM64\\Release|Debug\\tests\\UnitTestsCommonUtils, "
                    L"or set absolute " +
                    executableDirectoryEnvironmentVariable +
                    L" and " +
                    repositoryRootEnvironmentVariable +
                    L" overrides. This test cannot skip or pass without all-six-provider coverage.";
                Assert::Fail(message.c_str());
                return;
            }

            TestHelpers::TempDirectory tempDirectory;
            std::chrono::milliseconds slowest{};
            size_t executions = 0;
            size_t successes = 0;
            std::wstring failures;
            for (size_t index = 0; index < ARRAYSIZE(providers); ++index)
            {
                const auto& provider = providers[index];
                const auto application =
                    configuration.executable_directory / provider.executable;
                const auto source =
                    configuration.repository_root / provider.sample;
                const auto input = std::filesystem::path{ tempDirectory.path() } /
                                   (L"provider-" + std::to_wstring(index) + provider.extension);
                ++executions;

                const auto startMessage =
                    L"Provider execution " +
                    std::to_wstring(index + 1) +
                    L"/" +
                    std::to_wstring(ARRAYSIZE(providers)) +
                    L": " +
                    provider.executable +
                    L"; input=" +
                    source.wstring();
                Logger::WriteMessage(startMessage.c_str());

                std::error_code copyError;
                std::filesystem::copy_file(
                    source,
                    input,
                    std::filesystem::copy_options::overwrite_existing,
                    copyError);
                if (copyError)
                {
                    const auto resultMessage =
                        L"Provider execution " +
                        std::to_wstring(index + 1) +
                        L"/" +
                        std::to_wstring(ARRAYSIZE(providers)) +
                        L" result: " +
                        provider.executable +
                        L"; status=not_launched; copy_error=" +
                        std::to_wstring(copyError.value()) +
                        L" (" +
                        widen_ascii(copyError.message()) +
                        L").";
                    Logger::WriteMessage(resultMessage.c_str());
                    failures += L"\n- " + resultMessage;
                    continue;
                }

                const auto start = std::chrono::steady_clock::now();
                thumbnail_provider::launch_result result;
                try
                {
                    result = thumbnail_provider::launch_in_job(
                        application,
                        L"\"" + input.wstring() + L"\" 256",
                        thumbnail_provider::default_timeout_ms);
                }
                catch (const std::exception& exception)
                {
                    const auto resultMessage =
                        L"Provider execution " +
                        std::to_wstring(index + 1) +
                        L"/" +
                        std::to_wstring(ARRAYSIZE(providers)) +
                        L" result: " +
                        provider.executable +
                        L"; status=exception; message=" +
                        widen_ascii(exception.what()) +
                        L".";
                    Logger::WriteMessage(resultMessage.c_str());
                    failures += L"\n- " + resultMessage;
                    continue;
                }
                catch (...)
                {
                    const auto resultMessage =
                        L"Provider execution " +
                        std::to_wstring(index + 1) +
                        L"/" +
                        std::to_wstring(ARRAYSIZE(providers)) +
                        L" result: " +
                        provider.executable +
                        L"; status=unknown_exception.";
                    Logger::WriteMessage(resultMessage.c_str());
                    failures += L"\n- " + resultMessage;
                    continue;
                }

                const auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(
                    std::chrono::steady_clock::now() - start);
                slowest = (std::max)(slowest, elapsed);

                auto output = input;
                output.replace_extension(L".bmp");
                std::error_code outputError;
                const auto outputExists =
                    std::filesystem::is_regular_file(output, outputError);
                const auto succeeded =
                    result.status == thumbnail_provider::launch_status::completed &&
                    result.exit_code == 0 &&
                    outputExists;

                const auto resultMessage =
                    L"Provider execution " +
                    std::to_wstring(index + 1) +
                    L"/" +
                    std::to_wstring(ARRAYSIZE(providers)) +
                    L" result: " +
                    provider.executable +
                    L"; status=" +
                    launch_status_name(result.status) +
                    L"; exit_code=" +
                    std::to_wstring(result.exit_code) +
                    L"; error=" +
                    std::to_wstring(result.error) +
                    L"; bitmap=" +
                    (outputExists ? L"present" : L"missing") +
                    L"; elapsed_ms=" +
                    std::to_wstring(elapsed.count()) +
                    L".";
                Logger::WriteMessage(resultMessage.c_str());

                if (succeeded)
                {
                    ++successes;
                }
                else
                {
                    failures += L"\n- " + resultMessage;
                }
            }

            const auto summary =
                L"Provider execution summary: attempted=" +
                std::to_wstring(executions) +
                L"/" +
                std::to_wstring(ARRAYSIZE(providers)) +
                L"; succeeded=" +
                std::to_wstring(successes) +
                L"/" +
                std::to_wstring(ARRAYSIZE(providers)) +
                L"; slowest_ms=" +
                std::to_wstring(slowest.count()) +
                L"; default_budget_ms=" +
                std::to_wstring(thumbnail_provider::default_timeout_ms) +
                L".";
            Logger::WriteMessage(summary.c_str());

            Assert::AreEqual<size_t>(ARRAYSIZE(providers), executions);
            if (!failures.empty())
            {
                const auto message =
                    L"One or more provider executions failed after all six were attempted:" +
                    failures;
                Assert::Fail(message.c_str());
            }

            Assert::AreEqual<size_t>(ARRAYSIZE(providers), successes);
            Assert::IsTrue(slowest.count() < thumbnail_provider::default_timeout_ms);
        }

        TEST_METHOD (LaunchInJob_Timeout_TerminatesParentAndDescendant)
        {
            nonthrowing_temp_directory tempDirectory;
            static std::atomic_uint64_t invocation{};
            const auto uniqueName =
                L"containment_cmd_" +
                std::to_wstring(GetCurrentProcessId()) +
                L"_" +
                std::to_wstring(GetTickCount64()) +
                L"_" +
                std::to_wstring(invocation.fetch_add(1)) +
                L".exe";
            const auto isolatedCmd = tempDirectory.path() / uniqueName;
            std::filesystem::copy_file(get_cmd_path(), isolatedCmd);

            const auto pingPath = get_ping_path();
            const auto arguments =
                L"/d /s /c \"\"" +
                pingPath.wstring() +
                L"\" -n 120 127.0.0.1 >nul\"";
            const auto start = std::chrono::steady_clock::now();
            auto launch = std::async(
                std::launch::async,
                [&] {
                    return thumbnail_provider::launch_in_job(isolatedCmd, arguments, 5'000);
                });

            std::optional<contained_processes> processes;
            TestHelpers::WaitFor(
                [&] {
                    processes = find_contained_processes(uniqueName, L"ping.exe");
                    return processes.has_value();
                },
                std::chrono::seconds{ 4 });

            wil::unique_handle parentProcess;
            wil::unique_handle descendantProcess;
            std::filesystem::path parentImage;
            std::filesystem::path descendantImage;
            if (processes)
            {
                parentProcess.reset(OpenProcess(
                    SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_TERMINATE,
                    FALSE,
                    processes->parent_id));
                descendantProcess.reset(OpenProcess(
                    SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_TERMINATE,
                    FALSE,
                    processes->descendant_id));
                if (parentProcess)
                {
                    parentImage = get_process_image(parentProcess.get());
                }

                if (descendantProcess)
                {
                    descendantImage = get_process_image(descendantProcess.get());
                }
            }

            const auto result = launch.get();
            const auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(
                std::chrono::steady_clock::now() - start);

            const auto parentObservation =
                observe_process_termination(parentProcess.get());
            const auto descendantObservation =
                observe_process_termination(descendantProcess.get());
            parentProcess.reset();
            descendantProcess.reset();
            const auto cleanupSucceeded = tempDirectory.cleanup();

            Assert::IsTrue(processes.has_value(), L"Did not observe the isolated cmd.exe -> ping.exe process tree before timeout.");
            Assert::IsTrue(!parentImage.empty(), L"Could not retain a handle to the isolated parent process.");
            Assert::IsTrue(!descendantImage.empty(), L"Could not retain a handle to the ping descendant.");
            Assert::IsTrue(
                _wcsicmp(parentImage.c_str(), isolatedCmd.c_str()) == 0,
                L"The observed parent did not originate from the unique test-owned cmd.exe copy.");
            Assert::IsTrue(
                _wcsicmp(descendantImage.filename().c_str(), L"ping.exe") == 0,
                L"The observed descendant was not ping.exe.");
            Assert::IsTrue(result.status == thumbnail_provider::launch_status::timed_out);
            Assert::AreEqual<DWORD>(ERROR_TIMEOUT, result.error);
            Assert::IsTrue(elapsed < std::chrono::seconds{ 10 });
            Assert::AreEqual<DWORD>(processes->parent_id, result.process_id);
            Assert::AreEqual<DWORD>(
                WAIT_OBJECT_0,
                parentObservation.initial_wait,
                L"The direct parent process survived the timeout and required exact-handle fallback termination.");
            Assert::AreEqual<DWORD>(
                WAIT_OBJECT_0,
                descendantObservation.initial_wait,
                L"The ping descendant survived the timeout and required exact-handle fallback termination.");
            Assert::IsFalse(parentObservation.fallback_termination_used);
            Assert::IsFalse(descendantObservation.fallback_termination_used);
            Assert::IsTrue(parentObservation.exit_code_read);
            Assert::IsTrue(descendantObservation.exit_code_read);
            Assert::AreNotEqual<DWORD>(STILL_ACTIVE, parentObservation.exit_code);
            Assert::AreNotEqual<DWORD>(STILL_ACTIVE, descendantObservation.exit_code);
            Assert::IsTrue(
                cleanupSucceeded,
                L"The bounded non-throwing cleanup could not remove the unique test-owned executable directory.");

            const auto message =
                L"Timeout containment and cleanup verified zero orphan/artifact: parent PID " +
                std::to_wstring(processes->parent_id) +
                L" and ping descendant PID " +
                std::to_wstring(processes->descendant_id) +
                L" both terminated after " +
                std::to_wstring(elapsed.count()) +
                L" ms; exact-handle fallback used=false; unique executable directory removed=true.";
            Logger::WriteMessage(message.c_str());
        }

        TEST_METHOD (LaunchInJob_NestedJob_TerminatesParentAndDescendant)
        {
            nonthrowing_temp_directory tempDirectory;
            static std::atomic_uint64_t invocation{};
            const auto uniqueName =
                L"nested_containment_cmd_" +
                std::to_wstring(GetCurrentProcessId()) +
                L"_" +
                std::to_wstring(GetTickCount64()) +
                L"_" +
                std::to_wstring(invocation.fetch_add(1)) +
                L".exe";
            const auto isolatedCmd = tempDirectory.path() / uniqueName;
            std::filesystem::copy_file(get_cmd_path(), isolatedCmd);

            const auto helper =
                get_test_module_path().parent_path() /
                L"ThumbnailProviderTestHelper.exe";
            const auto resultPath = tempDirectory.path() / L"nested-job-result.bin";
            const auto pingPath = get_ping_path();
            Assert::IsTrue(
                std::filesystem::is_regular_file(helper),
                L"The nested-job test helper was not built beside the test module.");

            wil::unique_handle outerJob{ CreateJobObjectW(nullptr, nullptr) };
            Assert::IsNotNull(outerJob.get());
            JOBOBJECT_EXTENDED_LIMIT_INFORMATION outerJobInformation{};
            outerJobInformation.BasicLimitInformation.LimitFlags =
                JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
            Assert::IsTrue(SetInformationJobObject(
                outerJob.get(),
                JobObjectExtendedLimitInformation,
                &outerJobInformation,
                sizeof(outerJobInformation)));

            auto commandLine =
                quote_argument(helper) +
                L" " +
                quote_argument(resultPath) +
                L" " +
                quote_argument(isolatedCmd) +
                L" " +
                quote_argument(pingPath);
            std::vector<wchar_t> mutableCommandLine(
                commandLine.begin(),
                commandLine.end());
            mutableCommandLine.push_back(L'\0');

            STARTUPINFOW startupInformation{ sizeof(startupInformation) };
            PROCESS_INFORMATION processInformation{};
            Assert::IsTrue(CreateProcessW(
                helper.c_str(),
                mutableCommandLine.data(),
                nullptr,
                nullptr,
                FALSE,
                CREATE_NO_WINDOW | CREATE_SUSPENDED,
                nullptr,
                nullptr,
                &startupInformation,
                &processInformation));

            wil::unique_handle helperProcess{ processInformation.hProcess };
            wil::unique_handle helperThread{ processInformation.hThread };
            Assert::IsTrue(
                AssignProcessToJobObject(outerJob.get(), helperProcess.get()),
                L"Could not assign the test helper to its outer job.");
            Assert::AreNotEqual<DWORD>(
                static_cast<DWORD>(-1),
                ResumeThread(helperThread.get()));

            std::optional<contained_processes> processes;
            TestHelpers::WaitFor(
                [&] {
                    processes = find_contained_processes(uniqueName, L"ping.exe");
                    return processes.has_value();
                },
                std::chrono::seconds{ 4 });

            wil::unique_handle parentProcess;
            wil::unique_handle descendantProcess;
            if (processes)
            {
                parentProcess.reset(OpenProcess(
                    SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_TERMINATE,
                    FALSE,
                    processes->parent_id));
                descendantProcess.reset(OpenProcess(
                    SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_TERMINATE,
                    FALSE,
                    processes->descendant_id));
            }

            Assert::AreEqual<DWORD>(
                WAIT_OBJECT_0,
                WaitForSingleObject(helperProcess.get(), 12'000),
                L"The nested-job test helper did not finish.");
            DWORD helperExitCode = STILL_ACTIVE;
            Assert::IsTrue(GetExitCodeProcess(helperProcess.get(), &helperExitCode));

            nested_job_probe_result probeResult;
            std::ifstream resultFile(resultPath, std::ios::binary);
            resultFile.read(
                reinterpret_cast<char*>(&probeResult),
                sizeof(probeResult));
            const auto resultReadSucceeded = resultFile.good();
            resultFile.close();

            const auto parentObservation =
                observe_process_termination(parentProcess.get());
            const auto descendantObservation =
                observe_process_termination(descendantProcess.get());
            parentProcess.reset();
            descendantProcess.reset();
            helperProcess.reset();
            helperThread.reset();
            outerJob.reset();
            const auto cleanupSucceeded = tempDirectory.cleanup();

            Assert::IsTrue(
                processes.has_value(),
                L"Did not observe the nested isolated cmd.exe -> ping.exe process tree.");
            Assert::IsTrue(resultReadSucceeded, L"The nested-job helper result was incomplete.");
            Assert::AreEqual<DWORD>(1, probeResult.version);
            Assert::AreEqual<DWORD>(TRUE, probeResult.process_in_outer_job);
            Assert::AreEqual<DWORD>(
                static_cast<DWORD>(thumbnail_provider::launch_status::timed_out),
                probeResult.launch_status);
            Assert::AreEqual<DWORD>(ERROR_TIMEOUT, probeResult.error);
            Assert::AreEqual<DWORD>(processes->parent_id, probeResult.process_id);
            Assert::AreEqual<DWORD>(ERROR_SUCCESS, helperExitCode);
            Assert::AreEqual<DWORD>(WAIT_OBJECT_0, parentObservation.initial_wait);
            Assert::AreEqual<DWORD>(WAIT_OBJECT_0, descendantObservation.initial_wait);
            Assert::IsFalse(parentObservation.fallback_termination_used);
            Assert::IsFalse(descendantObservation.fallback_termination_used);
            Assert::IsTrue(parentObservation.exit_code_read);
            Assert::IsTrue(descendantObservation.exit_code_read);
            Assert::AreNotEqual<DWORD>(STILL_ACTIVE, parentObservation.exit_code);
            Assert::AreNotEqual<DWORD>(STILL_ACTIVE, descendantObservation.exit_code);
            Assert::IsTrue(cleanupSucceeded);

            Logger::WriteMessage(
                L"Nested-job compatibility verified: helper remained in its outer job while "
                L"the inner timeout job terminated the isolated cmd.exe and ping.exe process tree.");
        }
    };
}
