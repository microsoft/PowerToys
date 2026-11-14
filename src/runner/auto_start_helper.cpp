#include "pch.h"
#include "auto_start_helper.h"
#include "auto_start_task.h"

#include <Lmcons.h>

#include <comdef.h>
#include <taskschd.h>
#include <sddl.h>
#include <common/logger/logger.h>

// Helper macros from wix.
#define ExitOnFailure(x, s, ...) \
    if (FAILED(x))               \
    {                            \
        Logger::error(s, ##__VA_ARGS__); \
        goto LExit;              \
    }
#define ExitWithLastError(x, s, ...)       \
    {                                      \
        DWORD util_err = ::GetLastError(); \
        x = HRESULT_FROM_WIN32(util_err);  \
        if (!FAILED(x))                    \
        {                                  \
            x = E_FAIL;                    \
        }                                  \
        Logger::error(s, ##__VA_ARGS__);   \
        goto LExit;                        \
    }
#define ExitFunction() \
    {                  \
        goto LExit;    \
    }

const DWORD USERNAME_DOMAIN_LEN = DNLEN + UNLEN + 2; // Domain Name + '\' + User Name + '\0'
const DWORD USERNAME_LEN = UNLEN + 1; // User Name + '\0'

// Builds the security descriptor (SDDL) used when registering the autorun task.
// The task runs the PowerToys executable at logon, potentially elevated, so its
// definition must not be writable by arbitrary local users. We grant full access
// only to Local System, the Administrators group and the task's own user (so the
// non-elevated runner can still enable/disable/delete its own task). We must never
// grant write access to "Everyone", which would let any local user overwrite the
// task's action and achieve local privilege escalation.
static std::wstring get_auto_start_task_sddl()
{
    // Local System and Administrators always get full control.
    std::wstring sddl = L"D:(A;;FA;;;SY)(A;;FA;;;BA)";

    wil::unique_handle hToken;
    if (OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, hToken.put()))
    {
        BYTE tokenInfo[sizeof(TOKEN_USER) + SECURITY_MAX_SID_SIZE];
        DWORD tokenInfoLength = 0;
        if (GetTokenInformation(hToken.get(), TokenUser, tokenInfo, sizeof(tokenInfo), &tokenInfoLength))
        {
            auto pTokenUser = reinterpret_cast<TOKEN_USER*>(tokenInfo);
            wil::unique_hlocal_string stringSid;
            if (ConvertSidToStringSidW(pTokenUser->User.Sid, stringSid.put()))
            {
                sddl += L"(A;;FA;;;";
                sddl += stringSid.get();
                sddl += L")";
            }
        }
    }

    return sddl;
}

static std::wstring bstr_to_wstring(const _bstr_t& value)
{
    return value.length() ? static_cast<const wchar_t*>(value) : L"";
}

static std::optional<auto_start_task::RegisteredTask> read_registered_task(IRegisteredTask* pRegisteredTask)
{
    winrt::com_ptr<ITaskDefinition> definition;
    winrt::com_ptr<IPrincipal> principal;
    TASK_RUNLEVEL_TYPE runLevel = TASK_RUNLEVEL_LUA;
    _bstr_t securityDescriptor;
    if (FAILED(pRegisteredTask->get_Definition(definition.put())) ||
        FAILED(definition->get_Principal(principal.put())) ||
        FAILED(principal->get_RunLevel(&runLevel)) ||
        FAILED(pRegisteredTask->GetSecurityDescriptor(DACL_SECURITY_INFORMATION, securityDescriptor.GetAddress())))
    {
        return std::nullopt;
    }

    auto_start_task::RegisteredTask task{
        .runElevated = runLevel == TASK_RUNLEVEL_HIGHEST,
        .securityDescriptor = bstr_to_wstring(securityDescriptor),
    };

    winrt::com_ptr<IActionCollection> actions;
    winrt::com_ptr<IAction> action;
    winrt::com_ptr<IExecAction> execAction;
    LONG actionCount = 0;
    _bstr_t path;
    if (SUCCEEDED(definition->get_Actions(actions.put())) &&
        SUCCEEDED(actions->get_Count(&actionCount)) && actionCount == 1 &&
        SUCCEEDED(actions->get_Item(1, action.put())) &&
        SUCCEEDED(action->QueryInterface(IID_IExecAction, execAction.put_void())) &&
        SUCCEEDED(execAction->get_Path(path.GetAddress())))
    {
        task.executablePath = bstr_to_wstring(path);
    }

    winrt::com_ptr<ITriggerCollection> triggers;
    LONG triggerCount = 0;
    if (SUCCEEDED(definition->get_Triggers(triggers.put())) && SUCCEEDED(triggers->get_Count(&triggerCount)))
    {
        for (LONG i = 1; i <= triggerCount && !task.logonTriggerDelay; ++i)
        {
            winrt::com_ptr<ITrigger> trigger;
            winrt::com_ptr<ILogonTrigger> logonTrigger;
            TASK_TRIGGER_TYPE2 type{};
            VARIANT_BOOL enabled = VARIANT_FALSE;
            _bstr_t delay;
            if (SUCCEEDED(triggers->get_Item(i, trigger.put())) &&
                SUCCEEDED(trigger->get_Type(&type)) && type == TASK_TRIGGER_LOGON &&
                SUCCEEDED(trigger->get_Enabled(&enabled)) && enabled == VARIANT_TRUE &&
                SUCCEEDED(trigger->QueryInterface(IID_ILogonTrigger, logonTrigger.put_void())) &&
                SUCCEEDED(logonTrigger->get_Delay(delay.GetAddress())))
            {
                task.logonTriggerDelay = bstr_to_wstring(delay);
            }
        }
    }

    return task;
}

bool create_auto_start_task_for_this_user(bool runElevated)
{
    HRESULT hr = S_OK;

    WCHAR username_domain[USERNAME_DOMAIN_LEN];
    WCHAR username[USERNAME_LEN];

    std::wstring wstrTaskName;
    const std::wstring sddl = get_auto_start_task_sddl();
    std::optional<std::wstring> logonTriggerDelay;

    ITaskService* pService = NULL;
    ITaskFolder* pTaskFolder = NULL;
    ITaskDefinition* pTask = NULL;
    IRegistrationInfo* pRegInfo = NULL;
    ITaskSettings* pSettings = NULL;
    ITriggerCollection* pTriggerCollection = NULL;
    IRegisteredTask* pRegisteredTask = NULL;

    // ------------------------------------------------------
    // Get the Domain/Username for the trigger.
    if (!GetEnvironmentVariable(L"USERNAME", username, USERNAME_LEN))
    {
        ExitWithLastError(hr, "Getting username failed: {:x}", hr);
    }
    if (!GetEnvironmentVariable(L"USERDOMAIN", username_domain, USERNAME_DOMAIN_LEN))
    {
        ExitWithLastError(hr, "Getting the user's domain failed: {:x}", hr);
    }
    wcscat_s(username_domain, L"\\");
    wcscat_s(username_domain, username);

    // Task Name.
    wstrTaskName = L"Autorun for ";
    wstrTaskName += username;

    // Get the executable path passed to the custom action.
    WCHAR wszExecutablePath[MAX_PATH];
    GetModuleFileName(NULL, wszExecutablePath, MAX_PATH);

    // ------------------------------------------------------
    // Create an instance of the Task Service.
    hr = CoCreateInstance(CLSID_TaskScheduler,
                          NULL,
                          CLSCTX_INPROC_SERVER,
                          IID_ITaskService,
                          reinterpret_cast<void**>(&pService));
    ExitOnFailure(hr, "Failed to create an instance of ITaskService: {:x}", hr);

    // Connect to the task service.
    hr = pService->Connect(_variant_t(), _variant_t(), _variant_t(), _variant_t());
    ExitOnFailure(hr, "ITaskService::Connect failed: {:x}", hr);

    // ------------------------------------------------------
    // Get the PowerToys task folder. Creates it if it doesn't exist.
    hr = pService->GetFolder(_bstr_t(L"\\PowerToys"), &pTaskFolder);
    if (FAILED(hr))
    {
        // Folder doesn't exist. Get the Root folder and create the PowerToys subfolder.
        ITaskFolder* pRootFolder = NULL;
        hr = pService->GetFolder(_bstr_t(L"\\"), &pRootFolder);
        ExitOnFailure(hr, "Cannot get Root Folder pointer: {:x}", hr);
        hr = pRootFolder->CreateFolder(_bstr_t(L"\\PowerToys"), _variant_t(L""), &pTaskFolder);
        if (FAILED(hr))
        {
            pRootFolder->Release();
            ExitOnFailure(hr, "Cannot create PowerToys task folder: {:x}", hr);
        }
    }

    {
        IRegisteredTask* pExistingRegisteredTask = NULL;
        hr = pTaskFolder->GetTask(_bstr_t(wstrTaskName.c_str()), &pExistingRegisteredTask);
        if (SUCCEEDED(hr))
        {
            const auto existingTask = read_registered_task(pExistingRegisteredTask);
            pExistingRegisteredTask->Release();

            const bool isTrusted = existingTask && auto_start_task::grants_access_only_to(existingTask->securityDescriptor, sddl);
            if (isTrusted)
            {
                logonTriggerDelay = existingTask->logonTriggerDelay;
            }

            if (!isTrusted || !auto_start_task::matches(*existingTask, wszExecutablePath, runElevated))
            {
                Logger::info(L"The autorun task doesn't match the current settings, recreating it.");
                hr = pTaskFolder->DeleteTask(_bstr_t(wstrTaskName.c_str()), 0);
            }
        }
    }

    // If the task exists, just enable it.
    {
        IRegisteredTask* pExistingRegisteredTask = NULL;
        hr = pTaskFolder->GetTask(_bstr_t(wstrTaskName.c_str()), &pExistingRegisteredTask);
        if (SUCCEEDED(hr))
        {
            // Task exists, try enabling it.
            hr = pExistingRegisteredTask->put_Enabled(VARIANT_TRUE);
            pExistingRegisteredTask->Release();
            if (SUCCEEDED(hr))
            {
                // Function enable. Sounds like a success.
                ExitFunction();
            }
        }
    }

    // Create the task builder object to create the task.
    hr = pService->NewTask(0, &pTask);
    ExitOnFailure(hr, "Failed to create a task definition: {:x}", hr);

    // ------------------------------------------------------
    // Get the registration info for setting the identification.
    hr = pTask->get_RegistrationInfo(&pRegInfo);
    ExitOnFailure(hr, "Cannot get identification pointer: {:x}", hr);
    hr = pRegInfo->put_Author(_bstr_t(username_domain));
    ExitOnFailure(hr, "Cannot put identification info: {:x}", hr);

    // ------------------------------------------------------
    // Create the settings for the task
    hr = pTask->get_Settings(&pSettings);
    ExitOnFailure(hr, "Cannot get settings pointer: {:x}", hr);

    hr = pSettings->put_StartWhenAvailable(VARIANT_FALSE);
    ExitOnFailure(hr, "Cannot put_StartWhenAvailable setting info: {:x}", hr);
    hr = pSettings->put_StopIfGoingOnBatteries(VARIANT_FALSE);
    ExitOnFailure(hr, "Cannot put_StopIfGoingOnBatteries setting info: {:x}", hr);
    hr = pSettings->put_ExecutionTimeLimit(_bstr_t(L"PT0S")); //Unlimited
    ExitOnFailure(hr, "Cannot put_ExecutionTimeLimit setting info: {:x}", hr);
    hr = pSettings->put_DisallowStartIfOnBatteries(VARIANT_FALSE);
    ExitOnFailure(hr, "Cannot put_DisallowStartIfOnBatteries setting info: {:x}", hr);
    hr = pSettings->put_Priority(4);
    ExitOnFailure(hr, "Cannot put_Priority setting info : {:x}", hr);

    // ------------------------------------------------------
    // Get the trigger collection to insert the logon trigger.
    hr = pTask->get_Triggers(&pTriggerCollection);
    ExitOnFailure(hr, "Cannot get trigger collection: {:x}", hr);

    // Add the logon trigger to the task.
    {
        ITrigger* pTrigger = NULL;
        ILogonTrigger* pLogonTrigger = NULL;
        hr = pTriggerCollection->Create(TASK_TRIGGER_LOGON, &pTrigger);
        ExitOnFailure(hr, "Cannot create the trigger: {:x}", hr);

        hr = pTrigger->QueryInterface(
            IID_ILogonTrigger, reinterpret_cast<void**>(&pLogonTrigger));
        pTrigger->Release();
        ExitOnFailure(hr, "QueryInterface call failed for ILogonTrigger: {:x}", hr);

        hr = pLogonTrigger->put_Id(_bstr_t(L"Trigger1"));

        // Timing issues may make explorer not be started when the task runs.
        // Add a little delay to mitigate this.
        hr = pLogonTrigger->put_Delay(_bstr_t(logonTriggerDelay.value_or(L"PT03S").c_str()));

        // Define the user. The task will execute when the user logs on.
        // The specified user must be a user on this computer.
        hr = pLogonTrigger->put_UserId(_bstr_t(username_domain));
        pLogonTrigger->Release();
        ExitOnFailure(hr, "Cannot add user ID to logon trigger: {:x}", hr);
    }

    // ------------------------------------------------------
    // Add an Action to the task. This task will execute the path passed to this custom action.
    {
        IActionCollection* pActionCollection = NULL;
        IAction* pAction = NULL;
        IExecAction* pExecAction = NULL;

        // Get the task action collection pointer.
        hr = pTask->get_Actions(&pActionCollection);
        ExitOnFailure(hr, "Cannot get Task collection pointer: {:x}", hr);

        // Create the action, specifying that it is an executable action.
        hr = pActionCollection->Create(TASK_ACTION_EXEC, &pAction);
        pActionCollection->Release();
        ExitOnFailure(hr, "Cannot create the action: {:x}", hr);

        // QI for the executable task pointer.
        hr = pAction->QueryInterface(
            IID_IExecAction, reinterpret_cast<void**>(&pExecAction));
        pAction->Release();
        ExitOnFailure(hr, "QueryInterface call failed for IExecAction: {:x}", hr);

        // Set the path of the executable to PowerToys (passed as CustomActionData).
        hr = pExecAction->put_Path(_bstr_t(wszExecutablePath));
        pExecAction->Release();
        ExitOnFailure(hr, "Cannot set path of executable: {:x}", hr);
    }

    // ------------------------------------------------------
    // Create the principal for the task
    {
        IPrincipal* pPrincipal = NULL;
        hr = pTask->get_Principal(&pPrincipal);
        ExitOnFailure(hr, "Cannot get principal pointer: {:x}", hr);

        // Set up principal information:
        hr = pPrincipal->put_Id(_bstr_t(L"Principal1"));

        hr = pPrincipal->put_UserId(_bstr_t(username_domain));

        hr = pPrincipal->put_LogonType(TASK_LOGON_INTERACTIVE_TOKEN);

        if (runElevated)
        {
            hr = pPrincipal->put_RunLevel(_TASK_RUNLEVEL::TASK_RUNLEVEL_HIGHEST);
        }
        else
        {
            hr = pPrincipal->put_RunLevel(_TASK_RUNLEVEL::TASK_RUNLEVEL_LUA);
        }
        pPrincipal->Release();
        ExitOnFailure(hr, "Cannot put principal run level: {:x}", hr);
    }
    // ------------------------------------------------------
    //  Save the task in the PowerToys folder.
    {
        _variant_t taskSecurityDescriptor = sddl.c_str();
        hr = pTaskFolder->RegisterTaskDefinition(
            _bstr_t(wstrTaskName.c_str()),
            pTask,
            TASK_CREATE_OR_UPDATE,
            _variant_t(username_domain),
            _variant_t(),
            TASK_LOGON_INTERACTIVE_TOKEN,
            taskSecurityDescriptor,
            &pRegisteredTask);
        ExitOnFailure(hr, "Error saving the Task : {:x}", hr);
    }

LExit:
    if (pService)
        pService->Release();
    if (pTaskFolder)
        pTaskFolder->Release();
    if (pTask)
        pTask->Release();
    if (pRegInfo)
        pRegInfo->Release();
    if (pSettings)
        pSettings->Release();
    if (pTriggerCollection)
        pTriggerCollection->Release();
    if (pRegisteredTask)
        pRegisteredTask->Release();

    return (SUCCEEDED(hr));
}

bool delete_auto_start_task_for_this_user()
{
    HRESULT hr = S_OK;

    WCHAR username[USERNAME_LEN];
    std::wstring wstrTaskName;

    ITaskService* pService = NULL;
    ITaskFolder* pTaskFolder = NULL;

    // ------------------------------------------------------
    // Get the Username for the task.
    if (!GetEnvironmentVariable(L"USERNAME", username, USERNAME_LEN))
    {
        ExitWithLastError(hr, "Getting username failed: {:x}", hr);
    }

    // Task Name.
    wstrTaskName = L"Autorun for ";
    wstrTaskName += username;

    // ------------------------------------------------------
    // Create an instance of the Task Service.
    hr = CoCreateInstance(CLSID_TaskScheduler,
                          NULL,
                          CLSCTX_INPROC_SERVER,
                          IID_ITaskService,
                          reinterpret_cast<void**>(&pService));
    ExitOnFailure(hr, "Failed to create an instance of ITaskService: {:x}", hr);

    // Connect to the task service.
    hr = pService->Connect(_variant_t(), _variant_t(), _variant_t(), _variant_t());
    ExitOnFailure(hr, "ITaskService::Connect failed: {:x}", hr);

    // ------------------------------------------------------
    // Get the PowerToys task folder.
    hr = pService->GetFolder(_bstr_t(L"\\PowerToys"), &pTaskFolder);
    if (FAILED(hr))
    {
        // Folder doesn't exist. No need to disable a non-existing task.
        hr = S_OK;
        ExitFunction();
    }

    // ------------------------------------------------------
    // If the task exists, disable.
    {
        IRegisteredTask* pExistingRegisteredTask = NULL;
        hr = pTaskFolder->GetTask(_bstr_t(wstrTaskName.c_str()), &pExistingRegisteredTask);
        if (SUCCEEDED(hr))
        {
            // Task exists, try disabling it.
            hr = pTaskFolder->DeleteTask(_bstr_t(wstrTaskName.c_str()), 0);
        }
    }

LExit:
    if (pService)
        pService->Release();
    if (pTaskFolder)
        pTaskFolder->Release();

    return (SUCCEEDED(hr));
}

bool is_auto_start_task_active_for_this_user()
{
    HRESULT hr = S_OK;

    WCHAR username[USERNAME_LEN];
    std::wstring wstrTaskName;

    ITaskService* pService = NULL;
    ITaskFolder* pTaskFolder = NULL;

    // ------------------------------------------------------
    // Get the Username for the task.
    if (!GetEnvironmentVariable(L"USERNAME", username, USERNAME_LEN))
    {
        ExitWithLastError(hr, "Getting username failed: {:x}", hr);
    }

    // Task Name.
    wstrTaskName = L"Autorun for ";
    wstrTaskName += username;

    // ------------------------------------------------------
    // Create an instance of the Task Service.
    hr = CoCreateInstance(CLSID_TaskScheduler,
                          NULL,
                          CLSCTX_INPROC_SERVER,
                          IID_ITaskService,
                          reinterpret_cast<void**>(&pService));
    ExitOnFailure(hr, "Failed to create an instance of ITaskService: {:x}", hr);

    // Connect to the task service.
    hr = pService->Connect(_variant_t(), _variant_t(), _variant_t(), _variant_t());
    ExitOnFailure(hr, "ITaskService::Connect failed: {:x}", hr);

    // ------------------------------------------------------
    // Get the PowerToys task folder.
    hr = pService->GetFolder(_bstr_t(L"\\PowerToys"), &pTaskFolder);
    ExitOnFailure(hr, "ITaskFolder doesn't exist: {:x}", hr);

    // ------------------------------------------------------
    // If the task exists, disable.
    {
        IRegisteredTask* pExistingRegisteredTask = NULL;
        hr = pTaskFolder->GetTask(_bstr_t(wstrTaskName.c_str()), &pExistingRegisteredTask);
        if (SUCCEEDED(hr))
        {
            // Task exists, get its value.
            VARIANT_BOOL is_enabled;
            hr = pExistingRegisteredTask->get_Enabled(&is_enabled);
            pExistingRegisteredTask->Release();
            if (SUCCEEDED(hr))
            {
                // Got the value. Return it.
                hr = (is_enabled == VARIANT_TRUE) ? S_OK : E_FAIL; // Fake success or fail to return the value.
                ExitFunction();
            }
        }
    }

LExit:
    if (pService)
        pService->Release();
    if (pTaskFolder)
        pTaskFolder->Release();

    return (SUCCEEDED(hr));
}
