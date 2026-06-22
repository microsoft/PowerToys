using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.CmdPal.Ext.Apps.Programs;
using System;
using System.IO;
using System.Reflection;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests
{
    [TestClass]
    public class SystemAppDetectionTests
    {
        private static MethodInfo? _isProtectedMethod;

        [ClassInitialize]
        public static void ClassInit(TestContext _)
        {
            _isProtectedMethod = typeof(Win32Program).GetMethod("IsProtectedSystemApp", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(_isProtectedMethod, "Could not find IsProtectedSystemApp via reflection. If method was renamed/moved update tests accordingly.");
        }

        private bool IsProtected(Win32Program program)
        {
            return (bool)_isProtectedMethod!.Invoke(null, new object[] { program })!;
        }

        [TestMethod]
        public void CaseInsensitivity_PathDifferingByCase_IsDetected()
        {
            var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var system32 = Path.Combine(systemRoot, "System32");

            // create a path that differs only by case
            var path = Path.Combine(systemRoot.ToLowerInvariant(), "system32", "notepad.exe");

            var program = TestDataHelper.CreateTestWin32Program(fullPath: Path.GetFullPath(path));

            Assert.IsTrue(IsProtected(program), "Paths that differ only by case should be considered system-owned.");
        }

        [TestMethod]
        public void TrailingSlash_PathsWithAndWithoutTrailingSeparators_AreHandled()
        {
            var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var system32 = Path.Combine(systemRoot, "System32");

            var pathWithSlash = system32 + Path.DirectorySeparatorChar + "notepad.exe";
            var pathWithoutSlash = Path.Combine(system32, "notepad.exe");

            var progA = TestDataHelper.CreateTestWin32Program(fullPath: Path.GetFullPath(pathWithSlash));
            var progB = TestDataHelper.CreateTestWin32Program(fullPath: Path.GetFullPath(pathWithoutSlash));

            Assert.IsTrue(IsProtected(progA), "Path with trailing separator should be detected as system-owned.");
            Assert.IsTrue(IsProtected(progB), "Path without trailing separator should be detected as system-owned.");
        }

        [TestMethod]
        public void DirectoryBoundary_DoesNotFalsePositive_WhenPrefixOnly()
        {
            var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

            // A directory that starts with the System32 token but is not inside System32
            var fakeSystemLike = Path.Combine(systemRoot, "System32Apps", "MyApp.exe");

            var program = TestDataHelper.CreateTestWin32Program(fullPath: Path.GetFullPath(fakeSystemLike));

            // The desired behavior: this should NOT be considered inside System32.
            // If this test fails it indicates the detection logic is using simple prefix matching
            // and may incorrectly treat System32Apps as a system directory.
            Assert.IsFalse(IsProtected(program), "A path such as C:\\Windows\\System32Apps\\MyApp should NOT be treated as inside C:\\Windows\\System32.");
        }

        [TestMethod]
        public void DriveLetterNormalization_ReduceCaseDifferences_CorrectlyDetected()
        {
            var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var system32 = Path.Combine(systemRoot, "System32");

            // lower-case drive letter and mixed-case folders
            var path = system32.Replace("C:", "c:").Replace("System32", "system32");
            var program = TestDataHelper.CreateTestWin32Program(fullPath: Path.GetFullPath(path));

            Assert.IsTrue(IsProtected(program), "Drive-letter and folder casing differences should not affect detection.");
        }

        [TestMethod]
        public void RelativePath_Normalization_GetFullPathHandled()
        {
            var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var system32 = Path.Combine(systemRoot, "System32");

            // Construct a path that contains a relative segment and ensure GetFullPath normalization is considered
            var pathWithRelative = Path.Combine(systemRoot, "System32", "..", "System32", "notepad.exe");
            var normalized = Path.GetFullPath(pathWithRelative);

            var program = TestDataHelper.CreateTestWin32Program(fullPath: normalized);

            Assert.IsTrue(IsProtected(program), "Normalized paths that resolve into System32 should be detected as system-owned.");
        }
    }
}
