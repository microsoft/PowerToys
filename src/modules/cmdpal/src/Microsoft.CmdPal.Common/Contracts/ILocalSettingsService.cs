// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;

namespace Microsoft.CmdPal.Common.Contracts;

public interface ILocalSettingsService
{
    Task<bool> HasSettingAsync(string key);

    Task<T?> ReadSettingAsync<T>(string key);

    Task SaveSettingAsync<T>(string key, T value);
}
