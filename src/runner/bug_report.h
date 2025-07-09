#pragma once

#include <functional>
#include <vector>
#include <mutex>

// Observer pattern for bug report status changes
using BugReportCallback = std::function<void(bool isRunning)>;

class BugReportManager
{
public:
    static BugReportManager& instance();
    
    // Register a callback to be notified when bug report status changes
    void register_callback(const BugReportCallback& callback);
    
    // Remove all callbacks (useful for cleanup)
    void clear_callbacks();
    
    // Launch bug report and notify observers
    void launch_bug_report() noexcept;
    
    // Check if bug report is currently running
    bool is_bug_report_running() const noexcept;

private:
    BugReportManager() = default;
    ~BugReportManager() = default;
    BugReportManager(const BugReportManager&) = delete;
    BugReportManager& operator=(const BugReportManager&) = delete;
    
    // Notify all registered callbacks
    void notify_observers(bool isRunning);
    
    std::atomic_bool m_isBugReportRunning = false;
    std::vector<BugReportCallback> m_callbacks;
    mutable std::mutex m_callbacksMutex;
};

// Legacy functions for backward compatibility
void launch_bug_report() noexcept;
bool is_bug_report_running() noexcept;