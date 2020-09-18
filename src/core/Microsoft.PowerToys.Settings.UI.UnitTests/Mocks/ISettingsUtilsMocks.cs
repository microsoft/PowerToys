using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.Utilities;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.PowerToys.Settings.UI.UnitTests.Mocks
{
    internal static class ISettingsUtilsMocks
    {
        //Stubs out empty values for imageresizersettings and general settings as needed by the imageresizer viewmodel
        internal static Mock<ISettingsUtils> GetStubSettingsUtils()
        {
            var settingsUtils = new Mock<ISettingsUtils>();
            settingsUtils.Setup(x => x.GetSettings<It.IsAnyType>(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new InvocationFunc(invocation =>
            {
                var typeArgument = invocation.Method.GetGenericArguments()[0];
                return Activator.CreateInstance(typeArgument);
            }));

            return settingsUtils;
        }
    }
}
