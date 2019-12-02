#include "pch.h"
#include <WinSafer.h>
#include <Sddl.h>
#include <sstream>
#include <accctrl.h>
#include <aclapi.h>

#include "powertoy_module.h"
#include <common/two_way_pipe_message_ipc.h>
#include "tray_icon.h"
#include "general_settings.h"
#include "common/windows_colors.h"

#include <common/json.h>

#define BUFSIZE 1024

TwoWayPipeMessageIPC* current_settings_ipc = NULL;

json::JsonObject get_power_toys_settings() {
  json::JsonObject result;
  for (const auto&[name, powertoy] : modules()) {
    try {
      result.SetNamedValue(name, powertoy.json_config());
    }
    catch (...) {
      // TODO: handle malformed JSON.
    }
  }
  return result;
}

json::JsonObject get_all_settings() {
  json::JsonObject result;

  result.SetNamedValue(L"general", get_general_settings());
  result.SetNamedValue(L"powertoys", get_power_toys_settings());
  return result;
}

void dispatch_json_action_to_module(const json::JsonObject& powertoys_configs) {
  for (const auto& powertoy_element : powertoys_configs) {
    const std::wstring name{powertoy_element.Key().c_str()};
    if (modules().find(name) != modules().end()) {
      const auto element = powertoy_element.Value().Stringify();
      modules().at(name).call_custom_action(element.c_str());
    }
  }
}

void send_json_config_to_module(const std::wstring& module_key, const std::wstring& settings) {
  if (modules().find(module_key) != modules().end()) {
    modules().at(module_key).set_config(settings.c_str());
  }
}

void dispatch_json_config_to_modules(const json::JsonObject& powertoys_configs) {
  for (const auto& powertoy_element : powertoys_configs) {
    const auto element = powertoy_element.Value().Stringify();
    send_json_config_to_module(powertoy_element.Key().c_str(), element.c_str());
  }
};

void dispatch_received_json(const std::wstring &json_to_parse) {
  const json::JsonObject j = json::JsonObject::Parse(json_to_parse);
  for(const auto & base_element : j) {
    const auto name = base_element.Key();
    const auto value = base_element.Value();

    if (name == L"action") {
      dispatch_json_action_to_module(value.GetObjectW());
      continue;
    }

    if (name == L"general") {
      apply_general_settings(value.GetObjectW());
    }  
    else if (name == L"powertoys") {
      dispatch_json_config_to_modules(value.GetObjectW());
    } 
    else if (name != L"refresh") {
      continue;
    }
    if (current_settings_ipc != nullptr) {
      const std::wstring settings_string{get_all_settings().Stringify().c_str()};
      current_settings_ipc->send(settings_string);
    }
  }
  return;
}

void dispatch_received_json_callback(PVOID data) {
  std::wstring* msg = (std::wstring*)data;
  dispatch_received_json(*msg);
  delete msg;
}

void receive_json_send_to_main_thread(const std::wstring &msg) {
  std::wstring* copy = new std::wstring(msg);
  dispatch_run_on_main_ui_thread(dispatch_received_json_callback, copy);
}

DWORD g_settings_process_id = 0;

void run_settings_window() {
  STARTUPINFO startup_info = { sizeof(startup_info) };
  PROCESS_INFORMATION process_info = { 0 };
  HANDLE process = NULL;
  HANDLE hToken = NULL;
  STARTUPINFOEX siex = { 0 };
  PPROC_THREAD_ATTRIBUTE_LIST pptal = NULL;
  WCHAR executable_path[MAX_PATH];
  GetModuleFileName(NULL, executable_path, MAX_PATH);
  PathRemoveFileSpec(executable_path);
  wcscat_s(executable_path, L"\\PowerToysSettings.exe");
  WCHAR executable_args[MAX_PATH * 3];

  const std::wstring settings_theme_setting{get_general_settings().GetNamedString(L"theme").c_str()};
  std::wstring settings_theme;
  if (settings_theme_setting == L"dark" || (settings_theme_setting == L"system" && WindowsColors::is_dark_mode())) {
    settings_theme = L" dark"; // Include arg separating space
  }
  // Generate unique names for the pipes, if getting a UUID is possible
  std::wstring powertoys_pipe_name(L"\\\\.\\pipe\\powertoys_runner_");
  std::wstring settings_pipe_name(L"\\\\.\\pipe\\powertoys_settings_");
  SIZE_T size = 0;
  UUID temp_uuid;
  UuidCreate(&temp_uuid);
  wchar_t* uuid_chars;
  UuidToString(&temp_uuid, (RPC_WSTR*)&uuid_chars);
  if (uuid_chars != NULL) {
    powertoys_pipe_name += std::wstring(uuid_chars);
    settings_pipe_name += std::wstring(uuid_chars);
    RpcStringFree((RPC_WSTR*)&uuid_chars);
    uuid_chars = NULL;
  }
  DWORD powertoys_pid = GetCurrentProcessId();
  // Arguments for calling the settings executable:
  // C:\powertoys_path\PowerToysSettings.exe powertoys_pipe settings_pipe powertoys_pid
  // powertoys_pipe - PowerToys pipe server.
  // settings_pipe - Settings pipe server.
  // powertoys_pid - PowerToys process pid.
  // settings_theme - pass "dark" to start the settings window in dark mode
  wcscpy_s(executable_args, L"\"");
  wcscat_s(executable_args, executable_path);
  wcscat_s(executable_args, L"\"");
  wcscat_s(executable_args, L" ");
  wcscat_s(executable_args, powertoys_pipe_name.c_str());
  wcscat_s(executable_args, L" ");
  wcscat_s(executable_args, settings_pipe_name.c_str());
  wcscat_s(executable_args, L" ");
  wcscat_s(executable_args, std::to_wstring(powertoys_pid).c_str());
  wcscat_s(executable_args, settings_theme.c_str());

  // Run the Settings process with non-elevated privileges

  HWND hwnd = GetShellWindow();
  if (!hwnd) {
    goto LExit;
  }
  DWORD pid;
  GetWindowThreadProcessId(hwnd, &pid);

  process = OpenProcess(PROCESS_CREATE_PROCESS, FALSE, pid);
  if (!process) {
    goto LExit;
  }

  InitializeProcThreadAttributeList(nullptr, 1, 0, &size);
  pptal = (PPROC_THREAD_ATTRIBUTE_LIST)new char[size];
  if (!pptal) {
    goto LExit;
  }

  if (!InitializeProcThreadAttributeList(pptal, 1, 0, &size)) {
    goto LExit;
  }

  if (!UpdateProcThreadAttribute(pptal,
      0,
      PROC_THREAD_ATTRIBUTE_PARENT_PROCESS,
      &process,
      sizeof(process),
      nullptr,
      nullptr)) {
    goto LExit;
  }

  siex.lpAttributeList = pptal;
  siex.StartupInfo.cb = sizeof(siex);

  if (!CreateProcessW(executable_path,
      executable_args,
      nullptr,
      nullptr,
      FALSE,
      EXTENDED_STARTUPINFO_PRESENT,
      nullptr,
      nullptr,
      &siex.StartupInfo,
      &process_info)) {
    goto LExit;
  }

  if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &hToken)) {
    goto LExit;
  }
  current_settings_ipc = new TwoWayPipeMessageIPC(powertoys_pipe_name, settings_pipe_name, receive_json_send_to_main_thread);
  current_settings_ipc->start(hToken);
  g_settings_process_id = process_info.dwProcessId;

  WaitForSingleObject(process_info.hProcess, INFINITE);
  if (WaitForSingleObject(process_info.hProcess, INFINITE) != WAIT_OBJECT_0) {
    show_last_error_message(L"Couldn't wait on the Settings Window to close.", GetLastError());
  }

LExit:

  if (process_info.hProcess) {
    CloseHandle(process_info.hProcess);
  }

  if (process_info.hThread) {
    CloseHandle(process_info.hThread);
  }

  if (pptal) {
    delete[](char*)pptal;
  }

  if (process) {
    CloseHandle(process);
  }

  if (current_settings_ipc) {
    current_settings_ipc->end();
    delete current_settings_ipc;
    current_settings_ipc = NULL;
  }

  if (hToken) {
    CloseHandle(hToken);
  }

  g_settings_process_id = 0;
}

void bring_settings_to_front() {

  auto callback = [](HWND hwnd, LPARAM data) -> BOOL
  {
    DWORD processId;
    if (GetWindowThreadProcessId(hwnd, &processId) && processId == g_settings_process_id) {
      ShowWindow(hwnd, SW_NORMAL);
      SetForegroundWindow(hwnd);
      return FALSE;
    }

    return TRUE;
  };

  EnumWindows(callback, 0);
}

void open_settings_window() {
  if (g_settings_process_id != 0) {
    bring_settings_to_front();
  } else {
    std::thread(run_settings_window).detach();
  }
}
