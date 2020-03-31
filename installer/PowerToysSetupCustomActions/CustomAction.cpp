#include "stdafx.h"

#define SECURITY_WIN32
#include <Security.h>
#pragma comment(lib, "Secur32.lib")
#include <Lmcons.h>

#include <comdef.h>
#include <taskschd.h>
#pragma comment(lib, "taskschd.lib")
#pragma comment(lib, "comsupp.lib")

#include <iostream>
#include <strutil.h>
#include <ProjectTelemetry.h>

using namespace std;

TRACELOGGING_DEFINE_PROVIDER(
  g_hProvider,
  "Microsoft.PowerToysInstaller",
  // {e1d8165d-5cb6-5c74-3b51-bdfbfe4f7a3b}
  (0xe1d8165d, 0x5cb6, 0x5c74, 0x3b, 0x51, 0xbd, 0xfb, 0xfe, 0x4f, 0x7a, 0x3b),
  TraceLoggingOptionProjectTelemetry());

const DWORD USERNAME_DOMAIN_LEN = DNLEN + UNLEN + 2; // Domain Name + '\' + User Name + '\0'
const DWORD USERNAME_LEN = UNLEN + 1; // User Name + '\0'

// Creates a Scheduled Task to run at logon for the current user.
// The path of the executable to run should be passed as the CustomActionData (Value).
// Based on the Task Scheduler Logon Trigger Example:
// https://docs.microsoft.com/en-us/windows/win32/taskschd/logon-trigger-example--c---/
UINT __stdcall CreateScheduledTaskCA(MSIHANDLE hInstall) {
  HRESULT hr = S_OK;
  UINT er = ERROR_SUCCESS;

  TCHAR username_domain[USERNAME_DOMAIN_LEN];
  TCHAR username[USERNAME_LEN];

  std::wstring wstrTaskName;

  ITaskService *pService = NULL;
  ITaskFolder *pTaskFolder = NULL;
  ITaskDefinition *pTask = NULL;
  IRegistrationInfo *pRegInfo = NULL;
  ITaskSettings *pSettings = NULL;
  ITriggerCollection *pTriggerCollection = NULL;
  IRegisteredTask *pRegisteredTask = NULL;

  hr = WcaInitialize(hInstall, "CreateScheduledTaskCA");
  ExitOnFailure(hr, "Failed to initialize");

  WcaLog(LOGMSG_STANDARD, "Initialized.");

  // ------------------------------------------------------
  // Get the Domain/Username for the trigger.
  //
  // This action needs to run as the system to get elevated privileges from the installation,
  // so GetUserNameEx can't be used to get the current user details.
  // The USERNAME and USERDOMAIN environment variables are used instead.
  if (!GetEnvironmentVariable(L"USERNAME", username, USERNAME_LEN)) {
    ExitWithLastError(hr, "Getting username failed: %x", hr);
  }
  if (!GetEnvironmentVariable(L"USERDOMAIN", username_domain, USERNAME_DOMAIN_LEN)) {
    ExitWithLastError(hr, "Getting the user's domain failed: %x", hr);
  }
  wcscat_s(username_domain, L"\\");
  wcscat_s(username_domain, username);

  WcaLog(LOGMSG_STANDARD, "Current user detected: %ls", username_domain);

  // Task Name.
  wstrTaskName = L"Autorun for ";
  wstrTaskName += username;

  // Get the executable path passed to the custom action.
  LPWSTR wszExecutablePath = NULL;
  hr = WcaGetProperty(L"CustomActionData", &wszExecutablePath);
  ExitOnFailure(hr, "Failed to get the executable path from CustomActionData.");

  // COM and Security Initialization is expected to have been done by the MSI.
  // It couldn't be done in the DLL, anyway.
  // ------------------------------------------------------
  // Create an instance of the Task Service.
  hr = CoCreateInstance(CLSID_TaskScheduler,
    NULL,
    CLSCTX_INPROC_SERVER,
    IID_ITaskService,
    (void**)&pService);
  ExitOnFailure(hr, "Failed to create an instance of ITaskService: %x", hr);

  // Connect to the task service.
  hr = pService->Connect(_variant_t(), _variant_t(),
    _variant_t(), _variant_t());
  ExitOnFailure(hr, "ITaskService::Connect failed: %x", hr);

  // ------------------------------------------------------
  // Get the PowerToys task folder. Creates it if it doesn't exist.
  hr = pService->GetFolder(_bstr_t(L"\\PowerToys"), &pTaskFolder);
  if (FAILED(hr)) {
    // Folder doesn't exist. Get the Root folder and create the PowerToys subfolder.
    ITaskFolder *pRootFolder = NULL;
    hr = pService->GetFolder(_bstr_t(L"\\"), &pRootFolder);
    ExitOnFailure(hr, "Cannot get Root Folder pointer: %x", hr);
    hr = pRootFolder->CreateFolder(_bstr_t(L"\\PowerToys"), _variant_t(L""), &pTaskFolder);
    if (FAILED(hr)) {
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
  ITrigger *pTrigger = NULL;
  hr = pTriggerCollection->Create(TASK_TRIGGER_LOGON, &pTrigger);
  ExitOnFailure(hr, "Cannot create the trigger: %x", hr);

  ILogonTrigger *pLogonTrigger = NULL;
  hr = pTrigger->QueryInterface(
    IID_ILogonTrigger, (void**)&pLogonTrigger);
  pTrigger->Release();
  ExitOnFailure(hr, "QueryInterface call failed for ILogonTrigger: %x", hr);

  hr = pLogonTrigger->put_Id(_bstr_t(L"Trigger1"));
  if (FAILED(hr)) {
    WcaLogError(hr, "Cannot put the trigger ID: %x", hr);
  }

  // Timing issues may make explorer not be started when the task runs.
  // Add a little delay to mitigate this.
  hr = pLogonTrigger->put_Delay(_bstr_t(L"PT03S"));
  if (FAILED(hr)) {
    WcaLogError(hr, "Cannot put the trigger delay: %x", hr);
  }

  // Define the user. The task will execute when the user logs on.
  // The specified user must be a user on this computer.
  hr = pLogonTrigger->put_UserId(_bstr_t(username_domain));
  pLogonTrigger->Release();
  ExitOnFailure(hr, "Cannot add user ID to logon trigger: %x", hr);

  // ------------------------------------------------------
  // Add an Action to the task. This task will execute the path passed to this custom action.
  IActionCollection *pActionCollection = NULL;

  // Get the task action collection pointer.
  hr = pTask->get_Actions(&pActionCollection);
  ExitOnFailure(hr, "Cannot get Task collection pointer: %x", hr);

  // Create the action, specifying that it is an executable action.
  IAction *pAction = NULL;
  hr = pActionCollection->Create(TASK_ACTION_EXEC, &pAction);
  pActionCollection->Release();
  ExitOnFailure(hr, "Cannot create the action: %x", hr);

  IExecAction *pExecAction = NULL;
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
  IPrincipal *pPrincipal = NULL;
  hr = pTask->get_Principal(&pPrincipal);
  ExitOnFailure(hr, "Cannot get principal pointer: %x", hr);

  // Set up principal information:
  hr = pPrincipal->put_Id(_bstr_t(L"Principal1"));
  if (FAILED(hr)) {
    WcaLogError(hr, "Cannot put the principal ID: %x", hr);
  }

  hr = pPrincipal->put_UserId(_bstr_t(username_domain));
  if (FAILED(hr)) {
    WcaLogError(hr, "Cannot put principal user Id: %x", hr);
  }

  hr = pPrincipal->put_LogonType(TASK_LOGON_INTERACTIVE_TOKEN);
  if (FAILED(hr)) {
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
  if (pService) pService->Release();
  if (pTaskFolder) pTaskFolder->Release();
  if (pTask) pTask->Release();
  if (pRegInfo) pRegInfo->Release();
  if (pSettings) pSettings->Release();
  if (pTriggerCollection) pTriggerCollection->Release();
  if (pRegisteredTask) pRegisteredTask->Release();

  if (!SUCCEEDED(hr)) {
    PMSIHANDLE hRecord = MsiCreateRecord(0);
    MsiRecordSetString(hRecord, 0, TEXT("Failed to create a scheduled task to start PowerToys at user login. You can re-try to create the scheduled task using the PowerToys settings."));
    MsiProcessMessage(hInstall, INSTALLMESSAGE(INSTALLMESSAGE_WARNING + MB_OK), hRecord);
  }

  er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
  return WcaFinalize(er);
}

// Removes all Scheduled Tasks in the PowerToys folder and deletes the folder afterwards.
// Based on the Task Scheduler Displaying Task Names and State example:
// https://docs.microsoft.com/en-us/windows/desktop/TaskSchd/displaying-task-names-and-state--c---/
UINT __stdcall RemoveScheduledTasksCA(MSIHANDLE hInstall) {
  HRESULT hr = S_OK;
  UINT er = ERROR_SUCCESS;

  ITaskService *pService = NULL;
  ITaskFolder *pTaskFolder = NULL;
  IRegisteredTaskCollection* pTaskCollection = NULL;

  hr = WcaInitialize(hInstall, "RemoveScheduledTasksCA");
  ExitOnFailure(hr, "Failed to initialize");

  WcaLog(LOGMSG_STANDARD, "Initialized.");

  // COM and Security Initialization is expected to have been done by the MSI.
  // It couldn't be done in the DLL, anyway.
  // ------------------------------------------------------
  // Create an instance of the Task Service.
  hr = CoCreateInstance(CLSID_TaskScheduler,
    NULL,
    CLSCTX_INPROC_SERVER,
    IID_ITaskService,
    (void**)&pService);
  ExitOnFailure(hr, "Failed to create an instance of ITaskService: %x", hr);

  // Connect to the task service.
  hr = pService->Connect(_variant_t(), _variant_t(),
    _variant_t(), _variant_t());
  ExitOnFailure(hr, "ITaskService::Connect failed: %x", hr);

  // ------------------------------------------------------
  // Get the PowerToys task folder.
  hr = pService->GetFolder(_bstr_t(L"\\PowerToys"), &pTaskFolder);
  if (FAILED(hr)) {
    // Folder doesn't exist. No need to delete anything.
    WcaLog(LOGMSG_STANDARD, "The PowerToys scheduled task folder wasn't found. Nothing to delete.");
    hr = S_OK;
    ExitFunction();
  }

  // -------------------------------------------------------
  // Get the registered tasks in the folder.
  hr = pTaskFolder->GetTasks(TASK_ENUM_HIDDEN, &pTaskCollection);
  ExitOnFailure(hr, "Cannot get the registered tasks: %x", hr);

  LONG numTasks = 0;
  hr = pTaskCollection->get_Count(&numTasks);
  for (LONG i = 0; i < numTasks; i++) {
    // Delete all the tasks found.
    // If some tasks can't be deleted, the folder won't be deleted later and the user will still be notified.
    IRegisteredTask* pRegisteredTask = NULL;
    hr = pTaskCollection->get_Item(_variant_t(i + 1), &pRegisteredTask);
    if (SUCCEEDED(hr)) {
      BSTR taskName = NULL;
      hr = pRegisteredTask->get_Name(&taskName);
      if (SUCCEEDED(hr)) {
        hr = pTaskFolder->DeleteTask(taskName, NULL);
        if (FAILED(hr)) {
          WcaLogError(hr, "Cannot delete the '%S' task: %x", taskName, hr);
        }
        SysFreeString(taskName);
      } else {
        WcaLogError(hr, "Cannot get the registered task name: %x", hr);
      }
      pRegisteredTask->Release();
    } else {
      WcaLogError(hr, "Cannot get the registered task item at index=%d: %x", i + 1, hr);
    }
  }

  // ------------------------------------------------------
  // Get the pointer to the root task folder and delete the PowerToys subfolder.
  ITaskFolder *pRootFolder = NULL;
  hr = pService->GetFolder(_bstr_t(L"\\"), &pRootFolder);
  ExitOnFailure(hr, "Cannot get Root Folder pointer: %x", hr);
  hr = pRootFolder->DeleteFolder(_bstr_t(L"PowerToys"), NULL);
  pRootFolder->Release();
  ExitOnFailure(hr, "Cannot delete the PowerToys folder: %x", hr);

  WcaLog(LOGMSG_STANDARD, "Deleted the PowerToys Task Scheduler folder.");

LExit:
  if (pService) pService->Release();
  if (pTaskFolder) pTaskFolder->Release();
  if (pTaskCollection) pTaskCollection->Release();

  if (!SUCCEEDED(hr)) {
    PMSIHANDLE hRecord = MsiCreateRecord(0);
    MsiRecordSetString(hRecord, 0, TEXT("Failed to remove the PowerToys folder from the scheduled task. These can be removed manually later."));
    MsiProcessMessage(hInstall, INSTALLMESSAGE(INSTALLMESSAGE_WARNING + MB_OK), hRecord);
  }

  er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
  return WcaFinalize(er);
}

UINT __stdcall TelemetryLogInstallSuccessCA(MSIHANDLE hInstall) {
  HRESULT hr = S_OK;
  UINT er = ERROR_SUCCESS;

  hr = WcaInitialize(hInstall, "TelemetryLogInstallSuccessCA");
  ExitOnFailure(hr, "Failed to initialize");

  TraceLoggingWrite(
    g_hProvider,
    "Install_Success",
    ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
    TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
    TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));

LExit:
  er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
  return WcaFinalize(er);
}

UINT __stdcall TelemetryLogInstallCancelCA(MSIHANDLE hInstall) {
  HRESULT hr = S_OK;
  UINT er = ERROR_SUCCESS;

  hr = WcaInitialize(hInstall, "TelemetryLogInstallCancelCA");
  ExitOnFailure(hr, "Failed to initialize");

  TraceLoggingWrite(
    g_hProvider,
    "Install_Cancel",
    ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
    TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
    TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));

LExit:
  er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
  return WcaFinalize(er);
}

UINT __stdcall TelemetryLogInstallFailCA(MSIHANDLE hInstall) {
  HRESULT hr = S_OK;
  UINT er = ERROR_SUCCESS;

  hr = WcaInitialize(hInstall, "TelemetryLogInstallFailCA");
  ExitOnFailure(hr, "Failed to initialize");

  TraceLoggingWrite(
    g_hProvider,
    "Install_Fail",
    ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
    TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
    TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));

LExit:
  er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
  return WcaFinalize(er);
}

UINT __stdcall TelemetryLogUninstallSuccessCA(MSIHANDLE hInstall) {
  HRESULT hr = S_OK;
  UINT er = ERROR_SUCCESS;

  hr = WcaInitialize(hInstall, "TelemetryLogUninstallSuccessCA");
  ExitOnFailure(hr, "Failed to initialize");

  TraceLoggingWrite(
    g_hProvider,
    "UnInstall_Success",
    ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
    TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
    TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));

LExit:
  er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
  return WcaFinalize(er);
}

UINT __stdcall TelemetryLogUninstallCancelCA(MSIHANDLE hInstall) {
  HRESULT hr = S_OK;
  UINT er = ERROR_SUCCESS;

  hr = WcaInitialize(hInstall, "TelemetryLogUninstallCancelCA");
  ExitOnFailure(hr, "Failed to initialize");

  TraceLoggingWrite(
    g_hProvider,
    "UnInstall_Cancel",
    ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
    TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
    TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));

LExit:
  er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
  return WcaFinalize(er);
}

UINT __stdcall TelemetryLogUninstallFailCA(MSIHANDLE hInstall) {
  HRESULT hr = S_OK;
  UINT er = ERROR_SUCCESS;

  hr = WcaInitialize(hInstall, "TelemetryLogUninstallFailCA");
  ExitOnFailure(hr, "Failed to initialize");

  TraceLoggingWrite(
    g_hProvider,
    "UnInstall_Fail",
    ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
    TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
    TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));

LExit:
  er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
  return WcaFinalize(er);
}

UINT __stdcall TelemetryLogRepairCancelCA(MSIHANDLE hInstall) {
  HRESULT hr = S_OK;
  UINT er = ERROR_SUCCESS;

  hr = WcaInitialize(hInstall, "TelemetryLogRepairCancelCA");
  ExitOnFailure(hr, "Failed to initialize");

  TraceLoggingWrite(
    g_hProvider,
    "Repair_Cancel",
    ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
    TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
    TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));

LExit:
  er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
  return WcaFinalize(er);
}

UINT __stdcall TelemetryLogRepairFailCA(MSIHANDLE hInstall) {
  HRESULT hr = S_OK;
  UINT er = ERROR_SUCCESS;

  hr = WcaInitialize(hInstall, "TelemetryLogRepairFailCA");
  ExitOnFailure(hr, "Failed to initialize");

  TraceLoggingWrite(
    g_hProvider,
    "Repair_Fail",
    ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
    TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
    TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));

LExit:
  er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
  return WcaFinalize(er);
}

// DllMain - Initialize and cleanup WiX custom action utils.
extern "C" BOOL WINAPI DllMain(__in HINSTANCE hInst, __in ULONG ulReason, __in LPVOID) {
  switch (ulReason) {
  case DLL_PROCESS_ATTACH:
    WcaGlobalInitialize(hInst);
    TraceLoggingRegister(g_hProvider);
    break;

  case DLL_PROCESS_DETACH:
    TraceLoggingUnregister(g_hProvider);
    WcaGlobalFinalize();
    break;
  }

  return TRUE;
}
