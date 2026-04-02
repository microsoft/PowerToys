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

        Assert.IsTrue(summary.StartsWith("Python syntax error:", StringComparison.Ordinal), $"Summary: {summary}");
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

        Assert.IsTrue(summary.Contains("ZeroDivisionError"), $"Summary: {summary}");
        Assert.IsTrue(!string.IsNullOrEmpty(details));
    }

    [TestMethod]
    public void ParsePythonError_EmptyStderr()
    {
        var (summary, details) = PythonScriptService.ParsePythonError(string.Empty);

        Assert.IsTrue(!string.IsNullOrEmpty(summary));
        Assert.AreEqual(string.Empty, details);
    }
}
