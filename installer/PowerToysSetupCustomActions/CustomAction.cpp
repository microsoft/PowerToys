#include "stdafx.h"
#include "resource.h"
#include "RcResource.h"
#include <ProjectTelemetry.h>

#include <spdlog/sinks/base_sink.h>

#include "../../src/common/logger/logger.h"
#include "../../src/common/utils/MsiUtils.h"
#include "../../src/common/utils/modulesRegistry.h"
#include "../../src/common/updating/installer.h"
#include "../../src/common/version/version.h"

#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Management.Deployment.h>

#include "DepsFilesLists.h"

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

struct WcaSink : spdlog::sinks::base_sink<std::mutex>
{
    virtual void sink_it_(const spdlog::details::log_msg& msg) override
    {
        WcaLog(LOGMSG_STANDARD, msg.payload.data());
    }
    virtual void flush_() override
    {
        // we don't need to flush wca log manually
    }
};

void initSystemLogger()
{
    static std::once_flag initLoggerFlag;
    std::call_once(initLoggerFlag, []() { Logger::init(std::vector<spdlog::sink_ptr>{ std::make_shared<WcaSink>() }); });
}

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
UINT __stdcall ApplyModulesRegistryChangeSetsCA(MSIHANDLE hInstall)
{
    initSystemLogger();
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
            WcaLog(LOGMSG_STANDARD, "Couldn't apply registry changeSet");
            failedToApply = true;
        }
    }

    if (!failedToApply)
    {
        WcaLog(LOGMSG_STANDARD, "All registry changeSets applied successfully");
    }
LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

UINT __stdcall UnApplyModulesRegistryChangeSetsCA(MSIHANDLE hInstall)
{
    initSystemLogger();
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

    ExitOnFailure(hr, "Failed to extract msix");

LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
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
        WcaLog(LOGMSG_STANDARD, "Extracted MSIX");
        // TODO: Use to activate embedded MSIX
        const auto msix_path = std::filesystem::temp_directory_path() / "hello_package.msix";
        if (!msix->saveAsFile(msix_path))
        {
            ExitOnFailure(hr, "Failed to save msix");
        }
        WcaLog(LOGMSG_STANDARD, "Saved MSIX");
        using namespace winrt::Windows::Management::Deployment;
        using namespace winrt::Windows::Foundation;

        Uri msix_uri{ msix_path.wstring() };
        PackageManager pm;
        auto result = pm.AddPackageAsync(msix_uri, nullptr, DeploymentOptions::None).get();
        if (!result)
        {
            ExitOnFailure(hr, "Failed to AddPackage");
        }

        WcaLog(LOGMSG_STANDARD, "MSIX[s] were installed!");
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
            WcaLog(LOGMSG_STANDARD, "MSIX was uninstalled!");
        }
        else
        {
            WcaLog(LOGMSG_STANDARD, "Couldn't uninstall MSIX!");
        }
    }

LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

// Creates a Scheduled Task to run at logon for the current user.
// The path of the executable to run should be passed as the CustomActionData (Value).
// Based on the Task Scheduler Logon Trigger Example:
// https://learn.microsoft.com/windows/win32/taskschd/logon-trigger-example--c---/
UINT __stdcall CreateScheduledTaskCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;

    TCHAR username_domain[USERNAME_DOMAIN_LEN];
    TCHAR username[USERNAME_LEN];

    std::wstring wstrTaskName;

    ITaskService* pService = nullptr;
    ITaskFolder* pTaskFolder = nullptr;
    ITaskDefinition* pTask = nullptr;
    IRegistrationInfo* pRegInfo = nullptr;
    ITaskSettings* pSettings = nullptr;
    ITriggerCollection* pTriggerCollection = nullptr;
    IRegisteredTask* pRegisteredTask = nullptr;
    IPrincipal* pPrincipal = nullptr;
    ITrigger* pTrigger = nullptr;
    ILogonTrigger* pLogonTrigger = nullptr;
    IAction* pAction = nullptr;
    IActionCollection* pActionCollection = nullptr;
    IExecAction* pExecAction = nullptr;

    LPWSTR wszExecutablePath = nullptr;

    hr = WcaInitialize(hInstall, "CreateScheduledTaskCA");
    ExitOnFailure(hr, "Failed to initialize");

    WcaLog(LOGMSG_STANDARD, "Initialized.");

    // ------------------------------------------------------
    // Get the Domain/Username for the trigger.
    //
    // This action needs to run as the system to get elevated privileges from the installation,
    // so GetUserNameEx can't be used to get the current user details.
    // The USERNAME and USERDOMAIN environment variables are used instead.
    if (!GetEnvironmentVariable(L"USERNAME", username, USERNAME_LEN))
    {
        ExitWithLastError(hr, "Getting username failed: %x", hr);
    }
    if (!GetEnvironmentVariable(L"USERDOMAIN", username_domain, USERNAME_DOMAIN_LEN))
    {
        ExitWithLastError(hr, "Getting the user's domain failed: %x", hr);
    }
    wcscat_s(username_domain, L"\\");
    wcscat_s(username_domain, username);

    WcaLog(LOGMSG_STANDARD, "Current user detected: %ls", username_domain);

    // Task Name.
    wstrTaskName = L"Autorun for ";
    wstrTaskName += username;

    // Get the executable path passed to the custom action.
    hr = WcaGetProperty(L"CustomActionData", &wszExecutablePath);
    ExitOnFailure(hr, "Failed to get the executable path from CustomActionData.");

    // COM and Security Initialization is expected to have been done by the MSI.
    // It couldn't be done in the DLL, anyway.
    // ------------------------------------------------------
    // Create an instance of the Task Service.
    hr = CoCreateInstance(CLSID_TaskScheduler,
                          nullptr,
                          CLSCTX_INPROC_SERVER,
                          IID_ITaskService,
                          (void**)&pService);
    ExitOnFailure(hr, "Failed to create an instance of ITaskService: %x", hr);

    // Connect to the task service.
    hr = pService->Connect(_variant_t(), _variant_t(), _variant_t(), _variant_t());
    ExitOnFailure(hr, "ITaskService::Connect failed: %x", hr);

    // ------------------------------------------------------
    // Get the PowerToys task folder. Creates it if it doesn't exist.
    hr = pService->GetFolder(_bstr_t(L"\\PowerToys"), &pTaskFolder);
    if (FAILED(hr))
    {
        // Folder doesn't exist. Get the Root folder and create the PowerToys subfolder.
        ITaskFolder* pRootFolder = nullptr;
        hr = pService->GetFolder(_bstr_t(L"\\"), &pRootFolder);
        ExitOnFailure(hr, "Cannot get Root Folder pointer: %x", hr);
        hr = pRootFolder->CreateFolder(_bstr_t(L"\\PowerToys"), _variant_t(L""), &pTaskFolder);
        if (FAILED(hr))
        {
            pRootFolder->Release();
            ExitOnFailure(hr, "Cannot create PowerToys task folder: %x", hr);
        }
        WcaLog(LOGMSG_STANDARD, "PowerToys task folder created.");
    }

    // If the same task exists, remove it.
    pTaskFolder->DeleteTask(_bstr_t(wstrTaskName.c_str()), 0);

    // Create the task builder object to create the task.
    hr = pService->NewTask(0, &pTask);
    ExitOnFailure(hr, "Failed to create a task definition: %x", hr);

    // ------------------------------------------------------
    // Get the registration info for setting the identification.
    hr = pTask->get_RegistrationInfo(&pRegInfo);
    ExitOnFailure(hr, "Cannot get identification pointer: %x", hr);
    hr = pRegInfo->put_Author(_bstr_t(username_domain));
    ExitOnFailure(hr, "Cannot put identification info: %x", hr);

    // ------------------------------------------------------
    // Create the settings for the task
    hr = pTask->get_Settings(&pSettings);
    ExitOnFailure(hr, "Cannot get settings pointer: %x", hr);

    hr = pSettings->put_StartWhenAvailable(VARIANT_FALSE);
    ExitOnFailure(hr, "Cannot put_StartWhenAvailable setting info: %x", hr);
    hr = pSettings->put_StopIfGoingOnBatteries(VARIANT_FALSE);
    ExitOnFailure(hr, "Cannot put_StopIfGoingOnBatteries setting info: %x", hr);
    hr = pSettings->put_ExecutionTimeLimit(_bstr_t(L"PT0S")); //Unlimited
    ExitOnFailure(hr, "Cannot put_ExecutionTimeLimit setting info: %x", hr);
    hr = pSettings->put_DisallowStartIfOnBatteries(VARIANT_FALSE);
    ExitOnFailure(hr, "Cannot put_DisallowStartIfOnBatteries setting info: %x", hr);

    // ------------------------------------------------------
    // Get the trigger collection to insert the logon trigger.
    hr = pTask->get_Triggers(&pTriggerCollection);
    ExitOnFailure(hr, "Cannot get trigger collection: %x", hr);

    // Add the logon trigger to the task.
    hr = pTriggerCollection->Create(TASK_TRIGGER_LOGON, &pTrigger);
    ExitOnFailure(hr, "Cannot create the trigger: %x", hr);

    hr = pTrigger->QueryInterface(
        IID_ILogonTrigger, (void**)&pLogonTrigger);
    pTrigger->Release();
    ExitOnFailure(hr, "QueryInterface call failed for ILogonTrigger: %x", hr);

    hr = pLogonTrigger->put_Id(_bstr_t(L"Trigger1"));
    if (FAILED(hr))
    {
        WcaLogError(hr, "Cannot put the trigger ID: %x", hr);
    }

    // Timing issues may make explorer not be started when the task runs.
    // Add a little delay to mitigate this.
    hr = pLogonTrigger->put_Delay(_bstr_t(L"PT03S"));
    if (FAILED(hr))
    {
        WcaLogError(hr, "Cannot put the trigger delay: %x", hr);
    }

    // Define the user. The task will execute when the user logs on.
    // The specified user must be a user on this computer.
    hr = pLogonTrigger->put_UserId(_bstr_t(username_domain));
    pLogonTrigger->Release();
    ExitOnFailure(hr, "Cannot add user ID to logon trigger: %x", hr);

    // ------------------------------------------------------
    // Add an Action to the task. This task will execute the path passed to this custom action.

    // Get the task action collection pointer.
    hr = pTask->get_Actions(&pActionCollection);
    ExitOnFailure(hr, "Cannot get Task collection pointer: %x", hr);

    // Create the action, specifying that it is an executable action.
    hr = pActionCollection->Create(TASK_ACTION_EXEC, &pAction);
    pActionCollection->Release();
    ExitOnFailure(hr, "Cannot create the action: %x", hr);

    // QI for the executable task pointer.
    hr = pAction->QueryInterface(
        IID_IExecAction, (void**)&pExecAction);
    pAction->Release();
    ExitOnFailure(hr, "QueryInterface call failed for IExecAction: %x", hr);

    // Set the path of the executable to PowerToys (passed as CustomActionData).
    hr = pExecAction->put_Path(_bstr_t(wszExecutablePath));
    pExecAction->Release();
    ExitOnFailure(hr, "Cannot set path of executable: %x", hr);

    // ------------------------------------------------------
    // Create the principal for the task
    hr = pTask->get_Principal(&pPrincipal);
    ExitOnFailure(hr, "Cannot get principal pointer: %x", hr);

    // Set up principal information:
    hr = pPrincipal->put_Id(_bstr_t(L"Principal1"));
    if (FAILED(hr))
    {
        WcaLogError(hr, "Cannot put the principal ID: %x", hr);
    }

    hr = pPrincipal->put_UserId(_bstr_t(username_domain));
    if (FAILED(hr))
    {
        WcaLogError(hr, "Cannot put principal user Id: %x", hr);
    }

    hr = pPrincipal->put_LogonType(TASK_LOGON_INTERACTIVE_TOKEN);
    if (FAILED(hr))
    {
        WcaLogError(hr, "Cannot put principal logon type: %x", hr);
    }

    // Run the task with the highest available privileges.
    hr = pPrincipal->put_RunLevel(TASK_RUNLEVEL_LUA);
    pPrincipal->Release();
    ExitOnFailure(hr, "Cannot put principal run level: %x", hr);

    // ------------------------------------------------------
    //  Save the task in the PowerToys folder.
    {
        _variant_t SDDL_FULL_ACCESS_FOR_EVERYONE = L"D:(A;;FA;;;WD)";
        hr = pTaskFolder->RegisterTaskDefinition(
            _bstr_t(wstrTaskName.c_str()),
            pTask,
            TASK_CREATE_OR_UPDATE,
            _variant_t(username_domain),
            _variant_t(),
            TASK_LOGON_INTERACTIVE_TOKEN,
            SDDL_FULL_ACCESS_FOR_EVERYONE,
            &pRegisteredTask);
        ExitOnFailure(hr, "Error saving the Task : %x", hr);
    }

    WcaLog(LOGMSG_STANDARD, "Scheduled task created for the current user.");

LExit:
    ReleaseStr(wszExecutablePath);
    if (pService)
    {
        pService->Release();
    }
    if (pTaskFolder)
    {
        pTaskFolder->Release();
    }
    if (pTask)
    {
        pTask->Release();
    }
    if (pRegInfo)
    {
        pRegInfo->Release();
    }
    if (pSettings)
    {
        pSettings->Release();
    }
    if (pTriggerCollection)
    {
        pTriggerCollection->Release();
    }
    if (pRegisteredTask)
    {
        pRegisteredTask->Release();
    }

    if (!SUCCEEDED(hr))
    {
        PMSIHANDLE hRecord = MsiCreateRecord(0);
        MsiRecordSetString(hRecord, 0, TEXT("Failed to create a scheduled task to start PowerToys at user login. You can re-try to create the scheduled task using the PowerToys settings."));
        MsiProcessMessage(hInstall, static_cast<INSTALLMESSAGE>(INSTALLMESSAGE_WARNING + MB_OK), hRecord);
    }

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

    WcaLog(LOGMSG_STANDARD, "Initialized.");

    // COM and Security Initialization is expected to have been done by the MSI.
    // It couldn't be done in the DLL, anyway.
    // ------------------------------------------------------
    // Create an instance of the Task Service.
    hr = CoCreateInstance(CLSID_TaskScheduler,
                          nullptr,
                          CLSCTX_INPROC_SERVER,
                          IID_ITaskService,
                          (void**)&pService);
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
        WcaLog(LOGMSG_STANDARD, "The PowerToys scheduled task folder wasn't found. Nothing to delete.");
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
                    WcaLogError(hr, "Cannot delete the '%S' task: %x", taskName, hr);
                }
                SysFreeString(taskName);
            }
            else
            {
                WcaLogError(hr, "Cannot get the registered task name: %x", hr);
            }
            pRegisteredTask->Release();
        }
        else
        {
            WcaLogError(hr, "Cannot get the registered task item at index=%d: %x", i + 1, hr);
        }
    }

    // ------------------------------------------------------
    // Get the pointer to the root task folder and delete the PowerToys subfolder.
    hr = pService->GetFolder(_bstr_t(L"\\"), &pRootFolder);
    ExitOnFailure(hr, "Cannot get Root Folder pointer: %x", hr);
    hr = pRootFolder->DeleteFolder(_bstr_t(L"PowerToys"), 0);
    pRootFolder->Release();
    ExitOnFailure(hr, "Cannot delete the PowerToys folder: %x", hr);

    WcaLog(LOGMSG_STANDARD, "Deleted the PowerToys Task Scheduler folder.");

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
    try
    {
        if (auto install_path = GetMsiPackageInstalledPath())
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

    pFileContent = (char*)malloc(size);

    DWORD sizeread;
    if (!ReadFile(hfile, pFileContent, size, &sizeread, nullptr))
    {
        hr = GetLastError();
        ExitOnFailure(hr, "Certificate file read failed", hr);
    }

    if (!CertAddEncodedCertificateToStore(hCertStore,
                                          X509_ASN_ENCODING,
                                          (const BYTE*)pFileContent,
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
        const std::vector<std::wstring> packagesToRemoveDisplayName{ { L"PowerRenameContextMenu" }, { L"ImageResizerContextMenu" } };

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

const std::wstring WinAppSDKConsumers[] =
{
    L"Settings",
    L"modules\\PowerRename",
    L"modules\\MeasureTool",
    L"modules\\FileLocksmith",
    L"modules\\Hosts",
};

UINT __stdcall CreateWinAppSDKHardlinksCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    std::wstring installationFolder, winAppSDKFilesSrcDir;

    hr = WcaInitialize(hInstall, "CreateWinAppSDKHardlinksCA");
    ExitOnFailure(hr, "Failed to initialize");

    hr = getInstallFolder(hInstall, installationFolder);
    ExitOnFailure(hr, "Failed to get installation folder");

    winAppSDKFilesSrcDir = installationFolder + L"dll\\WinAppSDK\\";

    for (auto file : winAppSdkFiles)
    {
        for (auto consumer : WinAppSDKConsumers)
        {
            std::error_code ec;
            std::filesystem::create_hard_link((winAppSDKFilesSrcDir + file).c_str(), (installationFolder + consumer + L"\\" + file).c_str(), ec);

            if (ec.value() != S_OK)
            {
                std::wstring errorMessage{ L"Error creating hard link for: " };
                errorMessage += file;
                errorMessage += L", error code: " + std::to_wstring(ec.value());
                Logger::error(errorMessage);
            }
        }
    }

LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

const std::wstring PTInteropConsumers[] =
{
    L"modules\\ColorPicker",
    L"modules\\PowerOCR",
    L"modules\\launcher",
    L"modules\\FancyZones",
    L"modules\\ImageResizer",
    L"Settings",
    L"modules\\Awake",
    L"modules\\MeasureTool",
    L"modules\\PowerAccent",
    L"modules\\FileLocksmith",
    L"modules\\Hosts",
    L"modules\\FileExplorerPreview",
};

UINT __stdcall CreatePTInteropHardlinksCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    std::wstring installationFolder, interopFilesSrcDir;

    hr = WcaInitialize(hInstall, "CreatePTInteropHardlinksCA");
    ExitOnFailure(hr, "Failed to initialize");

    hr = getInstallFolder(hInstall, installationFolder);
    ExitOnFailure(hr, "Failed to get installation folder");

    interopFilesSrcDir = installationFolder + L"dll\\Interop\\";

    for (auto file : powerToysInteropFiles)
    {    
        for (auto consumer : PTInteropConsumers)
        {
            std::error_code ec;
            std::filesystem::create_hard_link((interopFilesSrcDir + file).c_str(), (installationFolder + consumer + L"\\" + file).c_str(), ec);
        
            if (ec.value() != S_OK)
            {
                std::wstring errorMessage{ L"Error creating hard link for: " };
                errorMessage += file;
                errorMessage += L", error code: " + std::to_wstring(ec.value());
                Logger::error(errorMessage);
            }
        }
    }

LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

UINT __stdcall CreateDotnetRuntimeHardlinksCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    std::wstring installationFolder, dotnetRuntimeFilesSrcDir, colorPickerDir, powerOCRDir, launcherDir, fancyZonesDir,
      imageResizerDir, settingsDir, awakeDir, measureToolDir, powerAccentDir, fileExplorerAddOnsDir, hostsDir, fileLocksmithDir;

    hr = WcaInitialize(hInstall, "CreateDotnetRuntimeHardlinksCA");
    ExitOnFailure(hr, "Failed to initialize");

    hr = getInstallFolder(hInstall, installationFolder);
    ExitOnFailure(hr, "Failed to get installation folder");

    dotnetRuntimeFilesSrcDir = installationFolder + L"dll\\dotnet\\";
    colorPickerDir = installationFolder + L"modules\\ColorPicker\\";
    powerOCRDir = installationFolder + L"modules\\PowerOCR\\";
    launcherDir = installationFolder + L"modules\\launcher\\";
    fancyZonesDir = installationFolder + L"modules\\FancyZones\\";
    imageResizerDir = installationFolder + L"modules\\ImageResizer\\";
    settingsDir = installationFolder + L"Settings\\";
    awakeDir = installationFolder + L"modules\\Awake\\";
    measureToolDir = installationFolder + L"modules\\MeasureTool\\";
    powerAccentDir = installationFolder + L"modules\\PowerAccent\\";
    fileExplorerAddOnsDir = installationFolder + L"modules\\FileExplorerPreview\\";
    hostsDir = installationFolder + L"modules\\Hosts\\";
    fileLocksmithDir = installationFolder + L"modules\\FileLocksmith\\";

    for (auto file : dotnetRuntimeFiles)
    {
        std::error_code ec;
        std::filesystem::create_hard_link((dotnetRuntimeFilesSrcDir + file).c_str(), (colorPickerDir + file).c_str(), ec);
        std::filesystem::create_hard_link((dotnetRuntimeFilesSrcDir + file).c_str(), (powerOCRDir + file).c_str(), ec);
        std::filesystem::create_hard_link((dotnetRuntimeFilesSrcDir + file).c_str(), (launcherDir + file).c_str(), ec);
        std::filesystem::create_hard_link((dotnetRuntimeFilesSrcDir + file).c_str(), (fancyZonesDir + file).c_str(), ec);
        std::filesystem::create_hard_link((dotnetRuntimeFilesSrcDir + file).c_str(), (imageResizerDir + file).c_str(), ec);
        std::filesystem::create_hard_link((dotnetRuntimeFilesSrcDir + file).c_str(), (settingsDir + file).c_str(), ec);
        std::filesystem::create_hard_link((dotnetRuntimeFilesSrcDir + file).c_str(), (awakeDir + file).c_str(), ec);
        std::filesystem::create_hard_link((dotnetRuntimeFilesSrcDir + file).c_str(), (measureToolDir + file).c_str(), ec);
        std::filesystem::create_hard_link((dotnetRuntimeFilesSrcDir + file).c_str(), (powerAccentDir + file).c_str(), ec);
        std::filesystem::create_hard_link((dotnetRuntimeFilesSrcDir + file).c_str(), (fileExplorerAddOnsDir + file).c_str(), ec);
        std::filesystem::create_hard_link((dotnetRuntimeFilesSrcDir + file).c_str(), (hostsDir + file).c_str(), ec);
        std::filesystem::create_hard_link((dotnetRuntimeFilesSrcDir + file).c_str(), (fileLocksmithDir + file).c_str(), ec);

        if (ec.value() != S_OK)
        {
            std::wstring errorMessage{ L"Error creating hard link for: " };
            errorMessage += file;
            errorMessage += L", error code: " + std::to_wstring(ec.value());
            Logger::error(errorMessage);
            er = ERROR_INSTALL_FAILURE;
        }
    }

    for (auto file : dotnetRuntimeWPFFiles)
    {
      std::error_code ec;
      std::filesystem::create_hard_link((dotnetRuntimeFilesSrcDir + file).c_str(), (awakeDir + file).c_str(), ec);
      std::filesystem::create_hard_link((dotnetRuntimeFilesSrcDir + file).c_str(), (colorPickerDir + file).c_str(), ec);
      std::filesystem::create_hard_link((dotnetRuntimeFilesSrcDir + file).c_str(), (powerOCRDir + file).c_str(), ec);
      std::filesystem::create_hard_link((dotnetRuntimeFilesSrcDir + file).c_str(), (launcherDir + file).c_str(), ec);
      std::filesystem::create_hard_link((dotnetRuntimeFilesSrcDir + file).c_str(), (fancyZonesDir + file).c_str(), ec);
      std::filesystem::create_hard_link((dotnetRuntimeFilesSrcDir + file).c_str(), (imageResizerDir + file).c_str(), ec);
      std::filesystem::create_hard_link((dotnetRuntimeFilesSrcDir + file).c_str(), (powerAccentDir + file).c_str(), ec);
      std::filesystem::create_hard_link((dotnetRuntimeFilesSrcDir + file).c_str(), (fileExplorerAddOnsDir + file).c_str(), ec);
      std::filesystem::create_hard_link((dotnetRuntimeFilesSrcDir + file).c_str(), (hostsDir + file).c_str(), ec);

      if (ec.value() != S_OK)
      {
        std::wstring errorMessage{ L"Error creating hard link for: " };
        errorMessage += file;
        errorMessage += L", error code: " + std::to_wstring(ec.value());
        Logger::error(errorMessage);
        er = ERROR_INSTALL_FAILURE;
      }
    }

    LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

UINT __stdcall DeleteWinAppSDKHardlinksCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    std::wstring installationFolder;

    hr = WcaInitialize(hInstall, "DeleteWinAppSDKHardlinksCA");
    ExitOnFailure(hr, "Failed to initialize");

    hr = getInstallFolder(hInstall, installationFolder);
    ExitOnFailure(hr, "Failed to get installation folder");

    try
    {
        for (auto file : winAppSdkFiles)
        {
            for (auto consumer : WinAppSDKConsumers)
            {
                DeleteFile((installationFolder + consumer + L"\\" + file).c_str());
            }
        }
    }
    catch (std::exception e)
    {
        std::string errorMessage{ "Exception thrown while trying to delete WAS hardlinks: " };
        errorMessage += e.what();
        Logger::error(errorMessage);

        er = ERROR_INSTALL_FAILURE;
    }

LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

UINT __stdcall DeletePTInteropHardlinksCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    std::wstring installationFolder, interopFilesSrcDir;

    hr = WcaInitialize(hInstall, "DeletePTInteropHardlinksCA");
    ExitOnFailure(hr, "Failed to initialize");

    hr = getInstallFolder(hInstall, installationFolder);
    ExitOnFailure(hr, "Failed to get installation folder");

    try
    {
        for (auto file : powerToysInteropFiles)
        {
            for (auto consumer : PTInteropConsumers)
            {
                DeleteFile((installationFolder + consumer + L"\\" + file).c_str());
            }
        }
    }
    catch (std::exception e)
    {
        std::string errorMessage{ "Exception thrown while trying to delete PowerToys Interop and VC Redist hardlinks: " };
        errorMessage += e.what();
        Logger::error(errorMessage);

        er = ERROR_INSTALL_FAILURE;
    }

LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

UINT __stdcall DeleteDotnetRuntimeHardlinksCA(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    std::wstring installationFolder, colorPickerDir, powerOCRDir, launcherDir, fancyZonesDir,
      imageResizerDir, settingsDir, awakeDir, measureToolDir, powerAccentDir, fileExplorerAddOnsDir,
      hostsDir, fileLocksmithDir;

    hr = WcaInitialize(hInstall, "DeleteDotnetRuntimeHardlinksCA");
    ExitOnFailure(hr, "Failed to initialize");

    hr = getInstallFolder(hInstall, installationFolder);
    ExitOnFailure(hr, "Failed to get installation folder");

    colorPickerDir = installationFolder + L"modules\\ColorPicker\\";
    powerOCRDir = installationFolder + L"modules\\PowerOCR\\";
    launcherDir = installationFolder + L"modules\\launcher\\";
    fancyZonesDir = installationFolder + L"modules\\FancyZones\\";
    imageResizerDir = installationFolder + L"modules\\ImageResizer\\";
    settingsDir = installationFolder + L"Settings\\";
    awakeDir = installationFolder + L"modules\\Awake\\";
    measureToolDir = installationFolder + L"modules\\MeasureTool\\";
    powerAccentDir = installationFolder + L"modules\\PowerAccent\\";
    fileExplorerAddOnsDir = installationFolder + L"modules\\FileExplorerPreview\\";
    hostsDir = installationFolder + L"modules\\Hosts\\";
    fileLocksmithDir = installationFolder + L"modules\\FileLocksmith\\";

    try
    {
        for (auto file : dotnetRuntimeFiles)
        {
          DeleteFile((colorPickerDir + file).c_str());
          DeleteFile((powerOCRDir + file).c_str());
          DeleteFile((launcherDir + file).c_str());
          DeleteFile((fancyZonesDir + file).c_str());
          DeleteFile((imageResizerDir + file).c_str());
          DeleteFile((settingsDir + file).c_str());
          DeleteFile((awakeDir + file).c_str());
          DeleteFile((measureToolDir + file).c_str());
          DeleteFile((powerAccentDir + file).c_str());
          DeleteFile((fileExplorerAddOnsDir + file).c_str());
          DeleteFile((hostsDir + file).c_str());
          DeleteFile((fileLocksmithDir + file).c_str());
        }

        for (auto file : dotnetRuntimeWPFFiles)
        {
          DeleteFile((awakeDir + file).c_str());
          DeleteFile((colorPickerDir + file).c_str());
          DeleteFile((powerOCRDir + file).c_str());
          DeleteFile((launcherDir + file).c_str());
          DeleteFile((fancyZonesDir + file).c_str());
          DeleteFile((imageResizerDir + file).c_str());
          DeleteFile((powerAccentDir + file).c_str());
          DeleteFile((fileExplorerAddOnsDir + file).c_str());
          DeleteFile((hostsDir + file).c_str());
        }
    }
    catch (std::exception e)
    {
        std::string errorMessage{ "Exception thrown while trying to delete dotnet runtime hardlinks: " };
        errorMessage += e.what();
        Logger::error(errorMessage);

        er = ERROR_INSTALL_FAILURE;
    }

LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
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

    std::array<std::wstring_view, 9> processesToTerminate = {
        L"PowerToys.PowerLauncher.exe",
        L"PowerToys.Settings.exe",
        L"PowerToys.Awake.exe",
        L"PowerToys.FancyZones.exe",
        L"PowerToys.FancyZonesEditor.exe",
        L"PowerToys.FileLocksmithUI.exe",
        L"PowerToys.ColorPickerUI.exe",
        L"PowerToys.AlwaysOnTop.exe",
        L"PowerToys.exe"
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

// DllMain - Initialize and cleanup WiX custom action utils.
extern "C" BOOL WINAPI DllMain(__in HINSTANCE hInst, __in ULONG ulReason, __in LPVOID)
{
    switch (ulReason)
    {
    case DLL_PROCESS_ATTACH:
        WcaGlobalInitialize(hInst);
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
