#pragma once

using winrt::Windows::ApplicationModel::Background::IBackgroundTaskInstance;

void dispatch_to_backround_handler(std::wstring_view background_handler_id, IBackgroundTaskInstance bti, const size_t button_id);
