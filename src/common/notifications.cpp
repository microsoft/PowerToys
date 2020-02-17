#include "pch.h"
#include "notifications.h"

#include <unknwn.h>
#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Data.Xml.Dom.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.UI.Notifications.h>
#include <winrt/Windows.ApplicationModel.Background.h>

using namespace winrt::Windows::ApplicationModel::Background;
using winrt::Windows::Data::Xml::Dom::XmlDocument;
using winrt::Windows::UI::Notifications::ToastNotification;
using winrt::Windows::UI::Notifications::ToastNotificationManager;

namespace
{
    constexpr std::wstring_view TASK_NAME = L"PowerToysBackgroundNotificationsHandler";
    constexpr std::wstring_view TASK_ENTRYPOINT = L"PowerToysNotifications.BackgroundHandler";
    constexpr std::wstring_view APPLICATION_ID = L"PowerToys";
}

void notifications::register_background_toast_handler()
{
    try
    {
        // Re-request access to clean up from previous PowerToys installations
        BackgroundExecutionManager::RemoveAccess();
        BackgroundExecutionManager::RequestAccessAsync().get();

        BackgroundTaskBuilder builder;
        ToastNotificationActionTrigger trigger{ APPLICATION_ID };
        builder.SetTrigger(trigger);
        builder.TaskEntryPoint(TASK_ENTRYPOINT);
        builder.Name(TASK_NAME);

        const auto tasks = BackgroundTaskRegistration::AllTasks();
        const bool already_registered = std::any_of(begin(tasks), end(tasks), [=](const auto& task) {
            return task.Value().Name() == TASK_NAME;
        });
        if (already_registered)
        {
            return;
        }
        (void)builder.Register();
    }
    catch (...)
    {
        // Couldn't register the background task, nothing we can do
    }
}

void notifications::show_toast(std::wstring_view message)
{
    // The toast won't be actually activated in the background, since it doesn't have any buttons
    show_toast_background_activated(message, {}, {});
}

void notifications::show_toast_background_activated(std::wstring_view message, std::wstring_view background_handler_id, std::vector<std::wstring_view> button_labels)
{
    // DO NOT LOCALIZE any string in this function, because they're XML tags and a subject to
    // https://docs.microsoft.com/en-us/windows/uwp/design/shell/tiles-and-notifications/toast-xml-schema

    std::wstring toast_xml;
    toast_xml.reserve(1024);
    toast_xml += LR"(<?xml version="1.0"?><toast><visual><binding template="ToastGeneric"><text>PowerToys</text><text>)";
    toast_xml += message;
    toast_xml += L"</text></binding></visual><actions>";

    for (size_t i = 0; i < size(button_labels); ++i)
    {
        toast_xml += LR"(<action activationType="background" arguments=")";
        toast_xml += L"button_id=" + std::to_wstring(i); // pass the button ID
        toast_xml += L"&amp;handler=";
        toast_xml += background_handler_id;
        toast_xml += LR"(" content=")";
        toast_xml += button_labels[i];
        toast_xml += LR"("/>)";
    }
    toast_xml += L"</actions></toast>";

    XmlDocument toast_xml_doc;
    toast_xml_doc.LoadXml(toast_xml);
    ToastNotification notification{ toast_xml_doc };

    const auto notifier = ToastNotificationManager::ToastNotificationManager::CreateToastNotifier();
    notifier.Show(notification);
}
