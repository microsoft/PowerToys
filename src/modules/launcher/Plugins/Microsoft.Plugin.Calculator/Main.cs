// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using ManagedCommon;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.Plugin.Calculator
{
    public class Main : IPlugin, IPluginI18n, IDisposable
    {
        private static readonly CalculateEngine CalculateEngine = new CalculateEngine();

        private PluginInitContext Context { get; set; }

        private string IconPath { get; set; }

        private bool _disposed;

        public List<Result> Query(Query query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(paramName: nameof(query));
            }

            if (!CalculateHelper.InputValid(query.Search))
            {
                return new List<Result>();
            }

            try
            {
                // Using CurrentUICulture since this is user facing
                var result = CalculateEngine.Interpret(query.Search, CultureInfo.CurrentUICulture);

                // This could happen for some incorrect queries, like pi(2)
                if (result.Equals(default(CalculateResult)))
                {
                    return new List<Result>();
                }

                return new List<Result>
                {
                    ResultHelper.CreateResult(result.RoundedResult, IconPath),
                };
            } // We want to keep the process alive if any the mages library throws any exceptions.
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Log.Exception("Exception when query for <{query}>", e, GetType());
            }

            return new List<Result>();
        }

        public void Init(PluginInitContext context)
        {
            Context = context ?? throw new ArgumentNullException(paramName: nameof(context));

            Context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(Context.API.GetCurrentTheme());
        }

        // Todo : Update with theme based IconPath
        private void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                IconPath = "Images/calculator.light.png";
            }
            else
            {
                IconPath = "Images/calculator.dark.png";
            }
        }

        private void OnThemeChanged(Theme currentTheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        public string GetTranslatedPluginTitle()
        {
            return Properties.Resources.wox_plugin_calculator_plugin_name;
        }

        public string GetTranslatedPluginDescription()
        {
            return Properties.Resources.wox_plugin_calculator_plugin_description;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Context.API.ThemeChanged -= OnThemeChanged;
                    _disposed = true;
                }
            }
        }
    }
}
