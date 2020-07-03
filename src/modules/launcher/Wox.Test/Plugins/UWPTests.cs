using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Wox.Infrastructure;
using Wox.Plugin;
using Microsoft.Plugin.Program.Programs;
using Moq;
using System.IO;

namespace Wox.Test.Plugins
{
    class UWPTests
    {
        public class mockUWP : UWP.Application
        {
           /* public mockUWP(AppxPackageHelper.IAppxManifestApplication manifestApp, UWP package) : base( manifestApp, package)
            {
                
            }*/

            protected override int Score(string query)
            {
                return 100;
            }

            protected override List<int> GetMatchingData(string query)
            {
                return null;
            }
        }

        [Test]
        public void Win32Apps_ShouldSetNameAsTitle_WhileCreatingResult()
        {
            Mock<IPublicAPI> mockAPI = new Mock<IPublicAPI>();
            mockAPI.Setup(m => m.GetTranslation(It.IsAny<string>())).Returns("subtitle");

            mockUWP item = new mockUWP();
            item.DisplayName = "Name";
            item.Description = "NameName";

            Result res = item.Result("Name", mockAPI.Object);

            Assert.IsTrue(res.Title.Equals("Name"));
        }
    }
}
