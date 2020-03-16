// TwoWayIPCManager.cpp : Implementation of CTwoWayIPCManager

#include "pch.h"
#include "TwoWayIPCManager.h"
#include <iostream>
#include <string>

// CTwoWayIPCManager

using namespace std;

CTwoWayIPCManager::CTwoWayIPCManager()
{
        this->m_MessagePipe = nullptr;
        m_message_from_runner = nullptr;
}

CTwoWayIPCManager::~CTwoWayIPCManager()
{
    delete this->m_MessagePipe;
}

/*
Send IPC Message.
*/
STDMETHODIMP CTwoWayIPCManager::SendMessage(BSTR message)
{
    wstring strMessage = (PCWSTR)message;
    this->m_MessagePipe->send(strMessage);
    wcout << "sending message" << L"\n";
    return S_OK;
}

STDMETHODIMP CTwoWayIPCManager::Initialize(BSTR runnerPipeName, BSTR settingsPipeName)
{
    try
    {
        wstring powertoys_pipe_name = (PCWSTR)runnerPipeName;
        wstring settings_pipe_name = (PCWSTR)settingsPipeName;

        wcout << powertoys_pipe_name.c_str() << L"\n";
        wcout << settings_pipe_name.c_str() << L"\n";

        this->m_MessagePipe = nullptr;
        this->m_MessagePipe = new TwoWayPipeMessageIPC(powertoys_pipe_name, settings_pipe_name, nullptr);
        this->m_MessagePipe->start(nullptr);
    }
    catch (std::exception exp)
    {
        wcout << "failed starting queue " << L"\n";
    }
    return S_OK;
}
