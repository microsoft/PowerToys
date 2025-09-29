#include "pch.h"
#include "resource.h"
#include "RcResource.h"
#include <ProjectTelemetry.h>
#include <spdlog/sinks/base_sink.h>
#include <filesystem>

#include "../../src/common/logger/logger.h"
#include "../../src/common/utils/gpo.h"
#include "../../src/common/utils/MsiUtils.h"
#include "../../src/common/utils/modulesRegistry.h"
#include "../../src/common/updating/installer.h"
#include "../../src/common/version/version.h"
#include "../../src/common/Telemetry/EtwTrace/EtwTrace.h"
#include "../../src/common/utils/package.h"
#include "../../src/common/utils/clean_video_conference.h"

#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Management.Deployment.h>
#include <winrt/Windows.Security.Credentials.h>

#include <wtsapi32.h>
#include <processthreadsapi.h>
#include <UserEnv.h>
#include <winnt.h>

using namespace std;

HINSTANCE DLL_HANDLE = nullptr;

TRACELOGGING_DEFINE_PROVIDER(
    g_hProvider,
    "Microsoft.PowerToys",
    // {38e8889b-9731-53f5-e901-e8a7c1753074}
    (0x38e8889b, 0x9731, 0x53f5, 0xe9, 0x01, 0xe8, 0xa7, 0xc1, 0x75, 0x30, 0x74),
    TraceLoggingOptionProjectTelemetry());

const DWORD USERNAME_DOMAIN_LEN = DNLEN + UNLEN + 2; // Domain Name + '\' + User Name + '\0'
const DWORD USERNAME_LEN = UNLEN + 1;                // User Name + '\0'

static const wchar_t *POWERTOYS_EXE_COMPONENT = L"{A2C66D91-3485-4D00-B04D-91844E6B345B}";
static const wchar_t *POWERTOYS_UPGRADE_CODE = L"{42B84BF7-5FBF-473B-9C8B-049DC16F7708}";

constexpr inline const wchar_t *DataDiagnosticsRegKey = L"Software\\Classes\\PowerToys";
constexpr inline const wchar_t *DataDiagnosticsRegValueName = L"AllowDataDiagnostics";

#define TraceLoggingWriteWrapper(provider, eventName, ...)   \
    if (isDataDiagnosticEnabled())                           \
    {                                                        \
        trace.UpdateState(true);                             \
        TraceLoggingWrite(provider, eventName, __VA_ARGS__); \
        trace.Flush();                                       \
        trace.UpdateState(false);                            \
    }

static Shared::Trace::ETWTrace trace{L"PowerToys_Installer"};

inline bool isDataDiagnosticEnabled()
{
    HKEY key{};
    if (RegOpenKeyExW(HKEY_CURRENT_USER,
                      DataDiagnosticsRegKey,
                      0,
                      KEY_READ,
                      &key) != ERROR_SUCCESS)
    {
        return false;
    }

    DWORD isDataDiagnosticsEnabled = 0;
    DWORD size = sizeof(isDataDiagnosticsEnabled);

    if (RegGetValueW(
            HKEY_CURRENT_USER,
            DataDiagnosticsRegKey,
            DataDiagnosticsRegValueName,
            RRF_RT_REG_DWORD,
            nullptr,
            &isDataDiagnosticsEnabled,
            &size) != ERROR_SUCCESS)
    {
        RegCloseKey(key);
        return false;
    }
    RegCloseKey(key);

    return isDataDiagnosticsEnabled == 1;
}

HRESULT getInstallFolder(MSIHANDLE hInstall, std::wstring &installationDir)
{
    DWORD len = 0;
    wchar_t _[1];
    MsiGetPropertyW(hInstall, L"CustomActionData", _, &len);
    len += 1;
    installationDir.resize(len);
    HRESULT hr = MsiGetPropertyW(hInstall, L"CustomActionData", installationDir.data(), &len);
    if (installationDir.length())
    {
        installationDir.resize(installationDir.length() - 1);
    }
    ExitOnFailure(hr, "Failed to get INSTALLFOLDER property.");
LExit:
    return hr;
}

BOOL IsLocalSystem()
{
    HANDLE hToken;
    UCHAR bTokenUser[sizeof(TOKEN_USER) + 8 + 4 * SID_MAX_SUB_AUTHORITIES];
    PTOKEN_USER pTokenUser = (PTOKEN_USER)bTokenUser;
    ULONG cbTokenUser;
    SID_IDENTIFIER_AUTHORITY siaNT = SECURITY_NT_AUTHORITY;
    PSID pSystemSid;
    BOOL bSystem;

    // open process token
    if (!OpenProcessToken(GetCurrentProcess(),
                          TOKEN_QUERY,
                          &hToken))
        return FALSE;

    // retrieve user SID
    if (!GetTokenInformation(hToken, TokenUser, pTokenUser,
                             sizeof(bTokenUser), &cbTokenUser))
    {
        CloseHandle(hToken);
        return FALSE;
    }

    CloseHandle(hToken);

    // allocate LocalSystem well-known SID
    if (!AllocateAndInitializeSid(&siaNT, 1, SECURITY_LOCAL_SYSTEM_RID,
                                  0, 0, 0, 0, 0, 0, 0, &pSystemSid))
        return FALSE;

    // compare the user SID from the token with the LocalSystem SID
    bSystem = EqualSid(pTokenUser->User.Sid, pSystemSid);

    FreeSid(pSystemSid);

    return bSystem;
}

BOOL ImpersonateLoggedInUserAndDoSomething(std::function<bool(HANDLE userToken)> action)
{
    HRESULT hr = S_OK;
    HANDLE hUserToken = NULL;
    DWORD dwSessionId;
    ProcessIdToSessionId(GetCurrentProcessId(), &dwSessionId);
    auto rv = WTSQueryUserToken(dwSessionId, &hUserToken);

    if (rv == 0)
    {
        hr = E_ABORT;
        ExitOnFailure(hr, "Failed to query user token");
    }

    HANDLE hUserTokenDup;
    if (DuplicateTokenEx(hUserToken, TOKEN_ALL_ACCESS, NULL, SECURITY_IMPERSONATION_LEVEL::SecurityImpersonation, TOKEN_TYPE::TokenPrimary, &hUserTokenDup) == 0)
    {
        CloseHandle(hUserToken);
        CloseHandle(hUserTokenDup);
        hr = E_ABORT;
        ExitOnFailure(hr, "Failed to duplicate user token");
    }

    if (ImpersonateLoggedOnUser(hUserTokenDup))
    {
        if (!action(hUserTokenDup))
        {
            hr = E_ABORT;
            ExitOnFailure(hr, "Failed to execute action");
        }

        RevertToSelf();
        CloseHandle(hUserToken);
        CloseHandle(hUserTokenDup);
    }
    else
    {
        hr = E_ABORT;
        ExitOnFailure(hr, "Failed to duplicate user token");
    }

LExit:
    return SUCCEEDED(hr);
}

static std::filesystem::path GetUserPowerShellModulesPath()
{
    PWSTR myDocumentsBlockPtr;

    if (SUCCEEDED(SHGetKnownFolderPath(FOLDERID_Documents, 0, NULL, &myDocumentsBlockPtr)))
    {
        const std::wstring myDocuments{myDocumentsBlockPtr};
        CoTaskMemFree(myDocumentsBlockPtr);
        return std::filesystem::path(myDocuments) / "PowerShell" / "Modules";
    }
    else
    {
        CoTaskMemFree(myDocumentsBlockPtr);
        return {};
    }
}

UINT __stdcall LaunchPowerToysCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    std::wstring installationFolder, path, args;
    std::wstring commandLine;

    hr = WcaInitialize(hInstall, "LaunchPowerToys");
    ExitOnFailure(hr, "Failed to initialize");
    hr = getInstallFolder(hInstall, installationFolder);
    ExitOnFailure(hr, "Failed to get installFolder.");

    path = installationFolder;
    path += L"\\PowerToys.exe";

    args = L"--dont-elevate";

    commandLine = L"\"" + path + L"\" ";
    commandLine += args;

    BOOL isSystemUser = IsLocalSystem();

    if (isSystemUser)
    {

        auto action = [&commandLine](HANDLE userToken)
        {
            STARTUPINFO startupInfo = { 0 };
            startupInfo.cb = sizeof(STARTUPINFO);
            startupInfo.wShowWindow = SW_SHOWNORMAL;
            PROCESS_INFORMATION processInformation;

            PVOID lpEnvironment = NULL;
            CreateEnvironmentBlock(&lpEnvironment, userToken, FALSE);

            CreateProcessAsUser(
                userToken,
                NULL,
                commandLine.data(),
                NULL,
                NULL,
                FALSE,
                CREATE_DEFAULT_ERROR_MODE | CREATE_UNICODE_ENVIRONMENT,
                lpEnvironment,
                NULL,
                &startupInfo,
                &processInformation);

            if (!CloseHandle(processInformation.hProcess))
            {
                return false;
            }
            if (!CloseHandle(processInformation.hThread))
            {
                return false;
            }

            return true;
        };

        if (!ImpersonateLoggedInUserAndDoSomething(action))
        {
            hr = E_ABORT;
            ExitOnFailure(hr, "ImpersonateLoggedInUserAndDoSomething failed");
        }
    }
    else
    {
        STARTUPINFO startupInfo = { 0 };
        startupInfo.cb = sizeof(STARTUPINFO);
        startupInfo.wShowWindow = SW_SHOWNORMAL;

        PROCESS_INFORMATION processInformation;

        // Start the resizer
        CreateProcess(
            NULL,
            commandLine.data(),
            NULL,
            NULL,
            TRUE,
            0,
            NULL,
            NULL,
            &startupInfo,
            &processInformation);

        if (!CloseHandle(processInformation.hProcess))
        {
            hr = E_ABORT;
            ExitOnFailure(hr, "Failed to close process handle");
        }
        if (!CloseHandle(processInformation.hThread))
        {
            hr = E_ABORT;
            ExitOnFailure(hr, "Failed to close thread handle");
        }
    }

LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

UINT __stdcall CheckGPOCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;

    hr = WcaInitialize(hInstall, "CheckGPOCA");
    ExitOnFailure(hr, "Failed to initialize");

    LPWSTR currentScope = nullptr;
    hr = WcaGetProperty(L"InstallScope", &currentScope);

    if (std::wstring{currentScope} == L"perUser")
    {
        if (powertoys_gpo::getDisablePerUserInstallationValue() == powertoys_gpo::gpo_rule_configured_enabled)
        {
            PMSIHANDLE hRecord = MsiCreateRecord(0);
            MsiRecordSetString(hRecord, 0, TEXT("The system administrator has disabled per-user installation."));
            MsiProcessMessage(hInstall, static_cast<INSTALLMESSAGE>(INSTALLMESSAGE_ERROR + MB_OK), hRecord);
            hr = E_ABORT;
        }
    }

LExit:
    UINT er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

// We've deprecated Video Conference Mute. This Custom Action cleans up any stray registry entry for the driver dll.
UINT __stdcall CleanVideoConferenceRegistryCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    hr = WcaInitialize(hInstall, "CleanVideoConferenceRegistry");
    ExitOnFailure(hr, "Failed to initialize");
    clean_video_conference();
LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

UINT __stdcall ApplyModulesRegistryChangeSetsCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    std::wstring installationFolder;
    bool failedToApply = false;

    hr = WcaInitialize(hInstall, "ApplyModulesRegistryChangeSets");
    ExitOnFailure(hr, "Failed to initialize");
    hr = getInstallFolder(hInstall, installationFolder);
    ExitOnFailure(hr, "Failed to get installFolder.");

    for (const auto &changeSet : getAllOnByDefaultModulesChangeSets(installationFolder))
    {
        if (!changeSet.apply())
        {
            Logger::error(L"Couldn't apply registry changeSet");
            failedToApply = true;
        }
    }

    if (!failedToApply)
    {
        Logger::info(L"All registry changeSets applied successfully");
    }
LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

UINT __stdcall UnApplyModulesRegistryChangeSetsCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    std::wstring installationFolder;

    hr = WcaInitialize(hInstall, "UndoModulesRegistryChangeSets"); // original func name is too long
    ExitOnFailure(hr, "Failed to initialize");
    hr = getInstallFolder(hInstall, installationFolder);
    ExitOnFailure(hr, "Failed to get installFolder.");
    for (const auto &changeSet : getAllModulesChangeSets(installationFolder))
    {
        changeSet.unApply();
    }

    SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, NULL, NULL);

    ExitOnFailure(hr, "Failed to extract msix");

LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

const wchar_t *DSC_CONFIGURE_PSD1_NAME = L"Microsoft.PowerToys.Configure.psd1";
const wchar_t *DSC_CONFIGURE_PSM1_NAME = L"Microsoft.PowerToys.Configure.psm1";

UINT __stdcall InstallDSCModuleCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    std::wstring installationFolder;

    hr = WcaInitialize(hInstall, "InstallDSCModuleCA");
    ExitOnFailure(hr, "Failed to initialize");

    hr = getInstallFolder(hInstall, installationFolder);
    ExitOnFailure(hr, "Failed to get installFolder.");

    {
        const auto baseModulesPath = GetUserPowerShellModulesPath();
        if (baseModulesPath.empty())
        {
            hr = E_FAIL;
            ExitOnFailure(hr, "Unable to determine Powershell modules path");
        }

        const auto modulesPath = baseModulesPath / L"Microsoft.PowerToys.Configure" / (get_product_version(false) + L".0");

        std::error_code errorCode;
        std::filesystem::create_directories(modulesPath, errorCode);
        if (errorCode)
        {
            hr = E_FAIL;
            ExitOnFailure(hr, "Unable to create Powershell modules folder");
        }

        for (const auto *filename : {DSC_CONFIGURE_PSD1_NAME, DSC_CONFIGURE_PSM1_NAME})
        {
            std::filesystem::copy_file(std::filesystem::path(installationFolder) / "DSCModules" / filename, modulesPath / filename, std::filesystem::copy_options::overwrite_existing, errorCode);

            if (errorCode)
            {
                hr = E_FAIL;
                ExitOnFailure(hr, "Unable to copy Powershell modules file");
            }
        }
    }

LExit:
    if (SUCCEEDED(hr))
    {
        er = ERROR_SUCCESS;
        Logger::info(L"DSC module was installed!");
    }
    else
    {
        er = ERROR_INSTALL_FAILURE;
        Logger::error(L"Couldn't install DSC module!");
    }

    return WcaFinalize(er);
}

UINT __stdcall UninstallDSCModuleCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;

    hr = WcaInitialize(hInstall, "UninstallDSCModuleCA");
    ExitOnFailure(hr, "Failed to initialize");

    {
        const auto baseModulesPath = GetUserPowerShellModulesPath();
        if (baseModulesPath.empty())
        {
            hr = E_FAIL;
            ExitOnFailure(hr, "Unable to determine Powershell modules path");
        }

        const auto powerToysModulePath = baseModulesPath / L"Microsoft.PowerToys.Configure";
        const auto versionedModulePath = powerToysModulePath / (get_product_version(false) + L".0");

        std::error_code errorCode;

        for (const auto *filename : {DSC_CONFIGURE_PSD1_NAME, DSC_CONFIGURE_PSM1_NAME})
        {
            std::filesystem::remove(versionedModulePath / filename, errorCode);

            if (errorCode)
            {
                hr = E_FAIL;
                ExitOnFailure(hr, "Unable to delete DSC file");
            }
        }

        for (const auto *modulePath : {&versionedModulePath, &powerToysModulePath})
        {
            std::filesystem::remove(*modulePath, errorCode);

            if (errorCode)
            {
                hr = E_FAIL;
                ExitOnFailure(hr, "Unable to delete DSC folder");
            }
        }
    }

LExit:
    if (SUCCEEDED(hr))
    {
        er = ERROR_SUCCESS;
        Logger::info(L"DSC module was uninstalled!");
    }
    else
    {
        er = ERROR_INSTALL_FAILURE;
        Logger::error(L"Couldn't uninstall DSC module!");
    }

    return WcaFinalize(er);
}

UINT __stdcall InstallEmbeddedMSIXCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    hr = WcaInitialize(hInstall, "InstallEmbeddedMSIXCA");
    ExitOnFailure(hr, "Failed to initialize");

    if (auto msix = RcResource::create(IDR_BIN_MSIX_HELLO_PACKAGE, L"BIN", DLL_HANDLE))
    {
        Logger::info(L"Extracted MSIX");
        // TODO: Use to activate embedded MSIX
        const auto msix_path = std::filesystem::temp_directory_path() / "hello_package.msix";
        if (!msix->saveAsFile(msix_path))
        {
            ExitOnFailure(hr, "Failed to save msix");
        }
        Logger::info(L"Saved MSIX");
        using namespace winrt::Windows::Management::Deployment;
        using namespace winrt::Windows::Foundation;

        Uri msix_uri{msix_path.wstring()};
        PackageManager pm;
        auto result = pm.AddPackageAsync(msix_uri, nullptr, DeploymentOptions::None).get();
        if (!result)
        {
            ExitOnFailure(hr, "Failed to AddPackage");
        }

        Logger::info(L"MSIX[s] were installed!");
    }
    else
    {
        ExitOnFailure(hr, "Failed to extract msix");
    }

LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

UINT __stdcall UninstallEmbeddedMSIXCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    using namespace winrt::Windows::Management::Deployment;
    using namespace winrt::Windows::Foundation;
    // TODO: This must be replaced with the actual publisher and package name
    const wchar_t package_name[] = L"46b35c25-b593-48d5-aeb1-d3e9c3b796e9";
    const wchar_t publisher[] = L"CN=yuyoyuppe";
    PackageManager pm;

    hr = WcaInitialize(hInstall, "UninstallEmbeddedMSIXCA");
    ExitOnFailure(hr, "Failed to initialize");

    for (const auto &p : pm.FindPackagesForUser({}, package_name, publisher))
    {
        auto result = pm.RemovePackageAsync(p.Id().FullName()).get();
        if (result)
        {
            Logger::info(L"MSIX was uninstalled!");
        }
        else
        {
            Logger::error(L"Couldn't uninstall MSIX!");
        }
    }

LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

UINT __stdcall RemoveWindowsServiceByName(std::wstring serviceName)
{
    SC_HANDLE hSCManager = OpenSCManager(NULL, NULL, SC_MANAGER_CONNECT);

    if (!hSCManager)
    {
        return ERROR_INSTALL_FAILURE;
    }

    SC_HANDLE hService = OpenService(hSCManager, serviceName.c_str(), SERVICE_STOP | DELETE);
    if (!hService)
    {
        CloseServiceHandle(hSCManager);
        return ERROR_INSTALL_FAILURE;
    }

    SERVICE_STATUS ss;
    if (ControlService(hService, SERVICE_CONTROL_STOP, &ss))
    {
        Sleep(1000);
        while (QueryServiceStatus(hService, &ss))
        {
            if (ss.dwCurrentState == SERVICE_STOP_PENDING)
            {
                Sleep(1000);
            }
            else
            {
                break;
            }
        }
    }

    BOOL deleteResult = DeleteService(hService);
    CloseServiceHandle(hService);
    CloseServiceHandle(hSCManager);

    if (!deleteResult)
    {
        return ERROR_INSTALL_FAILURE;
    }

    return ERROR_SUCCESS;
}

UINT __stdcall UnsetAdvancedPasteAPIKeyCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;

    try
    {
        winrt::Windows::Security::Credentials::PasswordVault vault;
        winrt::Windows::Security::Credentials::PasswordCredential cred;

        hr = WcaInitialize(hInstall, "UnsetAdvancedPasteAPIKey");
        ExitOnFailure(hr, "Failed to initialize");

        cred = vault.Retrieve(L"https://platform.openai.com/api-keys", L"PowerToys_AdvancedPaste_OpenAIKey");
        vault.Remove(cred);
    }
    catch (...)
    {
    }

LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

UINT __stdcall UninstallCommandNotFoundModuleCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    std::wstring installationFolder;
    std::string command;

    hr = WcaInitialize(hInstall, "UninstallCommandNotFoundModule");
    ExitOnFailure(hr, "Failed to initialize");

    hr = getInstallFolder(hInstall, installationFolder);
    ExitOnFailure(hr, "Failed to get installFolder.");

#ifdef _M_ARM64
    command = "powershell.exe";
    command += " ";
    command += "-NoProfile -NonInteractive -NoLogo -WindowStyle Hidden -ExecutionPolicy Unrestricted";
    command += " -Command ";
    command += "\"[Environment]::SetEnvironmentVariable('PATH', [Environment]::GetEnvironmentVariable('PATH', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('PATH', 'User'), 'Process');";
    command += "pwsh.exe -NoProfile -NonInteractive -NoLogo -WindowStyle Hidden -ExecutionPolicy Unrestricted -File '" + winrt::to_string(installationFolder) + "\\WinUI3Apps\\Assets\\Settings\\Scripts\\DisableModule.ps1" + "'\"";
#else
    command = "pwsh.exe";
    command += " ";
    command += "-NoProfile -NonInteractive -NoLogo -WindowStyle Hidden -ExecutionPolicy Unrestricted -File \"" + winrt::to_string(installationFolder) + "\\WinUI3Apps\\Assets\\Settings\\Scripts\\DisableModule.ps1" + "\"";
#endif

    system(command.c_str());

LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

UINT __stdcall UpgradeCommandNotFoundModuleCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    std::wstring installationFolder;
    std::string command;

    hr = WcaInitialize(hInstall, "UpgradeCommandNotFoundModule");
    ExitOnFailure(hr, "Failed to initialize");

    hr = getInstallFolder(hInstall, installationFolder);
    ExitOnFailure(hr, "Failed to get installFolder.");

    command = "pwsh.exe";
    command += " ";
    command += "-NoProfile -NonInteractive -NoLogo -WindowStyle Hidden -ExecutionPolicy Unrestricted -File \"" + winrt::to_string(installationFolder) + "\\WinUI3Apps\\Assets\\Settings\\Scripts\\UpgradeModule.ps1" + "\"";

    system(command.c_str());

LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

UINT __stdcall UninstallServicesCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    hr = WcaInitialize(hInstall, "UninstallServicesCA");

    ExitOnFailure(hr, "Failed to initialize");

    hr = RemoveWindowsServiceByName(L"PowerToys.MWB.Service");

LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

// Removes all Scheduled Tasks in the PowerToys folder and deletes the folder afterwards.
// Based on the Task Scheduler Displaying Task Names and State example:
// https://learn.microsoft.com/windows/desktop/TaskSchd/displaying-task-names-and-state--c---/
UINT __stdcall RemoveScheduledTasksCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;

    ITaskService *pService = nullptr;
    ITaskFolder *pTaskFolder = nullptr;
    IRegisteredTaskCollection *pTaskCollection = nullptr;
    ITaskFolder *pRootFolder = nullptr;
    LONG numTasks = 0;

    hr = WcaInitialize(hInstall, "RemoveScheduledTasksCA");
    ExitOnFailure(hr, "Failed to initialize");

    Logger::info(L"RemoveScheduledTasksCA Initialized.");

    // COM and Security Initialization is expected to have been done by the MSI.
    // It couldn't be done in the DLL, anyway.
    // ------------------------------------------------------
    // Create an instance of the Task Service.
    hr = CoCreateInstance(CLSID_TaskScheduler,
                          nullptr,
                          CLSCTX_INPROC_SERVER,
                          IID_ITaskService,
                          reinterpret_cast<void **>(&pService));
    ExitOnFailure(hr, "Failed to create an instance of ITaskService: %x", hr);

    // Connect to the task service.
    hr = pService->Connect(_variant_t(), _variant_t(), _variant_t(), _variant_t());
    ExitOnFailure(hr, "ITaskService::Connect failed: %x", hr);

    // ------------------------------------------------------
    // Get the PowerToys task folder.
    hr = pService->GetFolder(_bstr_t(L"\\PowerToys"), &pTaskFolder);
    if (FAILED(hr))
    {
        // Folder doesn't exist. No need to delete anything.
        Logger::info(L"The PowerToys scheduled task folder wasn't found. Nothing to delete.");
        hr = S_OK;
        ExitFunction();
    }

    // -------------------------------------------------------
    // Get the registered tasks in the folder.
    hr = pTaskFolder->GetTasks(TASK_ENUM_HIDDEN, &pTaskCollection);
    ExitOnFailure(hr, "Cannot get the registered tasks: %x", hr);

    hr = pTaskCollection->get_Count(&numTasks);
    for (LONG i = 0; i < numTasks; i++)
    {
        // Delete all the tasks found.
        // If some tasks can't be deleted, the folder won't be deleted later and the user will still be notified.
        IRegisteredTask *pRegisteredTask = nullptr;
        hr = pTaskCollection->get_Item(_variant_t(i + 1), &pRegisteredTask);
        if (SUCCEEDED(hr))
        {
            BSTR taskName = nullptr;
            hr = pRegisteredTask->get_Name(&taskName);
            if (SUCCEEDED(hr))
            {
                hr = pTaskFolder->DeleteTask(taskName, 0);
                if (FAILED(hr))
                {
                    Logger::error(L"Cannot delete the {} task: {}", taskName, hr);
                }
                SysFreeString(taskName);
            }
            else
            {
                Logger::error(L"Cannot get the registered task name: {}", hr);
            }
            pRegisteredTask->Release();
        }
        else
        {
            Logger::error(L"Cannot get the registered task item at index={}: {}", i + 1, hr);
        }
    }

    // ------------------------------------------------------
    // Get the pointer to the root task folder and delete the PowerToys subfolder.
    hr = pService->GetFolder(_bstr_t(L"\\"), &pRootFolder);
    ExitOnFailure(hr, "Cannot get Root Folder pointer: %x", hr);
    hr = pRootFolder->DeleteFolder(_bstr_t(L"PowerToys"), 0);
    pRootFolder->Release();
    ExitOnFailure(hr, "Cannot delete the PowerToys folder: %x", hr);

    Logger::info(L"Deleted the PowerToys Task Scheduler folder.");

LExit:
    if (pService)
    {
        pService->Release();
    }
    if (pTaskFolder)
    {
        pTaskFolder->Release();
    }
    if (pTaskCollection)
    {
        pTaskCollection->Release();
    }

    if (!SUCCEEDED(hr))
    {
        PMSIHANDLE hRecord = MsiCreateRecord(0);
        MsiRecordSetString(hRecord, 0, TEXT("Failed to remove the PowerToys folder from the scheduled task. These can be removed manually later."));
        MsiProcessMessage(hInstall, static_cast<INSTALLMESSAGE>(INSTALLMESSAGE_WARNING + MB_OK), hRecord);
    }

    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

UINT __stdcall TelemetryLogInstallSuccessCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;

    hr = WcaInitialize(hInstall, "TelemetryLogInstallSuccessCA");
    ExitOnFailure(hr, "Failed to initialize");

    TraceLoggingWriteWrapper(
        g_hProvider,
        "Install_Success",
        TraceLoggingWideString(get_product_version().c_str(), "Version"),
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));

LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

UINT __stdcall TelemetryLogInstallCancelCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;

    hr = WcaInitialize(hInstall, "TelemetryLogInstallCancelCA");
    ExitOnFailure(hr, "Failed to initialize");

    TraceLoggingWriteWrapper(
        g_hProvider,
        "Install_Cancel",
        TraceLoggingWideString(get_product_version().c_str(), "Version"),
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));

LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

UINT __stdcall TelemetryLogInstallFailCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;

    hr = WcaInitialize(hInstall, "TelemetryLogInstallFailCA");
    ExitOnFailure(hr, "Failed to initialize");

    TraceLoggingWriteWrapper(
        g_hProvider,
        "Install_Fail",
        TraceLoggingWideString(get_product_version().c_str(), "Version"),
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));

LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

UINT __stdcall TelemetryLogUninstallSuccessCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;

    hr = WcaInitialize(hInstall, "TelemetryLogUninstallSuccessCA");
    ExitOnFailure(hr, "Failed to initialize");

    TraceLoggingWriteWrapper(
        g_hProvider,
        "UnInstall_Success",
        TraceLoggingWideString(get_product_version().c_str(), "Version"),
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));

LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

UINT __stdcall TelemetryLogUninstallCancelCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;

    hr = WcaInitialize(hInstall, "TelemetryLogUninstallCancelCA");
    ExitOnFailure(hr, "Failed to initialize");

    TraceLoggingWriteWrapper(
        g_hProvider,
        "UnInstall_Cancel",
        TraceLoggingWideString(get_product_version().c_str(), "Version"),
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));

LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

UINT __stdcall TelemetryLogUninstallFailCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;

    hr = WcaInitialize(hInstall, "TelemetryLogUninstallFailCA");
    ExitOnFailure(hr, "Failed to initialize");

    TraceLoggingWriteWrapper(
        g_hProvider,
        "UnInstall_Fail",
        TraceLoggingWideString(get_product_version().c_str(), "Version"),
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));

LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

UINT __stdcall TelemetryLogRepairCancelCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;

    hr = WcaInitialize(hInstall, "TelemetryLogRepairCancelCA");
    ExitOnFailure(hr, "Failed to initialize");

    TraceLoggingWriteWrapper(
        g_hProvider,
        "Repair_Cancel",
        TraceLoggingWideString(get_product_version().c_str(), "Version"),
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));

LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

UINT __stdcall TelemetryLogRepairFailCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;

    hr = WcaInitialize(hInstall, "TelemetryLogRepairFailCA");
    ExitOnFailure(hr, "Failed to initialize");

    TraceLoggingWriteWrapper(
        g_hProvider,
        "Repair_Fail",
        TraceLoggingWideString(get_product_version().c_str(), "Version"),
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));

LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

UINT __stdcall DetectPrevInstallPathCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    hr = WcaInitialize(hInstall, "DetectPrevInstallPathCA");
    MsiSetPropertyW(hInstall, L"PREVIOUSINSTALLFOLDER", L"");

    LPWSTR currentScope = nullptr;
    hr = WcaGetProperty(L"InstallScope", &currentScope);

    try
    {
        if (auto install_path = GetMsiPackageInstalledPath(std::wstring{currentScope} == L"perUser"))
        {
            MsiSetPropertyW(hInstall, L"PREVIOUSINSTALLFOLDER", install_path->data());
        }
    }
    catch (...)
    {
    }
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

UINT __stdcall InstallCmdPalPackageCA(MSIHANDLE hInstall)
{
    using namespace winrt::Windows::Foundation;
    using namespace winrt::Windows::Management::Deployment;

    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    std::wstring installationFolder;

    hr = WcaInitialize(hInstall, "InstallCmdPalPackage");
    hr = getInstallFolder(hInstall, installationFolder);

    try
    {
        auto msix = package::FindMsixFile(installationFolder + L"\\WinUI3Apps\\CmdPal\\", false);
        auto dependencies = package::FindMsixFile(installationFolder + L"\\WinUI3Apps\\CmdPal\\Dependencies\\", true);

        if (!msix.empty())
        {
            auto msixPath = msix[0];

            if (!package::RegisterPackage(msixPath, dependencies))
            {
                Logger::error(L"Failed to install CmdPal package");
                er = ERROR_INSTALL_FAILURE;
            }
        }
    }
    catch (std::exception &e)
    {
        std::string errorMessage{"Exception thrown while trying to install CmdPal package: "};
        errorMessage += e.what();
        Logger::error(errorMessage);

        er = ERROR_INSTALL_FAILURE;
    }

    er = er == ERROR_SUCCESS ? (SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE) : er;
    return WcaFinalize(er);
}

UINT __stdcall UnRegisterCmdPalPackageCA(MSIHANDLE hInstall)
{
    using namespace winrt::Windows::Foundation;
    using namespace winrt::Windows::Management::Deployment;

    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;

    hr = WcaInitialize(hInstall, "UnRegisterCmdPalPackageCA");

    try
    {
        // Packages to unregister
        std::wstring packageToRemoveDisplayName {L"Microsoft.CommandPalette"};

        if (!package::UnRegisterPackage(packageToRemoveDisplayName))
        {
            Logger::error(L"Failed to unregister package: " + packageToRemoveDisplayName);
            er = ERROR_INSTALL_FAILURE;
        }
    }
    catch (std::exception &e)
    {
        std::string errorMessage{"Exception thrown while trying to unregister the CmdPal package: "};
        errorMessage += e.what();
        Logger::error(errorMessage);

        er = ERROR_INSTALL_FAILURE;
    }

    er = er == ERROR_SUCCESS ? (SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE) : er;
    return WcaFinalize(er);
}


UINT __stdcall UnRegisterContextMenuPackagesCA(MSIHANDLE hInstall)
{
    using namespace winrt::Windows::Foundation;
    using namespace winrt::Windows::Management::Deployment;

    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;

    hr = WcaInitialize(hInstall, "UnRegisterContextMenuPackagesCA"); // original func name is too long

    try
    {
        // Packages to unregister
        const std::vector<std::wstring> packagesToRemoveDisplayName{{L"PowerRenameContextMenu"}, {L"ImageResizerContextMenu"}, {L"FileLocksmithContextMenu"}, {L"NewPlusContextMenu"}};

        for (auto const &package : packagesToRemoveDisplayName)
        {
            if (!package::UnRegisterPackage(package))
            {
                Logger::error(L"Failed to unregister package: " + package);
                er = ERROR_INSTALL_FAILURE;
            }
        }
    }
    catch (std::exception &e)
    {
        std::string errorMessage{"Exception thrown while trying to unregister sparse packages: "};
        errorMessage += e.what();
        Logger::error(errorMessage);

        er = ERROR_INSTALL_FAILURE;
    }

    er = er == ERROR_SUCCESS ? (SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE) : er;
    return WcaFinalize(er);
}


UINT __stdcall CleanImageResizerRuntimeRegistryCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    hr = WcaInitialize(hInstall, "CleanImageResizerRuntimeRegistryCA");

    try
    {
        const wchar_t* CLSID_STR = L"{51B4D7E5-7568-4234-B4BB-47FB3C016A69}";
        const wchar_t* exts[] = { L".bmp", L".dib", L".gif", L".jfif", L".jpe", L".jpeg", L".jpg", L".jxr", L".png", L".rle", L".tif", L".tiff", L".wdp" };

        auto deleteKeyRecursive = [](HKEY root, const std::wstring &path) {
            RegDeleteTreeW(root, path.c_str());
        };

        // InprocServer32 chain root CLSID
        deleteKeyRecursive(HKEY_CURRENT_USER, L"Software\\Classes\\CLSID\\" + std::wstring(CLSID_STR));
        // DragDrop handler
        deleteKeyRecursive(HKEY_CURRENT_USER, L"Software\\Classes\\Directory\\ShellEx\\DragDropHandlers\\ImageResizer");
        // Extensions
        for (auto ext : exts)
        {
            deleteKeyRecursive(HKEY_CURRENT_USER, L"Software\\Classes\\SystemFileAssociations\\" + std::wstring(ext) + L"\\ShellEx\\ContextMenuHandlers\\ImageResizer");
        }
        // Sentinel
        RegDeleteTreeW(HKEY_CURRENT_USER, L"Software\\Microsoft\\PowerToys\\ImageResizer");
    }
    catch (...)
    {
        er = ERROR_INSTALL_FAILURE;
    }

    er = er == ERROR_SUCCESS ? (SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE) : er;
    return WcaFinalize(er);
}

UINT __stdcall CleanFileLocksmithRuntimeRegistryCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    hr = WcaInitialize(hInstall, "CleanFileLocksmithRuntimeRegistryCA");
    try
    {
        const wchar_t* CLSID_STR = L"{84D68575-E186-46AD-B0CB-BAEB45EE29C0}";
        auto deleteKeyRecursive = [](HKEY root, const std::wstring& path) {
            RegDeleteTreeW(root, path.c_str());
        };
        deleteKeyRecursive(HKEY_CURRENT_USER, L"Software\\Classes\\CLSID\\" + std::wstring(CLSID_STR));
        deleteKeyRecursive(HKEY_CURRENT_USER, L"Software\\Classes\\AllFileSystemObjects\\ShellEx\\ContextMenuHandlers\\FileLocksmithExt");
        deleteKeyRecursive(HKEY_CURRENT_USER, L"Software\\Classes\\Drive\\ShellEx\\ContextMenuHandlers\\FileLocksmithExt");
        RegDeleteTreeW(HKEY_CURRENT_USER, L"Software\\Microsoft\\PowerToys\\FileLocksmith");
    }
    catch (...)
    {
        er = ERROR_INSTALL_FAILURE;
    }
    er = er == ERROR_SUCCESS ? (SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE) : er;
    return WcaFinalize(er);
}

UINT __stdcall CleanPowerRenameRuntimeRegistryCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    hr = WcaInitialize(hInstall, "CleanPowerRenameRuntimeRegistryCA");
    try
    {
        const wchar_t* CLSID_STR = L"{0440049F-D1DC-4E46-B27B-98393D79486B}";
        auto deleteKeyRecursive = [](HKEY root, const std::wstring& path) {
            RegDeleteTreeW(root, path.c_str());
        };
        deleteKeyRecursive(HKEY_CURRENT_USER, L"Software\\Classes\\CLSID\\" + std::wstring(CLSID_STR));
        deleteKeyRecursive(HKEY_CURRENT_USER, L"Software\\Classes\\AllFileSystemObjects\\ShellEx\\ContextMenuHandlers\\PowerRenameExt");
        deleteKeyRecursive(HKEY_CURRENT_USER, L"Software\\Classes\\Directory\\background\\ShellEx\\ContextMenuHandlers\\PowerRenameExt");
        RegDeleteTreeW(HKEY_CURRENT_USER, L"Software\\Microsoft\\PowerToys\\PowerRename");
    }
    catch (...)
    {
        er = ERROR_INSTALL_FAILURE;
    }
    er = er == ERROR_SUCCESS ? (SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE) : er;
    return WcaFinalize(er);
}

UINT __stdcall CleanNewPlusRuntimeRegistryCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    hr = WcaInitialize(hInstall, "CleanNewPlusRuntimeRegistryCA");
    try
    {
        const wchar_t* CLSID_STR = L"{FF90D477-E32A-4BE8-8CC5-A502A97F5401}";
        auto deleteKeyRecursive = [](HKEY root, const std::wstring& path) {
            RegDeleteTreeW(root, path.c_str());
        };
        deleteKeyRecursive(HKEY_CURRENT_USER, L"Software\\Classes\\CLSID\\" + std::wstring(CLSID_STR));
        deleteKeyRecursive(HKEY_CURRENT_USER, L"Software\\Classes\\Directory\\background\\ShellEx\\ContextMenuHandlers\\NewPlusShellExtensionWin10");
        RegDeleteTreeW(HKEY_CURRENT_USER, L"Software\\Microsoft\\PowerToys\\NewPlus");
    }
    catch (...)
    {
        er = ERROR_INSTALL_FAILURE;
    }
    er = er == ERROR_SUCCESS ? (SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE) : er;
    return WcaFinalize(er);
}

UINT __stdcall TerminateProcessesCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    hr = WcaInitialize(hInstall, "TerminateProcessesCA");

    std::vector<DWORD> processes;
    const size_t maxProcesses = 4096;
    DWORD bytes = maxProcesses * sizeof(processes[0]);
    processes.resize(maxProcesses);

    if (!EnumProcesses(processes.data(), bytes, &bytes))
    {
        return 1;
    }
    processes.resize(bytes / sizeof(processes[0]));

    std::array<std::wstring_view, 42> processesToTerminate = {
        L"PowerToys.PowerLauncher.exe",
        L"PowerToys.Settings.exe",
        L"PowerToys.AdvancedPaste.exe",
        L"PowerToys.Awake.exe",
        L"PowerToys.FancyZones.exe",
        L"PowerToys.FancyZonesEditor.exe",
        L"PowerToys.FileLocksmithUI.exe",
        L"PowerToys.MouseJumpUI.exe",
        L"PowerToys.ColorPickerUI.exe",
        L"PowerToys.AlwaysOnTop.exe",
        L"PowerToys.RegistryPreview.exe",
        L"PowerToys.Hosts.exe",
        L"PowerToys.PowerRename.exe",
        L"PowerToys.ImageResizer.exe",
        L"PowerToys.LightSwitchService.exe",
        L"PowerToys.GcodeThumbnailProvider.exe",
        L"PowerToys.BgcodeThumbnailProvider.exe",
        L"PowerToys.PdfThumbnailProvider.exe",
        L"PowerToys.MonacoPreviewHandler.exe",
        L"PowerToys.MarkdownPreviewHandler.exe",
        L"PowerToys.StlThumbnailProvider.exe",
        L"PowerToys.SvgThumbnailProvider.exe",
        L"PowerToys.GcodePreviewHandler.exe",
        L"PowerToys.BgcodePreviewHandler.exe",
        L"PowerToys.QoiPreviewHandler.exe",
        L"PowerToys.PdfPreviewHandler.exe",
        L"PowerToys.QoiThumbnailProvider.exe",
        L"PowerToys.SvgPreviewHandler.exe",
        L"PowerToys.Peek.UI.exe",
        L"PowerToys.MouseWithoutBorders.exe",
        L"PowerToys.MouseWithoutBordersHelper.exe",
        L"PowerToys.MouseWithoutBordersService.exe",
        L"PowerToys.CropAndLock.exe",
        L"PowerToys.EnvironmentVariables.exe",
        L"PowerToys.WorkspacesSnapshotTool.exe",
        L"PowerToys.WorkspacesLauncher.exe",
        L"PowerToys.WorkspacesLauncherUI.exe",
        L"PowerToys.WorkspacesEditor.exe",
        L"PowerToys.WorkspacesWindowArranger.exe",
        L"Microsoft.CmdPal.UI.exe",
        L"PowerToys.ZoomIt.exe",
        L"PowerToys.exe",
    };

    for (const auto procID : processes)
    {
        if (!procID)
        {
            continue;
        }
        wchar_t processName[MAX_PATH] = L"<unknown>";

        HANDLE hProcess{OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ | PROCESS_TERMINATE, FALSE, procID)};
        if (!hProcess)
        {
            continue;
        }
        HMODULE hMod;
        DWORD cbNeeded;

        if (!EnumProcessModules(hProcess, &hMod, sizeof(hMod), &cbNeeded))
        {
            CloseHandle(hProcess);
            continue;
        }
        GetModuleBaseNameW(hProcess, hMod, processName, sizeof(processName) / sizeof(wchar_t));

        for (const auto processToTerminate : processesToTerminate)
        {
            if (processName == processToTerminate)
            {
                const DWORD timeout = 500;
                auto windowEnumerator = [](HWND hwnd, LPARAM procIDPtr) -> BOOL
                {
                    auto targetProcID = *reinterpret_cast<const DWORD *>(procIDPtr);
                    DWORD windowProcID = 0;
                    GetWindowThreadProcessId(hwnd, &windowProcID);
                    if (windowProcID == targetProcID)
                    {
                        DWORD_PTR _{};
                        SendMessageTimeoutA(hwnd, WM_CLOSE, 0, 0, SMTO_BLOCK, timeout, &_);
                    }
                    return TRUE;
                };
                EnumWindows(windowEnumerator, reinterpret_cast<LPARAM>(&procID));
                Sleep(timeout);
                TerminateProcess(hProcess, 0);
                break;
            }
        }
        CloseHandle(hProcess);
    }

    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

UINT __stdcall SetBundleInstallLocationCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    
    // Declare all variables at the beginning to avoid goto issues
    std::wstring customActionData;
    std::wstring installationFolder;
    std::wstring bundleUpgradeCode;
    std::wstring installScope;
    bool isPerUser = false;
    size_t pos1 = std::wstring::npos;
    size_t pos2 = std::wstring::npos;
    std::vector<HKEY> keysToTry;

    hr = WcaInitialize(hInstall, "SetBundleInstallLocationCA");
    ExitOnFailure(hr, "Failed to initialize");
    
    // Parse CustomActionData: "installFolder;upgradeCode;installScope"
    hr = getInstallFolder(hInstall, customActionData);
    ExitOnFailure(hr, "Failed to get CustomActionData.");
    
    pos1 = customActionData.find(L';');
    if (pos1 == std::wstring::npos) 
    {
        hr = E_INVALIDARG;
        ExitOnFailure(hr, "Invalid CustomActionData format - missing first semicolon");
    }
    
    pos2 = customActionData.find(L';', pos1 + 1);
    if (pos2 == std::wstring::npos) 
    {
        hr = E_INVALIDARG;
        ExitOnFailure(hr, "Invalid CustomActionData format - missing second semicolon");
    }
    
    installationFolder = customActionData.substr(0, pos1);
    bundleUpgradeCode = customActionData.substr(pos1 + 1, pos2 - pos1 - 1);
    installScope = customActionData.substr(pos2 + 1);
    
    isPerUser = (installScope == L"perUser");
    
    // Use the appropriate registry based on install scope
    HKEY targetKey = isPerUser ? HKEY_CURRENT_USER : HKEY_LOCAL_MACHINE;
    const wchar_t* keyName = isPerUser ? L"HKCU" : L"HKLM";
    
    WcaLog(LOGMSG_STANDARD, "SetBundleInstallLocationCA: Searching for Bundle in %ls registry", keyName);
    
    HKEY uninstallKey;
    LONG openResult = RegOpenKeyExW(targetKey, L"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall", 0, KEY_READ | KEY_ENUMERATE_SUB_KEYS, &uninstallKey);
    if (openResult != ERROR_SUCCESS)
    {
        WcaLog(LOGMSG_STANDARD, "SetBundleInstallLocationCA: Failed to open uninstall key, error: %ld", openResult);
        goto LExit;
    }
    
    DWORD index = 0;
    wchar_t subKeyName[256];
    DWORD subKeyNameSize = sizeof(subKeyName) / sizeof(wchar_t);
    
    while (RegEnumKeyExW(uninstallKey, index, subKeyName, &subKeyNameSize, nullptr, nullptr, nullptr, nullptr) == ERROR_SUCCESS)
    {
        HKEY productKey;
        if (RegOpenKeyExW(uninstallKey, subKeyName, 0, KEY_READ | KEY_WRITE, &productKey) == ERROR_SUCCESS)
        {
            wchar_t upgradeCode[256];
            DWORD upgradeCodeSize = sizeof(upgradeCode);
            DWORD valueType;
            
            if (RegQueryValueExW(productKey, L"BundleUpgradeCode", nullptr, &valueType, 
                               reinterpret_cast<LPBYTE>(upgradeCode), &upgradeCodeSize) == ERROR_SUCCESS)
            {
                // Remove brackets from registry upgradeCode for comparison (bundleUpgradeCode doesn't have brackets)
                std::wstring regUpgradeCode = upgradeCode;
                if (!regUpgradeCode.empty() && regUpgradeCode.front() == L'{' && regUpgradeCode.back() == L'}')
                {
                    regUpgradeCode = regUpgradeCode.substr(1, regUpgradeCode.length() - 2);
                }
                
                if (_wcsicmp(regUpgradeCode.c_str(), bundleUpgradeCode.c_str()) == 0)
                {
                    // Found matching Bundle, set InstallLocation
                    LONG setResult = RegSetValueExW(productKey, L"InstallLocation", 0, REG_SZ,
                                 reinterpret_cast<const BYTE*>(installationFolder.c_str()),
                                 static_cast<DWORD>((installationFolder.length() + 1) * sizeof(wchar_t)));
                    
                    if (setResult == ERROR_SUCCESS)
                    {
                        WcaLog(LOGMSG_STANDARD, "SetBundleInstallLocationCA: InstallLocation set successfully");
                    }
                    else
                    {
                        WcaLog(LOGMSG_STANDARD, "SetBundleInstallLocationCA: Failed to set InstallLocation, error: %ld", setResult);
                    }
                    
                    RegCloseKey(productKey);
                    RegCloseKey(uninstallKey);
                    goto LExit;
                }
            }
            RegCloseKey(productKey);
        }
        
        index++;
        subKeyNameSize = sizeof(subKeyName) / sizeof(wchar_t);
    }
    
    RegCloseKey(uninstallKey);
    
LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

void initSystemLogger()
{
    static std::once_flag initLoggerFlag;
    std::call_once(initLoggerFlag, []()
                   {
            WCHAR temp_path[MAX_PATH];
            auto ret = GetTempPath(MAX_PATH, temp_path);

            if (ret)
            {
                Logger::init("PowerToysMSI", std::wstring{ temp_path } + L"\\PowerToysMSIInstaller", L"");
            } });
}

// DllMain - Initialize and cleanup WiX custom action utils.
extern "C" BOOL WINAPI DllMain(__in HINSTANCE hInst, __in ULONG ulReason, __in LPVOID)
{
    switch (ulReason)
    {
    case DLL_PROCESS_ATTACH:
        WcaGlobalInitialize(hInst);
        initSystemLogger();
        TraceLoggingRegister(g_hProvider);
        DLL_HANDLE = hInst;
        break;

    case DLL_PROCESS_DETACH:
        TraceLoggingUnregister(g_hProvider);
        WcaGlobalFinalize();
        break;
    }

    return TRUE;
}
