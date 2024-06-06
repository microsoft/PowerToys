#include "pch.h"
#include "resource.h"
#include "RcResource.h"
#include <ProjectTelemetry.h>
#include <spdlog/sinks/base_sink.h>

#include "../../src/common/logger/logger.h"
#include "../../src/common/utils/gpo.h"
#include "../../src/common/utils/MsiUtils.h"
#include "../../src/common/utils/modulesRegistry.h"
#include "../../src/common/updating/installer.h"
#include "../../src/common/version/version.h"

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
    "Microsoft.PowerToysInstaller",
    // {e1d8165d-5cb6-5c74-3b51-bdfbfe4f7a3b}
    (0xe1d8165d, 0x5cb6, 0x5c74, 0x3b, 0x51, 0xbd, 0xfb, 0xfe, 0x4f, 0x7a, 0x3b),
    TraceLoggingOptionProjectTelemetry());

const DWORD USERNAME_DOMAIN_LEN = DNLEN + UNLEN + 2; // Domain Name + '\' + User Name + '\0'
const DWORD USERNAME_LEN = UNLEN + 1; // User Name + '\0'

static const wchar_t* POWERTOYS_EXE_COMPONENT = L"{A2C66D91-3485-4D00-B04D-91844E6B345B}";
static const wchar_t* POWERTOYS_UPGRADE_CODE = L"{42B84BF7-5FBF-473B-9C8B-049DC16F7708}";

HRESULT getInstallFolder(MSIHANDLE hInstall, std::wstring& installationDir)
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
        const std::wstring myDocuments{ myDocumentsBlockPtr };
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

    if (isSystemUser) {

        auto action = [&commandLine](HANDLE userToken) {
            STARTUPINFO startupInfo{ .cb = sizeof(STARTUPINFO),  .wShowWindow = SW_SHOWNORMAL };
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
        STARTUPINFO startupInfo{ .cb = sizeof(STARTUPINFO),  .wShowWindow = SW_SHOWNORMAL };

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

    if (std::wstring{ currentScope } == L"perUser")
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

    for (const auto& changeSet : getAllOnByDefaultModulesChangeSets(installationFolder))
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
    for (const auto& changeSet : getAllModulesChangeSets(installationFolder))
    {
        changeSet.unApply();
    }

    SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, NULL, NULL);

    ExitOnFailure(hr, "Failed to extract msix");

LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

const wchar_t* DSC_CONFIGURE_PSD1_NAME = L"Microsoft.PowerToys.Configure.psd1";
const wchar_t* DSC_CONFIGURE_PSM1_NAME = L"Microsoft.PowerToys.Configure.psm1";

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

        const auto modulesPath = baseModulesPath / L"Microsoft.PowerToys.Configure" / get_product_version();

        std::error_code errorCode;
        fs::create_directories(modulesPath, errorCode);
        if (errorCode)
        {
            hr = E_FAIL;
            ExitOnFailure(hr, "Unable to create Powershell modules folder");
        }

        for (const auto* filename : { DSC_CONFIGURE_PSD1_NAME, DSC_CONFIGURE_PSM1_NAME })
        {
            fs::copy_file(fs::path(installationFolder) / "DSCModules" / filename, modulesPath / filename, fs::copy_options::overwrite_existing, errorCode);

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
        const auto versionedModulePath = powerToysModulePath / get_product_version();

        std::error_code errorCode;

        for (const auto* filename : { DSC_CONFIGURE_PSD1_NAME, DSC_CONFIGURE_PSM1_NAME })
        {
            fs::remove(versionedModulePath / filename, errorCode);

            if (errorCode)
            {
                hr = E_FAIL;
                ExitOnFailure(hr, "Unable to delete DSC file");
            }
        }

        for (const auto* modulePath : { &versionedModulePath, &powerToysModulePath })
        {
            fs::remove(*modulePath, errorCode);

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

        Uri msix_uri{ msix_path.wstring() };
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

    for (const auto& p : pm.FindPackagesForUser({}, package_name, publisher))
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

    ITaskService* pService = nullptr;
    ITaskFolder* pTaskFolder = nullptr;
    IRegisteredTaskCollection* pTaskCollection = nullptr;
    ITaskFolder* pRootFolder = nullptr;
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
        reinterpret_cast<void**>(&pService));
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
        IRegisteredTask* pRegisteredTask = nullptr;
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

    TraceLoggingWrite(
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

    TraceLoggingWrite(
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

    TraceLoggingWrite(
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

    TraceLoggingWrite(
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

    TraceLoggingWrite(
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

    TraceLoggingWrite(
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

    TraceLoggingWrite(
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

    TraceLoggingWrite(
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
        if (auto install_path = GetMsiPackageInstalledPath(std::wstring{ currentScope } == L"perUser"))
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

UINT __stdcall CertifyVirtualCameraDriverCA(MSIHANDLE hInstall)
{
#ifdef CIBuild // On pipeline we are using microsoft certification
    WcaInitialize(hInstall, "CertifyVirtualCameraDriverCA");
    return WcaFinalize(ERROR_SUCCESS);
#else
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    LPWSTR certificatePath = nullptr;
    HCERTSTORE hCertStore = nullptr;
    HANDLE hfile = nullptr;
    DWORD size = INVALID_FILE_SIZE;
    char* pFileContent = nullptr;

    hr = WcaInitialize(hInstall, "CertifyVirtualCameraDriverCA");
    ExitOnFailure(hr, "Failed to initialize", hr);

    hr = WcaGetProperty(L"CustomActionData", &certificatePath);
    ExitOnFailure(hr, "Failed to get install property", hr);

    hCertStore = CertOpenStore(CERT_STORE_PROV_SYSTEM, 0, 0, CERT_SYSTEM_STORE_LOCAL_MACHINE, L"AuthRoot");
    if (!hCertStore)
    {
        hr = GetLastError();
        ExitOnFailure(hr, "Cannot put principal run level: %x", hr);
    }

    hfile = CreateFile(certificatePath, GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (hfile == INVALID_HANDLE_VALUE)
    {
        hr = GetLastError();
        ExitOnFailure(hr, "Certificate file open failed", hr);
    }

    size = GetFileSize(hfile, nullptr);
    if (size == INVALID_FILE_SIZE)
    {
        hr = GetLastError();
        ExitOnFailure(hr, "Certificate file size not valid", hr);
    }

    pFileContent = static_cast<char*>(malloc(size));

    DWORD sizeread;
    if (!ReadFile(hfile, pFileContent, size, &sizeread, nullptr))
    {
        hr = GetLastError();
        ExitOnFailure(hr, "Certificate file read failed", hr);
    }

    if (!CertAddEncodedCertificateToStore(hCertStore,
        X509_ASN_ENCODING,
        reinterpret_cast<const BYTE*>(pFileContent),
        size,
        CERT_STORE_ADD_ALWAYS,
        nullptr))
    {
        hr = GetLastError();
        ExitOnFailure(hr, "Adding certificate failed", hr);
    }

    free(pFileContent);

LExit:
    ReleaseStr(certificatePath);
    if (hCertStore)
    {
        CertCloseStore(hCertStore, 0);
    }
    if (hfile)
    {
        CloseHandle(hfile);
    }

    if (!SUCCEEDED(hr))
    {
        PMSIHANDLE hRecord = MsiCreateRecord(0);
        MsiRecordSetString(hRecord, 0, TEXT("Failed to add certificate to store"));
        MsiProcessMessage(hInstall, static_cast<INSTALLMESSAGE>(INSTALLMESSAGE_WARNING + MB_OK), hRecord);
    }

    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
#endif
}

UINT __stdcall InstallVirtualCameraDriverCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    LPWSTR driverPath = nullptr;

    hr = WcaInitialize(hInstall, "InstallVirtualCameraDriverCA");
    ExitOnFailure(hr, "Failed to initialize");

    hr = WcaGetProperty(L"CustomActionData", &driverPath);
    ExitOnFailure(hr, "Failed to get install property");

    BOOL requiresReboot;
    DiInstallDriverW(GetConsoleWindow(), driverPath, DIIRFLAG_FORCE_INF, &requiresReboot);

    hr = GetLastError();
    ExitOnFailure(hr, "Failed to install driver");

LExit:

    if (!SUCCEEDED(hr))
    {
        PMSIHANDLE hRecord = MsiCreateRecord(0);
        MsiRecordSetString(hRecord, 0, TEXT("Failed to install virtual camera driver"));
        MsiProcessMessage(hInstall, static_cast<INSTALLMESSAGE>(INSTALLMESSAGE_WARNING + MB_OK), hRecord);
    }

    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

UINT __stdcall UninstallVirtualCameraDriverCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    LPWSTR driverPath = nullptr;

    hr = WcaInitialize(hInstall, "UninstallVirtualCameraDriverCA");
    ExitOnFailure(hr, "Failed to initialize");

    hr = WcaGetProperty(L"CustomActionData", &driverPath);
    ExitOnFailure(hr, "Failed to get uninstall property");

    BOOL requiresReboot;
    DiUninstallDriverW(GetConsoleWindow(), driverPath, 0, &requiresReboot);

    switch (GetLastError())
    {
    case ERROR_ACCESS_DENIED:
    case ERROR_FILE_NOT_FOUND:
    case ERROR_INVALID_FLAGS:
    case ERROR_IN_WOW64:
    {
        hr = GetLastError();
        ExitOnFailure(hr, "Failed to uninstall driver");
        break;
    }
    }

LExit:

    if (!SUCCEEDED(hr))
    {
        PMSIHANDLE hRecord = MsiCreateRecord(0);
        MsiRecordSetString(hRecord, 0, TEXT("Failed to uninstall virtual camera driver"));
        MsiProcessMessage(hInstall, static_cast<INSTALLMESSAGE>(INSTALLMESSAGE_WARNING + MB_OK), hRecord);
    }

    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
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
        const std::vector<std::wstring> packagesToRemoveDisplayName{ { L"PowerRenameContextMenu" }, { L"ImageResizerContextMenu" }, { L"FileLocksmithContextMenu" } };

        PackageManager packageManager;

        for (auto const& package : packageManager.FindPackages())
        {
            const auto& packageFullName = std::wstring{ package.Id().FullName() };

            for (const auto& packageToRemove : packagesToRemoveDisplayName)
            {
                if (packageFullName.contains(packageToRemove))
                {
                    auto deploymentOperation{ packageManager.RemovePackageAsync(packageFullName) };
                    deploymentOperation.get();

                    // Check the status of the operation
                    if (deploymentOperation.Status() == AsyncStatus::Error)
                    {
                        auto deploymentResult{ deploymentOperation.GetResults() };
                        auto errorCode = deploymentOperation.ErrorCode();
                        auto errorText = deploymentResult.ErrorText();

                        Logger::error(L"Unregister {} package failed. ErrorCode: {}, ErrorText: {}", packageFullName, std::to_wstring(errorCode), errorText);

                        er = ERROR_INSTALL_FAILURE;
                    }
                    else if (deploymentOperation.Status() == AsyncStatus::Canceled)
                    {
                        Logger::error(L"Unregister {} package canceled.", packageFullName);

                        er = ERROR_INSTALL_FAILURE;
                    }
                    else if (deploymentOperation.Status() == AsyncStatus::Completed)
                    {
                        Logger::info(L"Unregister {} package completed.", packageFullName);
                    }
                    else
                    {
                        Logger::debug(L"Unregister {} package started.", packageFullName);
                    }
                }

            }
        }
    }
    catch (std::exception& e)
    {
        std::string errorMessage{ "Exception thrown while trying to unregister sparse packages: " };
        errorMessage += e.what();
        Logger::error(errorMessage);

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

    std::array<std::wstring_view, 32> processesToTerminate = {
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
        L"PowerToys.GcodeThumbnailProvider.exe",
        L"PowerToys.PdfThumbnailProvider.exe",
        L"PowerToys.MonacoPreviewHandler.exe",
        L"PowerToys.MarkdownPreviewHandler.exe",
        L"PowerToys.StlThumbnailProvider.exe",
        L"PowerToys.SvgThumbnailProvider.exe",
        L"PowerToys.GcodePreviewHandler.exe",
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
        L"PowerToys.exe",
    };

    for (const auto procID : processes)
    {
        if (!procID)
        {
            continue;
        }
        wchar_t processName[MAX_PATH] = L"<unknown>";

        HANDLE hProcess{ OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ | PROCESS_TERMINATE, FALSE, procID) };
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
                auto windowEnumerator = [](HWND hwnd, LPARAM procIDPtr) -> BOOL {
                    auto targetProcID = *reinterpret_cast<const DWORD*>(procIDPtr);
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

void initSystemLogger()
{
    static std::once_flag initLoggerFlag;
    std::call_once(initLoggerFlag, []() {
        WCHAR temp_path[MAX_PATH];
        auto ret = GetTempPath(MAX_PATH, temp_path);

        if (ret)
        {
            Logger::init("PowerToysMSI", std::wstring{ temp_path } + L"\\PowerToysMSIInstaller", L"");
        }
        });
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
