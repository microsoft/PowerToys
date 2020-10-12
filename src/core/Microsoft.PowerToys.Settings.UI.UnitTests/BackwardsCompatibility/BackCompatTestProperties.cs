using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Moq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;

namespace Microsoft.PowerToys.Settings.UI.UnitTests.BackwardsCompatibility
{
    public static class BackCompatTestProperties
    {
        public const string RootPathStubFiles = "..\\..\\..\\..\\src\\core\\Microsoft.PowerToys.Settings.UI.UnitTests\\BackwardsCompatibility\\TestFiles\\{0}\\Microsoft\\PowerToys\\{1}\\{2}";

        internal class MockSettingsRepository<T> : ISettingsRepository<T> where T : ISettingsConfig, new()
        {
            T _settingsConfig;
            readonly ISettingsUtils _settingsUtils;
            public MockSettingsRepository( ISettingsUtils settingsUtils)
            {
                _settingsUtils = settingsUtils;
            }
            public T SettingsConfig
            {
                get
                {
                    T settingsItem = new T();
                    _settingsConfig = _settingsUtils.GetSettings<T>(settingsItem.GetModuleName());
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
        }


        public static Mock<IIOProvider>GetModuleIOProvider(string version, string module, string fileName)
        {
            
            var stubSettingsPath = string.Format(CultureInfo.InvariantCulture, BackCompatTestProperties.RootPathStubFiles, version, module, fileName);
            Expression<Func<string, bool>> filterExpression = (string s) => s.Contains(module, StringComparison.Ordinal);
            var mockIOProvider = IIOProviderMocks.GetMockIOReadWithStubFile(stubSettingsPath, filterExpression);
            return mockIOProvider;
        }

        public static void VerifyModuleIOProviderWasRead(Mock<IIOProvider> provider, string module,  int expectedCallCount)
        {
            if(provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            Expression<Func<string, bool>> filterExpression = (string s) => s.Contains(module, StringComparison.Ordinal);

            IIOProviderMocks.VerifyIOReadWithStubFile(provider, filterExpression, expectedCallCount);
        }

        public static Mock<IIOProvider> GetGeneralSettingsIOProvider(string version)
        {
            var stubGeneralSettingsPath = string.Format(CultureInfo.InvariantCulture, BackCompatTestProperties.RootPathStubFiles, version, string.Empty, "settings.json");
            Expression<Func<string, bool>> filterExpression = (string s) => s.Contains("Microsoft\\PowerToys\\settings.json", StringComparison.Ordinal);
            var mockGeneralIOProvider = IIOProviderMocks.GetMockIOReadWithStubFile(stubGeneralSettingsPath, filterExpression);
            return mockGeneralIOProvider;
        }

        public static void VerifyGeneralSettingsIOProviderWasRead(Mock<IIOProvider> provider, int expectedCallCount)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            Expression<Func<string, bool>> filterExpression = (string s) => s.Contains("Microsoft\\PowerToys\\settings.json", StringComparison.Ordinal);
            IIOProviderMocks.VerifyIOReadWithStubFile(provider, filterExpression, expectedCallCount);
        }

    }
}
