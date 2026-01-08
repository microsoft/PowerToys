#pragma once
#include <wtypes.h>
#include <string>
#include <functional>
#include <thread>
#include <atomic>
#include <mutex>

class NightLightRegistryObserver
{
public:
    NightLightRegistryObserver(HKEY root, const std::wstring& subkey, std::function<void()> callback) :
        _root(root), _subkey(subkey), _callback(std::move(callback)), _stop(false)
    {
        _thread = std::thread([this]() { this->Run(); });
    }

    ~NightLightRegistryObserver()
    {
        Stop();
    }

    void Stop()
    {
        _stop = true;

        {
            std::lock_guard<std::mutex> lock(_mutex);
            if (_event)
                SetEvent(_event);
        }

        if (_thread.joinable())
            _thread.join();

        std::lock_guard<std::mutex> lock(_mutex);
        if (_hKey)
        {
            RegCloseKey(_hKey);
            _hKey = nullptr;
        }

        if (_event)
        {
            CloseHandle(_event);
            _event = nullptr;
        }
    }


private:
    void Run()
    {
        {
            std::lock_guard<std::mutex> lock(_mutex);
            if (RegOpenKeyExW(_root, _subkey.c_str(), 0, KEY_NOTIFY, &_hKey) != ERROR_SUCCESS)
                return;

            _event = CreateEventW(nullptr, TRUE, FALSE, nullptr);
            if (!_event)
            {
                RegCloseKey(_hKey);
                _hKey = nullptr;
                return;
            }
        }

        while (!_stop)
        {
            HKEY hKeyLocal = nullptr;
            HANDLE eventLocal = nullptr;

            {
                std::lock_guard<std::mutex> lock(_mutex);
                if (_stop)
                    break;

                hKeyLocal = _hKey;
                eventLocal = _event;
            }

            if (!hKeyLocal || !eventLocal)
                break;

            if (_stop)
                break;

            if (RegNotifyChangeKeyValue(hKeyLocal, FALSE, REG_NOTIFY_CHANGE_LAST_SET, eventLocal, TRUE) != ERROR_SUCCESS)
                break;

            DWORD wait = WaitForSingleObject(eventLocal, INFINITE);
            if (_stop || wait == WAIT_FAILED)
                break;

            ResetEvent(eventLocal);

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

        {
            std::lock_guard<std::mutex> lock(_mutex);
            if (_hKey)
            {
                RegCloseKey(_hKey);
                _hKey = nullptr;
            }

            if (_event)
            {
                CloseHandle(_event);
                _event = nullptr;
            }
        }
    }


    HKEY _root;
    std::wstring _subkey;
    std::function<void()> _callback;
    HANDLE _event = nullptr;
    HKEY _hKey = nullptr;
    std::thread _thread;
    std::atomic<bool> _stop;
    std::mutex _mutex;
};