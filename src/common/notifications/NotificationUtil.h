#pragma once

#include <common/SettingsAPI/FileWatcher.h>

namespace notifications
{
    class NotificationUtil
    {
    public:
        NotificationUtil();
        ~NotificationUtil();

        void WarnIfElevationIsRequired(std::wstring title, std::wstring message, std::wstring button1, std::wstring button2);

    private:
        std::unique_ptr<FileWatcher> m_settingsFileWatcher;
        bool m_warningsElevatedApps;
        bool m_warningShown = false;

        void ReadSettings();
    };
}
