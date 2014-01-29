using System;
using System.Reflection;
using NUnit.Framework;
using Python.Runtime;

namespace Wox.Test
{
    [TestFixture]
    public class PyImportTest
    {
        private IntPtr gs;

        [SetUp]
        public void SetUp()
        {
            PythonEngine.Initialize();
            gs = PythonEngine.AcquireLock();

            //string here = Environment.CurrentDirectory;
            // trunk\pythonnet\src\embed_tests\bin\Debug

            /* 
             * Append the tests directory to sys.path
             * using reflection to circumvent the private modifires placed on most Runtime methods.
             */
            string s = @"d:\github\pythonnet\pythonnet\src\tests";

            Type RTClass = typeof(Runtime);

            /* pyStrPtr = PyString_FromString(s); */
            MethodInfo PyString_FromString = RTClass.GetMethod("PyString_FromString", BindingFlags.Public | BindingFlags.Static);
            object[] funcArgs = new object[1];
            funcArgs[0] = s;
            IntPtr pyStrPtr = (IntPtr)PyString_FromString.Invoke(null, funcArgs);

            /* SysDotPath = sys.path */
            MethodInfo PySys_GetObject = RTClass.GetMethod("PySys_GetObject", BindingFlags.Public | BindingFlags.Static);
            funcArgs[0] = "path";
            IntPtr SysDotPath = (IntPtr)PySys_GetObject.Invoke(null, funcArgs);

            /* SysDotPath.append(*pyStrPtr) */
            MethodInfo PyList_Append = RTClass.GetMethod("PyList_Append", BindingFlags.Public | BindingFlags.Static);
            funcArgs = new object[2];
            funcArgs[0] = SysDotPath;
            funcArgs[1] = pyStrPtr;
            int r = (int)PyList_Append.Invoke(null, funcArgs);
        }

        [TearDown]
        public void TearDown()
        {
            PythonEngine.ReleaseLock(gs);
            PythonEngine.Shutdown();
        }

        [Test]
        public void TestDottedName()
        {
            PyObject module = PythonEngine.ImportModule("PyImportTest.test.one");
            Assert.IsNotNull(module, ">>>  import PyImportTest.test.one  # FAILED");
        }
    }
}
