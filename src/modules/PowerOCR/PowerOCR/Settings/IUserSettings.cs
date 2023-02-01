// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerOCR.Settings;

public interface IUserSettings
{
    SettingItem<string> ActivationShortcut { get; }

    SettingItem<string> PreferredLanguage { get; }

    void SendSettingsTelemetry();
}
