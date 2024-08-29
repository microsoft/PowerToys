// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace Microsoft.CmdPal.Common.Contracts;

public interface ILocalSettingsService
{
    Task<bool> HasSettingAsync(string key);

    Task<T?> ReadSettingAsync<T>(string key);

    Task SaveSettingAsync<T>(string key, T value);
}
