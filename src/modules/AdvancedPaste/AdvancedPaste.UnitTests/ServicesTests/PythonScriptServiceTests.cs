// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

using AdvancedPaste.Services.PythonScripts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdvancedPaste.UnitTests.ServicesTests;

[TestClass]
public sealed class PythonScriptServiceTests
{
    [TestMethod]
    public void MergeWithAutoDetectedImports_DetectsSimpleImports()
    {
        var lines = new[]
        {
            "# @advancedpaste:name test",
            "import requests",
            "import numpy",
            "import os",
            "import sys",
        };

        var result = PythonScriptService.MergeWithAutoDetectedImports(lines, []);

        Assert.AreEqual(2, result.Count); // requests + numpy; os and sys are stdlib
        Assert.IsTrue(result.Any(r => r.ImportName == "requests" && r.PipPackage == "requests"));
        Assert.IsTrue(result.Any(r => r.ImportName == "numpy" && r.PipPackage == "numpy"));
    }

    [TestMethod]
    public void MergeWithAutoDetectedImports_DetectsFromImports()
    {
        var lines = new[]
        {
            "from PIL import Image",
            "from markitdown import MarkItDown",
        };

        var result = PythonScriptService.MergeWithAutoDetectedImports(lines, []);

        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.Any(r => r.ImportName == "PIL" && r.PipPackage == "Pillow"));
        Assert.IsTrue(result.Any(r => r.ImportName == "markitdown" && r.PipPackage == "markitdown"));
    }

    [TestMethod]
    public void MergeWithAutoDetectedImports_WellKnownMappings()
    {
        var lines = new[]
        {
            "import cv2",
            "import win32clipboard",
            "import yaml",
        };

        var result = PythonScriptService.MergeWithAutoDetectedImports(lines, []);

        Assert.AreEqual(3, result.Count);
        Assert.IsTrue(result.Any(r => r.ImportName == "cv2" && r.PipPackage == "opencv-python"));
        Assert.IsTrue(result.Any(r => r.ImportName == "win32clipboard" && r.PipPackage == "pywin32"));
        Assert.IsTrue(result.Any(r => r.ImportName == "yaml" && r.PipPackage == "PyYAML"));
    }

    [TestMethod]
    public void MergeWithAutoDetectedImports_ExplicitRequirementsTakePrecedence()
    {
        var lines = new[]
        {
            "import cv2",
            "import requests",
        };

        var explicitReqs = new List<PythonRequirement>
        {
            new("cv2", "opencv-python-headless"),
        };

        var result = PythonScriptService.MergeWithAutoDetectedImports(lines, explicitReqs);

        Assert.AreEqual(2, result.Count);

        // cv2 should use the explicit pip package name, not the well-known mapping
        var cv2Req = result.First(r => r.ImportName == "cv2");
        Assert.AreEqual("opencv-python-headless", cv2Req.PipPackage);

        // requests should be auto-detected
        Assert.IsTrue(result.Any(r => r.ImportName == "requests"));
    }

    [TestMethod]
    public void MergeWithAutoDetectedImports_SkipsStdlib()
    {
        var lines = new[]
        {
            "import os",
            "import sys",
            "import json",
            "import io",
            "import pathlib",
            "import tempfile",
            "import subprocess",
        };

        var result = PythonScriptService.MergeWithAutoDetectedImports(lines, []);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void MergeWithAutoDetectedImports_SkipsComments()
    {
        var lines = new[]
        {
            "# import requests",
            "# from PIL import Image",
            "import json",
        };

        var result = PythonScriptService.MergeWithAutoDetectedImports(lines, []);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void MergeWithAutoDetectedImports_HandlesMultipleImportsOnOneLine()
    {
        var lines = new[]
        {
            "import requests, numpy, pandas",
        };

        var result = PythonScriptService.MergeWithAutoDetectedImports(lines, []);

        Assert.AreEqual(3, result.Count);
        Assert.IsTrue(result.Any(r => r.ImportName == "requests"));
        Assert.IsTrue(result.Any(r => r.ImportName == "numpy"));
        Assert.IsTrue(result.Any(r => r.ImportName == "pandas"));
    }

    [TestMethod]
    public void MergeWithAutoDetectedImports_HandlesSubmoduleImport()
    {
        var lines = new[]
        {
            "import win32com.client",
            "from llama_cpp import Llama",
        };

        var result = PythonScriptService.MergeWithAutoDetectedImports(lines, []);

        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.Any(r => r.ImportName == "win32com" && r.PipPackage == "pywin32"));
        Assert.IsTrue(result.Any(r => r.ImportName == "llama_cpp" && r.PipPackage == "llama-cpp-python"));
    }

    [TestMethod]
    public void ParsePythonError_ModuleNotFoundError()
    {
        var stderr = """
            Traceback (most recent call last):
              File "C:\scripts\reverse.py", line 4, in <module>
                import win32clipboard
            ModuleNotFoundError: No module named 'win32clipboard'
            """;

        var (summary, details) = PythonScriptService.ParsePythonError(stderr);

        Assert.IsTrue(summary.Contains("reverse.py"), $"Summary should mention the script: {summary}");
        Assert.IsTrue(summary.Contains("line 4"), $"Summary should mention the line: {summary}");
        Assert.IsTrue(summary.Contains("win32clipboard"), $"Summary should mention the module: {summary}");
        Assert.IsTrue(summary.Contains("pywin32"), $"Summary should suggest pip package: {summary}");
        Assert.IsTrue(!string.IsNullOrEmpty(details));
    }

    [TestMethod]
    public void ParsePythonError_SyntaxError()
    {
        var stderr = """
            File "test.py", line 5
                def foo(
                       ^
            SyntaxError: unexpected EOF while parsing
            """;

        var (summary, details) = PythonScriptService.ParsePythonError(stderr);

        Assert.IsTrue(summary.Contains("test.py"), $"Summary should mention the script: {summary}");
        Assert.IsTrue(summary.Contains("line 5"), $"Summary should mention the line: {summary}");
        Assert.IsTrue(summary.Contains("Python syntax error:"), $"Summary: {summary}");
        Assert.IsTrue(!string.IsNullOrEmpty(details));
    }

    [TestMethod]
    public void ParsePythonError_SyntaxErrorWithColumn()
    {
        var stderr = "  File \"script.py\", line 3\n    x = (1 +\n        ^\nSyntaxError: '(' was never closed\n";

        var (summary, details) = PythonScriptService.ParsePythonError(stderr);

        Assert.IsTrue(summary.Contains("script.py"), $"Summary should mention the script: {summary}");
        Assert.IsTrue(summary.Contains("line 3"), $"Summary should mention the line: {summary}");
        Assert.IsTrue(summary.Contains("col"), $"Summary should mention the column: {summary}");
        Assert.IsTrue(summary.Contains("Python syntax error:"), $"Summary: {summary}");
        Assert.IsTrue(!string.IsNullOrEmpty(details));
    }

    [TestMethod]
    public void ParsePythonError_GenericError()
    {
        var stderr = """
            Traceback (most recent call last):
              File "test.py", line 10, in <module>
                result = 1 / 0
            ZeroDivisionError: division by zero
            """;

        var (summary, details) = PythonScriptService.ParsePythonError(stderr);

        Assert.IsTrue(summary.Contains("test.py"), $"Summary should mention the script: {summary}");
        Assert.IsTrue(summary.Contains("line 10"), $"Summary should mention the line: {summary}");
        Assert.IsTrue(summary.Contains("ZeroDivisionError"), $"Summary: {summary}");
        Assert.IsTrue(!string.IsNullOrEmpty(details));
    }

    [TestMethod]
    public void ParsePythonError_NestedTraceback_ShowsLastFrame()
    {
        var stderr = """
            Traceback (most recent call last):
              File "main.py", line 5, in <module>
                helper()
              File "helper.py", line 12, in helper
                do_work()
              File "worker.py", line 8, in do_work
                raise RuntimeError("bad state")
            RuntimeError: bad state
            """;

        var (summary, details) = PythonScriptService.ParsePythonError(stderr);

        Assert.IsTrue(summary.Contains("worker.py"), $"Summary should mention the last script in the chain: {summary}");
        Assert.IsTrue(summary.Contains("line 8"), $"Summary should mention the line of the last frame: {summary}");
        Assert.IsTrue(summary.Contains("bad state"), $"Summary should contain the error message: {summary}");
    }

    [TestMethod]
    public void ParsePythonError_EmptyStderr()
    {
        var (summary, details) = PythonScriptService.ParsePythonError(string.Empty);

        Assert.IsTrue(!string.IsNullOrEmpty(summary));
        Assert.AreEqual(string.Empty, details);
    }

    [TestMethod]
    public void ParsePythonError_NoTraceback_PlainStderr()
    {
        var stderr = "Something went wrong in the script\n";

        var (summary, details) = PythonScriptService.ParsePythonError(stderr);

        // No File "..." reference, so no location — just the message
        Assert.IsTrue(summary.Contains("Something went wrong"), $"Summary: {summary}");
        Assert.IsFalse(summary.Contains("line"), $"Summary should not contain 'line' without a traceback: {summary}");
    }

    [TestMethod]
    public void ExtractLastTracebackLocation_BasicTraceback()
    {
        var lines = new[]
        {
            "Traceback (most recent call last):",
            "  File \"script.py\", line 10, in <module>",
            "    result = 1 / 0",
            "ZeroDivisionError: division by zero",
        };

        var location = PythonScriptService.ExtractLastTracebackLocation(lines);

        Assert.IsNotNull(location);
        Assert.AreEqual("script.py", location.Value.FileName);
        Assert.AreEqual(10, location.Value.Line);
        Assert.IsNull(location.Value.Column);
    }

    [TestMethod]
    public void ExtractLastTracebackLocation_WithCaret()
    {
        var lines = new[]
        {
            "  File \"test.py\", line 5",
            "    def foo(",
            "           ^",
            "SyntaxError: unexpected EOF while parsing",
        };

        var location = PythonScriptService.ExtractLastTracebackLocation(lines);

        Assert.IsNotNull(location);
        Assert.AreEqual("test.py", location.Value.FileName);
        Assert.AreEqual(5, location.Value.Line);
        Assert.IsNotNull(location.Value.Column);
    }

    [TestMethod]
    public void ExtractLastTracebackLocation_FullPath_ReturnsBasename()
    {
        var lines = new[]
        {
            "Traceback (most recent call last):",
            "  File \"C:\\Users\\user\\scripts\\my_script.py\", line 42, in <module>",
            "    some_call()",
            "ValueError: invalid value",
        };

        var location = PythonScriptService.ExtractLastTracebackLocation(lines);

        Assert.IsNotNull(location);
        Assert.AreEqual("my_script.py", location.Value.FileName);
        Assert.AreEqual(42, location.Value.Line);
    }

    [TestMethod]
    public void ExtractLastTracebackLocation_NoFileLine_ReturnsNull()
    {
        var lines = new[]
        {
            "Some random error output",
            "No traceback here",
        };

        var location = PythonScriptService.ExtractLastTracebackLocation(lines);

        Assert.IsNull(location);
    }

    [TestMethod]
    public void ParsePipInstallError_ExtractsErrorLine()
    {
        var stderr = """
            Collecting some-package
              Downloading some-package-1.0.tar.gz (15 kB)
            ERROR: Could not find a version that satisfies the requirement some-package (from versions: none)
            ERROR: No matching distribution found for some-package
            """;

        var (summary, fullStderr) = PythonScriptService.ParsePipInstallError(stderr);

        Assert.IsTrue(summary.Contains("No matching distribution"), $"Summary should contain the last ERROR line: {summary}");
        Assert.IsTrue(!string.IsNullOrEmpty(fullStderr));
    }

    [TestMethod]
    public void ParsePipInstallError_NoErrorPrefix_UsesLastLine()
    {
        var stderr = "permission denied: /usr/lib/python3/dist-packages\n";

        var (summary, fullStderr) = PythonScriptService.ParsePipInstallError(stderr);

        Assert.IsTrue(summary.Contains("permission denied"), $"Summary: {summary}");
        Assert.IsTrue(!string.IsNullOrEmpty(fullStderr));
    }

    [TestMethod]
    public void ParsePipInstallError_EmptyStderr()
    {
        var (summary, fullStderr) = PythonScriptService.ParsePipInstallError(string.Empty);

        Assert.AreEqual("unknown error", summary);
        Assert.AreEqual(string.Empty, fullStderr);
    }
}
