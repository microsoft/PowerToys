// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Contracts;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Models;
using Microsoft.Extensions.Options;
using Windows.Storage;

namespace Microsoft.CmdPal.Common.Services;

public class LocalSettingsService : ILocalSettingsService
{
    // TODO! for now, we're hardcoding the path as effectively:
    // %localappdata%\CmdPal\LocalSettings.json
    private const string DefaultApplicationDataFolder = "CmdPal";
    private const string DefaultLocalSettingsFile = "LocalSettings.json";

    private readonly IFileService _fileService;
    private readonly LocalSettingsOptions _options;

    private readonly string _localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private readonly string _applicationDataFolder;
    private readonly string _localSettingsFile;

    private readonly bool _isMsix;

    private Dictionary<string, object> _settings;
    private bool _isInitialized;

    public LocalSettingsService(IFileService fileService, IOptions<LocalSettingsOptions> options)
    {
        _isMsix = false; // RuntimeHelper.IsMSIX;

        _fileService = fileService;
        _options = options.Value;

        _applicationDataFolder = Path.Combine(_localApplicationData, _options.ApplicationDataFolder ?? DefaultApplicationDataFolder);
        _localSettingsFile = _options.LocalSettingsFile ?? DefaultLocalSettingsFile;

        _settings = new Dictionary<string, object>();
    }

    private async Task InitializeAsync()
    {
        if (!_isInitialized)
        {
            _settings = await Task.Run(() => _fileService.Read<Dictionary<string, object>>(_applicationDataFolder, _localSettingsFile)) ?? new Dictionary<string, object>();

            _isInitialized = true;
        }
    }

    public async Task<bool> HasSettingAsync(string key)
    {
        if (_isMsix)
        {
            return ApplicationData.Current.LocalSettings.Values.ContainsKey(key);
        }
        else
        {
            await InitializeAsync();

            if (_settings != null)
            {
                return _settings.ContainsKey(key);
            }
        }

        return false;
    }

    public async Task<T?> ReadSettingAsync<T>(string key)
    {
        if (_isMsix)
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out var obj))
            {
                return await Json.ToObjectAsync<T>((string)obj);
            }
        }
        else
        {
            await InitializeAsync();

            if (_settings != null && _settings.TryGetValue(key, out var obj))
            {
                var s = obj.ToString();

                if (s != null)
                {
                    return await Json.ToObjectAsync<T>(s);
                }
            }
        }

        return default;
    }

    public async Task SaveSettingAsync<T>(string key, T value)
    {
        if (_isMsix)
        {
            ApplicationData.Current.LocalSettings.Values[key] = await Json.StringifyAsync(value!);
        }
        else
        {
            await InitializeAsync();

            _settings[key] = await Json.StringifyAsync(value!);

            await Task.Run(() => _fileService.Save(_applicationDataFolder, _localSettingsFile, _settings));
        }
    }
}
