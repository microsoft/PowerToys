#include "pch.h"

#include "common.h"
#include "com_object_factory.h"
#include "notifications.h"

#include <unknwn.h>
#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Data.Xml.Dom.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.UI.Notifications.h>
#include <winrt/Windows.ApplicationModel.Background.h>

#include "winstore.h"

#include <winerror.h>
#include <NotificationActivationCallback.h>

#include "notifications_winrt/handler_functions.h"

using namespace winrt::Windows::ApplicationModel::Background;
using winrt::Windows::Data::Xml::Dom::XmlDocument;
using winrt::Windows::UI::Notifications::ToastNotification;
using winrt::Windows::UI::Notifications::ToastNotificationManager;

namespace
{
    constexpr std::wstring_view TASK_NAME = L"PowerToysBackgroundNotificationsHandler";
    constexpr std::wstring_view TASK_ENTRYPOINT = L"PowerToysNotifications.BackgroundHandler";
    constexpr std::wstring_view APPLICATION_ID = L"PowerToys";

    constexpr std::wstring_view WIN32_AUMID = L"Microsoft.PowerToysWin32";
}

static DWORD loop_thread_id()
{
    static const DWORD thread_id = GetCurrentThreadId();
    return thread_id;
}

class DECLSPEC_UUID("DD5CACDA-7C2E-4997-A62A-04A597B58F76") NotificationActivator : public INotificationActivationCallback
{
public:
    HRESULT __stdcall QueryInterface(_In_ REFIID iid, _Outptr_ void** resultInterface) override
    {
        static const QITAB qit[] = {
            QITABENT(NotificationActivator, INotificationActivationCallback),
            { 0 }
        };
        return QISearch(this, qit, iid, resultInterface);
    }

    ULONG __stdcall AddRef() override
    {
        return ++_refCount;
    }

    ULONG __stdcall Release() override
    {
        LONG refCount = --_refCount;
        if (refCount == 0)
        {
            PostThreadMessage(loop_thread_id(), WM_QUIT, 0, 0);
            delete this;
        }
        return refCount;
    }

    virtual HRESULT STDMETHODCALLTYPE Activate(
        LPCWSTR appUserModelId,
        LPCWSTR invokedArgs,
        const NOTIFICATION_USER_INPUT_DATA*,
        ULONG) override
    {
        auto lib = LoadLibraryW(L"Notifications.dll");
        if (!lib)
        {
            return 1;
        }
        auto dispatcher = reinterpret_cast<decltype(dispatch_to_backround_handler)*>(GetProcAddress(lib, "dispatch_to_backround_handler"));
        if (!dispatcher)
        {
            return 1;
        }

        dispatcher(invokedArgs);

        return 0;
    }

private:
    std::atomic<long> _refCount;
};

void notifications::run_desktop_app_activator_loop()
{
    com_object_factory<NotificationActivator> factory;

    (void)loop_thread_id();

    DWORD token;
    auto res = CoRegisterClassObject(__uuidof(NotificationActivator), &factory, CLSCTX_LOCAL_SERVER, REGCLS_MULTIPLEUSE, &token);
    if (!SUCCEEDED(res))
    {
        return;
    }

    run_message_loop();
    CoRevokeClassObject(token);
}

void notifications::register_background_toast_handler()
{
    if (!winstore::running_as_packaged())
    {
        // The WIX installer will have us registered via the registry
        return;
    }
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
    show_toast_with_activations(message, {}, {});
}

inline void xml_escape(std::wstring data)
{
    std::wstring buffer;
    buffer.reserve(data.size());
    for (size_t pos = 0; pos != data.size(); ++pos)
    {
        switch (data[pos])
        {
        case L'&':
            buffer.append(L"&amp;");
            break;
        case L'\"':
            buffer.append(L"&quot;");
            break;
        case L'\'':
            buffer.append(L"&apos;");
            break;
        case L'<':
            buffer.append(L"&lt;");
            break;
        case L'>':
            buffer.append(L"&gt;");
            break;
        default:
            buffer.append(&data[pos], 1);
            break;
        }
    }
    data.swap(buffer);
}

void notifications::show_toast_with_activations(std::wstring_view message, std::wstring_view background_handler_id, std::vector<button_t> buttons)
{
    // DO NOT LOCALIZE any string in this function, because they're XML tags and a subject to
    // https://docs.microsoft.com/en-us/windows/uwp/design/shell/tiles-and-notifications/toast-xml-schema

    std::wstring toast_xml;
    toast_xml.reserve(1024);
    std::wstring title{L"PowerToys"};
    if (winstore::running_as_packaged())
    {
        title += L" (Experimental)";
    }

    toast_xml += LR"(<?xml version="1.0"?><toast><visual><binding template="ToastGeneric"><text>)";
    toast_xml += title;
    toast_xml += L"</text><text>";
    toast_xml += message;
    toast_xml += L"</text></binding></visual><actions>";

    for (size_t i = 0; i < size(buttons); ++i)
    {
        std::visit(overloaded{
                       [&](const link_button& b) {
                           toast_xml += LR"(<action activationType="protocol" arguments=")";
                           toast_xml += b.url;
                           toast_xml += LR"(" content=")";
                           toast_xml += b.label;
                           toast_xml += LR"("/>)";
                       },
                       [&](const background_activated_button& b) {
                           toast_xml += LR"(<action activationType="background" arguments=")";
                           toast_xml += L"button_id=" + std::to_wstring(i); // pass the button ID
                           toast_xml += L"&amp;handler=";
                           toast_xml += background_handler_id;
                           toast_xml += LR"(" content=")";
                           toast_xml += b.label;
                           toast_xml += LR"("/>)";
                       },
                   },
                   buttons[i]);
    }
    toast_xml += L"</actions></toast>";

    XmlDocument toast_xml_doc;
    xml_escape(toast_xml);
    toast_xml_doc.LoadXml(toast_xml);
    ToastNotification notification{ toast_xml_doc };

    const auto notifier = winstore::running_as_packaged() ? ToastNotificationManager::ToastNotificationManager::CreateToastNotifier() :
                                                            ToastNotificationManager::ToastNotificationManager::CreateToastNotifier(WIN32_AUMID);
    notifier.Show(notification);
}
