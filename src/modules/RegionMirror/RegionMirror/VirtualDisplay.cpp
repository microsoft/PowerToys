// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include "VirtualDisplay.h"

#include <cfgmgr32.h>
#include <objbase.h>
#include <setupapi.h>

#include <algorithm>
#include <chrono>
#include <cstdint>
#include <cstdlib>
#include <cwchar>
#include <limits>
#include <stdexcept>
#include <thread>
#include <tuple>
#include <vector>

#pragma comment(lib, "Advapi32.lib")
#pragma comment(lib, "Cfgmgr32.lib")
#pragma comment(lib, "Ole32.lib")
#pragma comment(lib, "Setupapi.lib")
#pragma comment(lib, "Swdevice.lib")

namespace
{
    using Clock = std::chrono::steady_clock;
    constexpr auto EnumerationTimeout = std::chrono::seconds(30);
    constexpr auto RetirementTimeout = std::chrono::seconds(5);
    constexpr auto PollInterval = std::chrono::milliseconds(100);
    constexpr char StagingHelp[] = " Stage the pinned driver with Prepare-VirtualDisplayDriver.ps1 -Stage, then launch RegionMirror as administrator. No test signing or security setting changes are needed by this PoC.";

    struct DeviceInfoSet
    {
        HDEVINFO value = INVALID_HANDLE_VALUE;
        ~DeviceInfoSet()
        {
            if (value != INVALID_HANDLE_VALUE)
            {
                SetupDiDestroyDeviceInfoList(value);
            }
        }
    };

    struct Event
    {
        HANDLE value = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        ~Event()
        {
            if (value)
            {
                CloseHandle(value);
            }
        }
    };

    struct CreationResult
    {
        HANDLE event;
        HRESULT result = E_PENDING;
        std::wstring instanceId;
    };

    void CALLBACK DeviceCreated(HSWDEVICE, HRESULT result, PVOID context, PCWSTR instanceId)
    {
        auto& state = *static_cast<CreationResult*>(context);
        state.result = result;
        try
        {
            if (instanceId)
            {
                state.instanceId = instanceId;
            }
        }
        catch (...)
        {
            state.result = E_OUTOFMEMORY;
        }
        SetEvent(state.event);
    }

    bool IsElevated()
    {
        HANDLE token = nullptr;
        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token))
        {
            return false;
        }
        TOKEN_ELEVATION elevation{};
        DWORD size = 0;
        const bool elevated = GetTokenInformation(token, TokenElevation, &elevation, sizeof(elevation), &size) && elevation.TokenIsElevated;
        CloseHandle(token);
        return elevated;
    }

    bool IsDriverHardwareId(const wchar_t* value)
    {
        return _wcsicmp(value, L"MttVDD") == 0 || _wcsicmp(value, L"Root\\MttVDD") == 0;
    }

    bool IsPresentInstance(const std::wstring& instanceId)
    {
        DeviceInfoSet devices{ SetupDiGetClassDevsW(nullptr, nullptr, nullptr, DIGCF_ALLCLASSES | DIGCF_PRESENT) };
        if (devices.value == INVALID_HANDLE_VALUE)
        {
            throw std::runtime_error("Could not inspect removal of the previous temporary display.");
        }
        SP_DEVINFO_DATA deviceInfo{};
        deviceInfo.cbSize = sizeof(deviceInfo);
        for (DWORD index = 0; SetupDiEnumDeviceInfo(devices.value, index, &deviceInfo); ++index)
        {
            wchar_t candidate[MAX_DEVICE_ID_LEN]{};
            if (SetupDiGetDeviceInstanceIdW(devices.value, &deviceInfo, candidate, ARRAYSIZE(candidate), nullptr) && _wcsicmp(candidate, instanceId.c_str()) == 0)
            {
                return true;
            }
        }
        if (GetLastError() != ERROR_NO_MORE_ITEMS)
        {
            throw std::runtime_error("Could not finish inspecting removal of the previous temporary display.");
        }
        return false;
    }

    void WaitForRetiredInstance(std::wstring& retiredInstance, const std::atomic_bool& cancel)
    {
        if (retiredInstance.empty())
        {
            return;
        }
        const auto deadline = Clock::now() + RetirementTimeout;
        for (;;)
        {
            if (cancel.load())
            {
                throw std::runtime_error("Virtual display creation was cancelled.");
            }
            if (!IsPresentInstance(retiredInstance))
            {
                retiredInstance.clear();
                return;
            }
            if (Clock::now() >= deadline)
            {
                throw std::runtime_error("Windows is still removing the previous RegionMirror display. Wait a moment before starting another session.");
            }
            std::this_thread::sleep_for(PollInterval);
        }
    }

    struct RightmostMonitor
    {
        bool found = false;
        RECT bounds{};
    };

    BOOL CALLBACK FindRightmostMonitor(HMONITOR monitor, HDC, LPRECT, LPARAM parameter)
    {
        auto& search = *reinterpret_cast<RightmostMonitor*>(parameter);
        MONITORINFO info{};
        info.cbSize = sizeof(info);
        if (GetMonitorInfoW(monitor, &info) && info.rcMonitor.right > info.rcMonitor.left && info.rcMonitor.bottom > info.rcMonitor.top)
        {
            if (!search.found || info.rcMonitor.right > search.bounds.right || (info.rcMonitor.right == search.bounds.right && info.rcMonitor.top < search.bounds.top))
            {
                search.found = true;
                search.bounds = info.rcMonitor;
            }
        }
        return TRUE;
    }

    void RequireUnusedDriverDefaults()
    {
        DeviceInfoSet devices{ SetupDiGetClassDevsW(nullptr, nullptr, nullptr, DIGCF_ALLCLASSES | DIGCF_PRESENT) };
        if (devices.value == INVALID_HANDLE_VALUE)
        {
            throw std::runtime_error("Could not inspect existing display devices.");
        }

        SP_DEVINFO_DATA deviceInfo{};
        deviceInfo.cbSize = sizeof(deviceInfo);
        for (DWORD index = 0; SetupDiEnumDeviceInfo(devices.value, index, &deviceInfo); ++index)
        {
            DWORD bytes = 0;
            SetupDiGetDeviceRegistryPropertyW(devices.value, &deviceInfo, SPDRP_HARDWAREID, nullptr, nullptr, 0, &bytes);
            if (!bytes)
            {
                continue;
            }
            std::vector<wchar_t> ids(bytes / sizeof(wchar_t) + 2, L'\0');
            if (!SetupDiGetDeviceRegistryPropertyW(devices.value, &deviceInfo, SPDRP_HARDWAREID, nullptr, reinterpret_cast<PBYTE>(ids.data()), bytes, nullptr))
            {
                continue;
            }
            for (const wchar_t* id = ids.data(); *id; id += wcslen(id) + 1)
            {
                if (IsDriverHardwareId(id))
                {
                    throw std::runtime_error("An existing Virtual Display Driver device is present. This PoC requires its own temporary adapter and will not change an existing adapter.");
                }
            }
        }

        // The stock driver reads global configuration. Use its built-in one-monitor defaults
        // rather than overwrite a configuration that may belong to another application.
        HKEY key = nullptr;
        if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, L"SOFTWARE\\MikeTheTech\\VirtualDisplayDriver", 0, KEY_QUERY_VALUE, &key) == ERROR_SUCCESS)
        {
            DWORD bytes = 0;
            const LSTATUS status = RegQueryValueExW(key, L"VDDPATH", nullptr, nullptr, nullptr, &bytes);
            RegCloseKey(key);
            if (status == ERROR_SUCCESS)
            {
                throw std::runtime_error("An existing Virtual Display Driver configuration override was found. This PoC requires the driver's unmodified defaults and will not replace shared settings.");
            }
        }
        for (const auto* path : { L"C:\\VirtualDisplayDriver\\vdd_settings.xml", L"C:\\VirtualDisplayDriver\\option.txt" })
        {
            if (GetFileAttributesW(path) != INVALID_FILE_ATTRIBUTES)
            {
                throw std::runtime_error("An existing Virtual Display Driver configuration file was found. This PoC requires the driver's built-in one-monitor defaults and will not replace shared settings.");
            }
        }
    }

    bool IsOwnedDeviceOrAncestor(DEVINST device, const std::wstring& ownedInstance)
    {
        for (unsigned depth = 0; depth != 32; ++depth)
        {
            wchar_t id[MAX_DEVICE_ID_LEN]{};
            if (CM_Get_Device_IDW(device, id, MAX_DEVICE_ID_LEN, 0) == CR_SUCCESS && _wcsicmp(id, ownedInstance.c_str()) == 0)
            {
                return true;
            }
            DEVINST parent = 0;
            if (CM_Get_Parent(&parent, device, 0) != CR_SUCCESS)
            {
                break;
            }
            device = parent;
        }
        return false;
    }

    bool IsOwnedInstance(const wchar_t* instance, const std::wstring& ownedInstance)
    {
        if (!instance || !*instance)
        {
            return false;
        }
        if (_wcsicmp(instance, ownedInstance.c_str()) == 0)
        {
            return true;
        }
        DEVINST device = 0;
        return CM_Locate_DevNodeW(&device, const_cast<PWSTR>(instance), CM_LOCATE_DEVNODE_NORMAL) == CR_SUCCESS && IsOwnedDeviceOrAncestor(device, ownedInstance);
    }

    bool IsOwnedInterface(const wchar_t* interfacePath, const std::wstring& ownedInstance)
    {
        DeviceInfoSet devices{ SetupDiCreateDeviceInfoList(nullptr, nullptr) };
        if (devices.value == INVALID_HANDLE_VALUE)
        {
            return false;
        }
        SP_DEVICE_INTERFACE_DATA interfaceData{};
        interfaceData.cbSize = sizeof(interfaceData);
        if (!SetupDiOpenDeviceInterfaceW(devices.value, interfacePath, 0, &interfaceData))
        {
            return false;
        }
        DWORD bytes = 0;
        SetupDiGetDeviceInterfaceDetailW(devices.value, &interfaceData, nullptr, 0, &bytes, nullptr);
        if (bytes < sizeof(SP_DEVICE_INTERFACE_DETAIL_DATA_W))
        {
            return false;
        }
        std::vector<BYTE> storage(bytes);
        auto* details = reinterpret_cast<SP_DEVICE_INTERFACE_DETAIL_DATA_W*>(storage.data());
        details->cbSize = sizeof(*details);
        SP_DEVINFO_DATA deviceInfo{};
        deviceInfo.cbSize = sizeof(deviceInfo);
        return SetupDiGetDeviceInterfaceDetailW(devices.value, &interfaceData, details, bytes, nullptr, &deviceInfo) && IsOwnedDeviceOrAncestor(deviceInfo.DevInst, ownedInstance);
    }

    std::wstring FindOwnedDisplayName(const std::wstring& ownedInstance)
    {
        for (DWORD index = 0;; ++index)
        {
            DISPLAY_DEVICEW adapter{};
            adapter.cb = sizeof(adapter);
            if (!EnumDisplayDevicesW(nullptr, index, &adapter, 0))
            {
                break;
            }
            if (IsOwnedInstance(adapter.DeviceID, ownedInstance))
            {
                return adapter.DeviceName;
            }
        }

        // Some drivers expose an interface path instead of an instance ID in DISPLAY_DEVICE.
        // Follow the adapter's PnP ancestry; a newly hot-plugged physical monitor never qualifies.
        UINT pathCount = 0;
        UINT modeCount = 0;
        if (GetDisplayConfigBufferSizes(QDC_ALL_PATHS, &pathCount, &modeCount) != ERROR_SUCCESS)
        {
            return {};
        }
        std::vector<DISPLAYCONFIG_PATH_INFO> paths(pathCount);
        std::vector<DISPLAYCONFIG_MODE_INFO> modes(modeCount);
        if (QueryDisplayConfig(QDC_ALL_PATHS, &pathCount, paths.data(), &modeCount, modes.data(), nullptr) != ERROR_SUCCESS)
        {
            return {};
        }
        for (UINT index = 0; index < pathCount; ++index)
        {
            DISPLAYCONFIG_ADAPTER_NAME adapter{};
            adapter.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_ADAPTER_NAME;
            adapter.header.size = sizeof(adapter);
            adapter.header.adapterId = paths[index].sourceInfo.adapterId;
            if (DisplayConfigGetDeviceInfo(&adapter.header) != ERROR_SUCCESS || !IsOwnedInterface(adapter.adapterDevicePath, ownedInstance))
            {
                continue;
            }
            DISPLAYCONFIG_SOURCE_DEVICE_NAME source{};
            source.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
            source.header.size = sizeof(source);
            source.header.adapterId = paths[index].sourceInfo.adapterId;
            source.header.id = paths[index].sourceInfo.id;
            if (DisplayConfigGetDeviceInfo(&source.header) == ERROR_SUCCESS && source.viewGdiDeviceName[0])
            {
                return source.viewGdiDeviceName;
            }
        }
        return {};
    }

    bool ApplySupportedMode(const std::wstring& deviceName, int requestedWidth, int requestedHeight, LONG desktopRight, LONG desktopTop, Clock::time_point deadline, const std::atomic_bool& cancel)
    {
        std::vector<DEVMODEW> modes;
        for (DWORD index = 0; index != 4096; ++index)
        {
            if (cancel.load() || Clock::now() >= deadline)
            {
                return false;
            }
            DEVMODEW mode{};
            mode.dmSize = sizeof(mode);
            if (!EnumDisplaySettingsW(deviceName.c_str(), index, &mode))
            {
                break;
            }
            if (mode.dmPelsWidth && mode.dmPelsHeight)
            {
                modes.push_back(mode);
            }
        }
        if (modes.empty())
        {
            return false;
        }
        const auto score = [=](const DEVMODEW& mode) {
            const auto dx = static_cast<int64_t>(mode.dmPelsWidth) - requestedWidth;
            const auto dy = static_cast<int64_t>(mode.dmPelsHeight) - requestedHeight;
            const auto refreshDistance = std::abs(static_cast<int64_t>(mode.dmDisplayFrequency) - 60);
            return std::tuple{ dx * dx + dy * dy, refreshDistance, mode.dmBitsPerPel == 32 ? 0 : 1 };
        };
        std::stable_sort(modes.begin(), modes.end(), [&](const auto& left, const auto& right) { return score(left) < score(right); });
        for (auto mode : modes)
        {
            if (cancel.load() || Clock::now() >= deadline)
            {
                return false;
            }
            if (static_cast<int64_t>(desktopRight) + mode.dmPelsWidth > (std::numeric_limits<LONG>::max)())
            {
                continue;
            }
            mode.dmPosition = { desktopRight, desktopTop };
            mode.dmFields |= DM_POSITION | DM_PELSWIDTH | DM_PELSHEIGHT;
            if (ChangeDisplaySettingsExW(deviceName.c_str(), &mode, nullptr, CDS_TEST, nullptr) != DISP_CHANGE_SUCCESSFUL)
            {
                continue;
            }
            // No CDS_UPDATEREGISTRY: only this owned adapter receives a transient mode change.
            if (ChangeDisplaySettingsExW(deviceName.c_str(), &mode, nullptr, 0, nullptr) == DISP_CHANGE_SUCCESSFUL)
            {
                return true;
            }
        }
        return false;
    }

    struct MonitorSearch
    {
        const std::wstring& deviceName;
        HMONITOR monitor = nullptr;
        RECT bounds{};
    };

    BOOL CALLBACK FindMonitor(HMONITOR monitor, HDC, LPRECT, LPARAM parameter)
    {
        auto& search = *reinterpret_cast<MonitorSearch*>(parameter);
        MONITORINFOEXW info{};
        info.cbSize = sizeof(info);
        if (GetMonitorInfoW(monitor, reinterpret_cast<MONITORINFO*>(&info)) && _wcsicmp(info.szDevice, search.deviceName.c_str()) == 0)
        {
            search.monitor = monitor;
            search.bounds = info.rcMonitor;
            return FALSE;
        }
        return TRUE;
    }
}

namespace RegionMirror
{
    VirtualDisplay::~VirtualDisplay()
    {
        Close();
    }

    void VirtualDisplay::Create(int requestedWidth, int requestedHeight, const std::atomic_bool& cancel)
    {
        Close();
        if (requestedWidth <= 0 || requestedHeight <= 0 || requestedWidth > 16384 || requestedHeight > 16384)
        {
            throw std::runtime_error("The requested virtual display size is outside the supported PoC range.");
        }
        if (!IsElevated())
        {
            throw std::runtime_error(std::string("Creating the temporary virtual display requires an elevated RegionMirror process.") + StagingHelp);
        }
        // SwDeviceClose starts asynchronous PnP removal. Wait on the creating worker,
        // not the UI thread, and only for the exact instance this object previously owned.
        WaitForRetiredInstance(m_retiredInstanceId, cancel);
        RequireUnusedDriverDefaults();
        if (cancel.load())
        {
            throw std::runtime_error("Virtual display creation was cancelled.");
        }

        RightmostMonitor anchor;
        if (!EnumDisplayMonitors(nullptr, nullptr, FindRightmostMonitor, reinterpret_cast<LPARAM>(&anchor)) || !anchor.found)
        {
            throw std::runtime_error("Could not find an active monitor next to which to place the temporary display.");
        }
        // Use one real monitor's corner. Mixing the desktop's right and top extrema
        // can leave the new display disconnected from an irregular monitor layout.
        const LONG desktopRight = anchor.bounds.right;
        const LONG desktopTop = anchor.bounds.top;
        GUID id{};
        if (FAILED(CoCreateGuid(&id)))
        {
            throw std::runtime_error("Could not create an identity for the temporary display.");
        }
        wchar_t instance[40]{};
        if (!StringFromGUID2(id, instance, ARRAYSIZE(instance)))
        {
            throw std::runtime_error("Could not format the temporary display identity.");
        }
        Event event;
        if (!event.value)
        {
            throw std::runtime_error("Could not create the virtual display completion event.");
        }
        CreationResult completion{ event.value };
        SW_DEVICE_CREATE_INFO createInfo{};
        createInfo.cbSize = sizeof(createInfo);
        createInfo.pszInstanceId = instance;
        createInfo.pszzHardwareIds = L"MttVDD\0";
        createInfo.pszDeviceDescription = L"PowerToys RegionMirror temporary display";
        createInfo.CapabilityFlags = SWDeviceCapabilitiesRemovable | SWDeviceCapabilitiesSilentInstall | SWDeviceCapabilitiesDriverRequired;
        try
        {
            // The default SWDeviceLifetimeHandle ties enumeration to this handle. Do not use
            // ParentPresent lifetime, a global root device, or the driver's reload pipe.
            const HRESULT result = SwDeviceCreate(L"PowerToysRegionMirror", L"HTREE\\ROOT\\0", &createInfo, 0, nullptr, DeviceCreated, &completion, &m_device);
            if (FAILED(result))
            {
                throw std::runtime_error("SwDeviceCreate failed (HRESULT " + std::to_string(static_cast<uint32_t>(result)) + ")." + StagingHelp);
            }
            const auto deadline = Clock::now() + EnumerationTimeout;
            bool modeApplied = false;
            while (Clock::now() < deadline)
            {
                if (cancel.load())
                {
                    throw std::runtime_error("Virtual display creation was cancelled.");
                }
                if (m_instanceId.empty() && WaitForSingleObject(event.value, 0) == WAIT_OBJECT_0)
                {
                    if (FAILED(completion.result) || completion.instanceId.empty())
                    {
                        throw std::runtime_error("The temporary display could not be enumerated (HRESULT " + std::to_string(static_cast<uint32_t>(completion.result)) + ")." + StagingHelp);
                    }
                    m_instanceId = completion.instanceId;
                }
                if (!m_instanceId.empty())
                {
                    m_deviceName = FindOwnedDisplayName(m_instanceId);
                    if (!m_deviceName.empty())
                    {
                        if (!modeApplied)
                        {
                            modeApplied = ApplySupportedMode(m_deviceName, requestedWidth, requestedHeight, desktopRight, desktopTop, deadline, cancel);
                        }
                        if (modeApplied)
                        {
                            MonitorSearch search{ m_deviceName };
                            EnumDisplayMonitors(nullptr, nullptr, FindMonitor, reinterpret_cast<LPARAM>(&search));
                            if (search.monitor && search.bounds.right > search.bounds.left && search.bounds.bottom > search.bounds.top)
                            {
                                m_monitor = search.monitor;
                                m_bounds = search.bounds;
                                return;
                            }
                        }
                    }
                }
                std::this_thread::sleep_for(PollInterval);
            }
            std::string diagnostic;
            DEVINST deviceInstance = 0;
            ULONG deviceStatus = 0;
            ULONG problem = 0;
            if (!m_instanceId.empty() && CM_Locate_DevNodeW(&deviceInstance, m_instanceId.data(), CM_LOCATE_DEVNODE_NORMAL) == CR_SUCCESS && CM_Get_DevNode_Status(&deviceStatus, &problem, deviceInstance, 0) == CR_SUCCESS)
            {
                diagnostic = " PnP problem code: " + std::to_string(problem) + ".";
            }
            throw std::runtime_error(std::string("Timed out waiting for the owned virtual display and a supported display mode. Check that the pinned x64 driver package is staged and accepted by Windows.") + diagnostic + StagingHelp);
        }
        catch (...)
        {
            // Close also waits for an in-flight creation callback, keeping its context alive.
            Close();
            // Cancellation can arrive before the worker copies the callback's instance ID.
            // After Close has joined the callback, retain that exact identity for next time.
            if (!completion.instanceId.empty())
            {
                m_retiredInstanceId.swap(completion.instanceId);
            }
            throw;
        }
    }

    void VirtualDisplay::Close() noexcept
    {
        if (m_device)
        {
            SwDeviceClose(m_device);
            m_device = nullptr;
        }
        if (!m_instanceId.empty())
        {
            // Swapping cannot allocate, preserving Close's noexcept contract.
            m_retiredInstanceId.swap(m_instanceId);
        }
        m_monitor = nullptr;
        m_bounds = {};
        m_deviceName.clear();
        m_instanceId.clear();
    }

    HMONITOR VirtualDisplay::Monitor() const noexcept
    {
        return m_monitor;
    }

    RECT VirtualDisplay::Bounds() const noexcept
    {
        return m_bounds;
    }

    std::wstring VirtualDisplay::DeviceName() const
    {
        // GDI names can change while Windows re-enumerates the display topology.
        // Resolve the owned adapter each time; an empty result lets the caller retry.
        return m_device && !m_instanceId.empty() ? FindOwnedDisplayName(m_instanceId) : std::wstring{};
    }

    std::wstring VirtualDisplay::InstanceId() const
    {
        return m_instanceId;
    }
}
