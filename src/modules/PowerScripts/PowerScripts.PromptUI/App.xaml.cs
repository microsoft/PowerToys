// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using Microsoft.UI.Xaml;

namespace PowerScripts.PromptUI;

/// <summary>
/// Entry point for the WinUI 3 parameter prompt. Reads a <see cref="PromptSpec"/> from the
/// <c>--spec</c> file, shows <see cref="PromptWindow"/>, and lets the window write the chosen values
/// to the <c>--out</c> file. Exit codes: 0 = confirmed, 2 = cancelled/failed (matches the Host's
/// contract, where 2 means "cancelled at the parameter prompt").
/// </summary>
public partial class App : Application
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private Window? _window;

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        string? specPath = null;
        string? outPath = null;

        var cli = Environment.GetCommandLineArgs();
        for (int i = 1; i < cli.Length; i++)
        {
            switch (cli[i])
            {
                case "--spec" when i + 1 < cli.Length:
                    specPath = cli[++i];
                    break;
                case "--out" when i + 1 < cli.Length:
                    outPath = cli[++i];
                    break;
            }
        }

        if (specPath is null || outPath is null || !File.Exists(specPath))
        {
            Environment.Exit(2);
            return;
        }

        PromptSpec spec;
        try
        {
            spec = JsonSerializer.Deserialize<PromptSpec>(File.ReadAllText(specPath), JsonOptions) ?? new PromptSpec();
        }
        catch (Exception)
        {
            Environment.Exit(2);
            return;
        }

        _window = new PromptWindow(spec, outPath);
        _window.Activate();
    }
}
