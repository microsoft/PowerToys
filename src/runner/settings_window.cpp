#include "pch.h"
#include <WinSafer.h>
#include <Sddl.h>
#include <sstream>
#include <accctrl.h>
#include <aclapi.h>
#include <cpprest/json.h>
#include "powertoy_module.h"
#include <common/two_way_pipe_message_ipc.h>
#include "tray_icon.h"
#include "general_settings.h"

#define BUFSIZE 1024

using namespace web;

TwoWayPipeMessageIPC* current_settings_ipc = NULL;

json::value get_power_toys_settings() {
  json::value result = json::value::object();
  for (auto&[name, powertoy] : modules()) {
    try {
      json::value powertoys_config = json::value::parse(powertoy.get_config());
      result.as_object()[name] = powertoys_config;
    }
    catch (json::json_exception&) {
      //Malformed JSON.
    }
  }
  return result;
}

json::value get_all_settings() {
  json::value result = json::value::object();
  result.as_object()[L"general"] = get_general_settings();
  result.as_object()[L"powertoys"] = get_power_toys_settings();
  return result;
}

void dispatch_json_action_to_module(const json::value& powertoys_configs) {
  for (auto powertoy_element : powertoys_configs.as_object()) {
    std::wstringstream ws;
    ws << powertoy_element.second;
    if (modules().find(powertoy_element.first) != modules().end()) {
      modules().at(powertoy_element.first).call_custom_action(ws.str());
    }
  }
}

void send_json_config_to_module(const std::wstring& module_key, const std::wstring& settings) {
  if (modules().find(module_key) != modules().end()) {
    modules().at(module_key).set_config(settings);
  }
}

void dispatch_json_config_to_modules(const json::value& powertoys_configs) {
  for (auto powertoy_element : powertoys_configs.as_object()) {
    std::wstringstream ws;
    ws << powertoy_element.second;
    send_json_config_to_module(powertoy_element.first, ws.str());
  }
};

void dispatch_received_json(const std::wstring &json_to_parse) {
  json::value j = json::value::parse(json_to_parse);
  for(auto base_element : j.as_object()) {
    if (base_element.first == L"general") {
      apply_general_settings(base_element.second);
      std::wstringstream ws;
      ws << get_all_settings();
      if (current_settings_ipc != NULL) {
        current_settings_ipc->send(ws.str());
      }
    } else if (base_element.first == L"powertoys") {
      dispatch_json_config_to_modules(base_element.second);
      std::wstringstream ws;
      ws << get_all_settings();
      if (current_settings_ipc != NULL) {
        current_settings_ipc->send(ws.str());
      }
    } else if (base_element.first == L"refresh") {
      std::wstringstream ws;
      ws << get_all_settings();
      if (current_settings_ipc != NULL) {
        current_settings_ipc->send(ws.str());
      }
    } else if (base_element.first == L"action") {
      dispatch_json_action_to_module(base_element.second);
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
  wcscpy_s(executable_args, L"\"");
  wcscat_s(executable_args, executable_path);
  wcscat_s(executable_args, L"\"");
  wcscat_s(executable_args, L" ");
  wcscat_s(executable_args, powertoys_pipe_name.c_str());
  wcscat_s(executable_args, L" ");
  wcscat_s(executable_args, settings_pipe_name.c_str());
  wcscat_s(executable_args, L" ");
  wcscat_s(executable_args, std::to_wstring(powertoys_pid).c_str());

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
