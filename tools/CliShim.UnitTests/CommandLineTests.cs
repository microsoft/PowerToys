// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerToys.CliShim.UnitTests;

[TestClass]
public sealed class CommandLineTests
{
    [DataTestMethod]

    // Normal shell launches: unquoted program name, ends at first whitespace.
    [DataRow("fancyzones arg", "arg")]
    [DataRow("fancyzones a b c", "a b c")]
    [DataRow("fancyzones", "")]
    [DataRow("filelocksmith", "")]

    // Quoted program name (path with spaces): ends at the closing quote, no backslash-escaping.
    [DataRow(@"""C:\Program Files\PowerToys\cli\fancyzones.exe"" arg", "arg")]
    [DataRow(@"""C:\Program Files\PowerToys\cli\fancyzones.exe""", "")]

    // The user's exact quoting in the tail is preserved verbatim (the whole point of the shim).
    [DataRow(@"""C:\cli\fancyzones.exe"" ""a b""", @"""a b""")]
    [DataRow(@"fancyzones --path ""C:\a b\c.png""", @"--path ""C:\a b\c.png""")]

    // Tabs count as whitespace for both the argv[0] terminator and the trim.
    [DataRow("fancyzones\targ", "arg")]
    [DataRow("fancyzones \t arg", "arg")]

    // Regression: a command line padded with leading whitespace must NOT leak the program name
    // (a non-shell parent can pass this via CreateProcessW; the OS loader never does).
    [DataRow("  fancyzones arg", "arg")]
    [DataRow(" fancyzones", "")]
    [DataRow(@"  ""C:\cli\fancyzones.exe"" arg", "arg")]

    // Degenerate inputs.
    [DataRow("", "")]

    // Unterminated argv[0] quote: ends at end-of-string (CRT-faithful), so no arguments remain.
    [DataRow(@"""C:\Program Files\app", "")]
    public void StripArgumentZero_ReturnsForwardedTail(string commandLine, string expected)
    {
        Assert.AreEqual(expected, CommandLine.StripArgumentZero(commandLine));
    }
}
