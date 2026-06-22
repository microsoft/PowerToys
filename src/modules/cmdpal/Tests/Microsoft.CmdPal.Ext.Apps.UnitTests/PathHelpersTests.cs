using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.CmdPal.Ext.Apps.Utils;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests
{
    [TestClass]
    public class PathHelpersTests
    {
        [TestMethod]
        public void IsPathInsideDirectory_System32Child_ReturnsTrue()
        {
            var systemRoot = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Windows);
            var system32 = System.IO.Path.Combine(systemRoot, "System32");
            var child = System.IO.Path.Combine(system32, "cmd.exe");

            Assert.IsTrue(PathHelpers.IsPathInsideDirectory(child, system32));
        }

        [TestMethod]
        public void IsPathInsideDirectory_SimilarPrefixNotChild_ReturnsFalse()
        {
            var systemRoot = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Windows);
            var system32 = System.IO.Path.Combine(systemRoot, "System32");
            // Construct a path that contains the sequence but is not under System32
            var notChild = System.IO.Path.Combine(systemRoot, "System32Apps", "MyApp.exe");

            Assert.IsFalse(PathHelpers.IsPathInsideDirectory(notChild, system32));
        }

        [TestMethod]
        public void IsPathInsideDirectory_TrailingSeparatorHandling_ReturnsTrue()
        {
            var systemRoot = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Windows);
            var system32 = System.IO.Path.Combine(systemRoot, "System32") + System.IO.Path.DirectorySeparatorChar;
            var child = System.IO.Path.Combine(systemRoot, "System32", "notepad.exe");

            Assert.IsTrue(PathHelpers.IsPathInsideDirectory(child, system32));
        }
    }
}
