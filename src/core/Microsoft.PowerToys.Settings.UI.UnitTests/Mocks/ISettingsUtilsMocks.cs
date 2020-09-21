using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.Interface;
using Moq;
using System;

namespace Microsoft.PowerToys.Settings.UI.UnitTests.Mocks
{
    class AnySettingsConfig : ISettingsConfig, ITypeMatcher
    {
        public bool Matches(Type typeArgument) => true;

        public string ToJsonString()
        {
            return string.Empty;
        }
    }

    internal static class ISettingsUtilsMocks
    {
        //Stubs out empty values for imageresizersettings and general settings as needed by the imageresizer viewmodel
        internal static Mock<ISettingsUtils> GetStubSettingsUtils()
        {
            var settingsUtils = new Mock<ISettingsUtils>();
            settingsUtils.Setup(x => x.GetSettings<AnySettingsConfig>(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new InvocationFunc(invocation =>
            {
                var typeArgument = invocation.Method.GetGenericArguments()[0];
                return Activator.CreateInstance(typeArgument);
            }));

            return settingsUtils;
        }
    }
}
