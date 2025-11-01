#pragma once
#include <wtypes.h>
#include <string>
#include <functional>
#include <thread>
#include <atomic>

class RegistryObserver
{
public:
    RegistryObserver(HKEY root, const std::wstring& subkey, std::function<void()> callback) :
        _root(root), _subkey(subkey), _callback(std::move(callback)), _stop(false)
    {
        _thread = std::thread([this]() { this->Run(); });
    }

    ~RegistryObserver()
    {
        Stop();
    }

    void Stop()
    {
        _stop = true;

        if (_event)
            SetEvent(_event);
        if (_hKey)
        {
            RegCloseKey(_hKey);
            _hKey = nullptr;
        }

        if (_thread.joinable())
            _thread.join();

        if (_event)
        {
            CloseHandle(_event);
            _event = nullptr;
        }
    }

private:
    void Run()
    {
        if (RegOpenKeyExW(_root, _subkey.c_str(), 0, KEY_NOTIFY, &_hKey) != ERROR_SUCCESS)
            return;

        _event = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        if (!_event)
        {
            RegCloseKey(_hKey);
            _hKey = nullptr;
            return;
        }

        while (!_stop)
        {
            if (RegNotifyChangeKeyValue(_hKey, FALSE, REG_NOTIFY_CHANGE_LAST_SET, _event, TRUE) != ERROR_SUCCESS)
                break;

            DWORD wait = WaitForSingleObject(_event, INFINITE);
            if (_stop || wait == WAIT_FAILED)
                break;

            ResetEvent(_event);

            if (!_stop && _callback)
            {
                try
                {
                    _callback();
                }
                catch (...)
                {
                }
            }
        }

        if (_hKey)
        {
            RegCloseKey(_hKey);
            _hKey = nullptr;
        }
    }

    HKEY _root;
    std::wstring _subkey;
    std::function<void()> _callback;
    HANDLE _event = nullptr;
    HKEY _hKey = nullptr;
    std::thread _thread;
    std::atomic<bool> _stop;
};
