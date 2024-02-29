// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO.Abstractions;
using System.Linq.Expressions;
using System.Text;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Moq;

namespace Microsoft.PowerToys.Settings.UI.UnitTests.BackwardsCompatibility
{
    public static class BackCompatTestProperties
    {
        public const string RootPathStubFiles = "..\\..\\..\\..\\..\\src\\settings-ui\\Settings.UI.UnitTests\\BackwardsCompatibility\\TestFiles\\{0}\\Microsoft\\PowerToys\\{1}\\{2}";

        // Using Ordinal since this is used internally for a path
        private static readonly Expression<Func<string, bool>> SettingsFilterExpression = s => s == null || s.Contains("Microsoft\\PowerToys\\settings.json", StringComparison.Ordinal);

        private static readonly CompositeFormat RootPathStubFilesCompositeFormat = System.Text.CompositeFormat.Parse(BackCompatTestProperties.RootPathStubFiles);

        internal sealed class MockSettingsRepository<T> : ISettingsRepository<T>
            where T : ISettingsConfig, new()
        {
            private readonly ISettingsUtils _settingsUtils;
            private T _settingsConfig;

            public MockSettingsRepository(ISettingsUtils settingsUtils)
            {
                _settingsUtils = settingsUtils;
            }

            public T SettingsConfig
            {
                get
                {
                    T settingsItem = new T();
                    _settingsConfig = _settingsUtils.GetSettingsOrDefault<T>(settingsItem.GetModuleName());
                    return _settingsConfig;
                }

                set
                {
                    if (value != null)
                    {
                        _settingsConfig = value;
                    }
                }
            }

            public bool ReloadSettings()
            {
                try
                {
                    T settingsItem = new T();
                    _settingsConfig = _settingsUtils.GetSettingsOrDefault<T>(settingsItem.GetModuleName());

                    SettingsConfig = _settingsConfig;

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static Mock<IFile> GetModuleIOProvider(string version, string module, string fileName)
        {
            var stubSettingsPath = StubSettingsPath(version, module, fileName);
            Expression<Func<string, bool>> filterExpression = ModuleFilterExpression(module);
            return IIOProviderMocks.GetMockIOReadWithStubFile(stubSettingsPath, filterExpression);
        }

        public static string StubGeneralSettingsPath(string version)
        {
            return StubSettingsPath(version, string.Empty, "settings.json");
        }

        public static string StubSettingsPath(string version, string module, string fileName)
        {
            return string.Format(CultureInfo.InvariantCulture, RootPathStubFilesCompositeFormat, version, module, fileName);
        }

        public static void VerifyModuleIOProviderWasRead(Mock<IFile> provider, string module, int expectedCallCount)
        {
            ArgumentNullException.ThrowIfNull(provider);

            Expression<Func<string, bool>> filterExpression = ModuleFilterExpression(module);

            IIOProviderMocks.VerifyIOReadWithStubFile(provider, filterExpression, expectedCallCount);
        }

        private static Expression<Func<string, bool>> ModuleFilterExpression(string module)
        {
            // Using Ordinal since this is used internally for a path
            return s => s == null || s.Contains(module, StringComparison.Ordinal);
        }

        public static Mock<IFile> GetGeneralSettingsIOProvider(string version)
        {
            var stubGeneralSettingsPath = StubGeneralSettingsPath(version);
            return IIOProviderMocks.GetMockIOReadWithStubFile(stubGeneralSettingsPath, SettingsFilterExpression);
        }

        public static void VerifyGeneralSettingsIOProviderWasRead(Mock<IFile> provider, int expectedCallCount)
        {
            ArgumentNullException.ThrowIfNull(provider);

            IIOProviderMocks.VerifyIOReadWithStubFile(provider, SettingsFilterExpression, expectedCallCount);
        }
    }
}
