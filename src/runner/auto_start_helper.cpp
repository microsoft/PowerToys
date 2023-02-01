#include "pch.h"
#include "auto_start_helper.h"

#include <Lmcons.h>

#include <comdef.h>
#include <taskschd.h>

// Helper macros from wix.
// TODO: use "s" and "..." parameters to report errors from these functions.
#define ExitOnFailure(x, s, ...) \
    if (FAILED(x))               \
    {                            \
        goto LExit;              \
    }
#define ExitWithLastError(x, s, ...)       \
    {                                      \
        DWORD Dutil_er = ::GetLastError(); \
        x = HRESULT_FROM_WIN32(Dutil_er);  \
        if (!FAILED(x))                    \
        {                                  \
            x = E_FAIL;                    \
        }                                  \
        goto LExit;                        \
    }
#define ExitFunction() \
    {                  \
        goto LExit;    \
    }

const DWORD USERNAME_DOMAIN_LEN = DNLEN + UNLEN + 2; // Domain Name + '\' + User Name + '\0'
const DWORD USERNAME_LEN = UNLEN + 1; // User Name + '\0'

bool create_auto_start_task_for_this_user(bool runElevated)
{
    HRESULT hr = S_OK;

    WCHAR username_domain[USERNAME_DOMAIN_LEN];
    WCHAR username[USERNAME_LEN];

    std::wstring wstrTaskName;

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
        ExitWithLastError(hr, "Getting username failed: %x", hr);
    }
    if (!GetEnvironmentVariable(L"USERDOMAIN", username_domain, USERNAME_DOMAIN_LEN))
    {
        ExitWithLastError(hr, "Getting the user's domain failed: %x", hr);
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
        ITaskFolder* pRootFolder = NULL;
        hr = pService->GetFolder(_bstr_t(L"\\"), &pRootFolder);
        ExitOnFailure(hr, "Cannot get Root Folder pointer: %x", hr);
        hr = pRootFolder->CreateFolder(_bstr_t(L"\\PowerToys"), _variant_t(L""), &pTaskFolder);
        if (FAILED(hr))
        {
            pRootFolder->Release();
            ExitOnFailure(hr, "Cannot create PowerToys task folder: %x", hr);
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
    {
        ITrigger* pTrigger = NULL;
        ILogonTrigger* pLogonTrigger = NULL;
        hr = pTriggerCollection->Create(TASK_TRIGGER_LOGON, &pTrigger);
        ExitOnFailure(hr, "Cannot create the trigger: %x", hr);

        hr = pTrigger->QueryInterface(
            IID_ILogonTrigger, (void**)&pLogonTrigger);
        pTrigger->Release();
        ExitOnFailure(hr, "QueryInterface call failed for ILogonTrigger: %x", hr);

        hr = pLogonTrigger->put_Id(_bstr_t(L"Trigger1"));

        // Timing issues may make explorer not be started when the task runs.
        // Add a little delay to mitigate this.
        hr = pLogonTrigger->put_Delay(_bstr_t(L"PT03S"));

        // Define the user. The task will execute when the user logs on.
        // The specified user must be a user on this computer.
        hr = pLogonTrigger->put_UserId(_bstr_t(username_domain));
        pLogonTrigger->Release();
        ExitOnFailure(hr, "Cannot add user ID to logon trigger: %x", hr);
    }

    // ------------------------------------------------------
    // Add an Action to the task. This task will execute the path passed to this custom action.
    {
        IActionCollection* pActionCollection = NULL;
        IAction* pAction = NULL;
        IExecAction* pExecAction = NULL;

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
    }

    // ------------------------------------------------------
    // Create the principal for the task
    {
        IPrincipal* pPrincipal = NULL;
        hr = pTask->get_Principal(&pPrincipal);
        ExitOnFailure(hr, "Cannot get principal pointer: %x", hr);

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
        ExitOnFailure(hr, "Cannot put principal run level: %x", hr);
    }
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
        ExitWithLastError(hr, "Getting username failed: %x", hr);
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
    ExitOnFailure(hr, "Failed to create an instance of ITaskService: %x", hr);

    // Connect to the task service.
    hr = pService->Connect(_variant_t(), _variant_t(), _variant_t(), _variant_t());
    ExitOnFailure(hr, "ITaskService::Connect failed: %x", hr);

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
        ExitWithLastError(hr, "Getting username failed: %x", hr);
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
    ExitOnFailure(hr, "Failed to create an instance of ITaskService: %x", hr);

    // Connect to the task service.
    hr = pService->Connect(_variant_t(), _variant_t(), _variant_t(), _variant_t());
    ExitOnFailure(hr, "ITaskService::Connect failed: %x", hr);

    // ------------------------------------------------------
    // Get the PowerToys task folder.
    hr = pService->GetFolder(_bstr_t(L"\\PowerToys"), &pTaskFolder);
    ExitOnFailure(hr, "ITaskFolder doesn't exist: %x", hr);

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
