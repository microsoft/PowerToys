// TwoWayIPCManager.cpp : Implementation of CTwoWayIPCManager

#include "pch.h"
#include "TwoWayIPCManager.h"
#include <iostream>
#include <string>


using namespace std;

/*
* Default constructor, initialized the IPC variabl to null.
*/
CTwoWayIPCManager::CTwoWayIPCManager()
{
    this->m_MessagePipe = nullptr;
}

CTwoWayIPCManager::~CTwoWayIPCManager()
{
    delete this->m_MessagePipe;
}

/*
* Send IPC Message.
*/
STDMETHODIMP CTwoWayIPCManager::SendMessage(BSTR message)
{
    wstring strMessage = (PCWSTR)message;
    this->m_MessagePipe->send(strMessage);
    wcout << "sending message" << L"\n";
    return S_OK;
}

/*
* This method is meant to initialize the IPC constructor and
* and start the threading process.
*/
STDMETHODIMP CTwoWayIPCManager::Initialize(BSTR runnerPipeName, BSTR settingsPipeName)
{
    try
    {
        wstring powertoys_pipe_name = (PCWSTR)runnerPipeName;
        wstring settings_pipe_name = (PCWSTR)settingsPipeName;

        this->m_MessagePipe = new TwoWayPipeMessageIPC(powertoys_pipe_name, settings_pipe_name, nullptr);
        this->m_MessagePipe->start(nullptr);
    }
    catch (std::exception exp)
    {
        S_FALSE;
    }
    return S_OK;
}
