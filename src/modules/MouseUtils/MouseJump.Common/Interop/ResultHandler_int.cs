// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MouseJump.Common.Interop;

public static partial class ResultHandler
{
    /*
       helpers for handling results from Win32 API calls that return an int value,
       throwing exceptions when errors occur, and reading the last error code when necessary.
    */

    public static void ThrowIfZero(
        int result,
        bool getLastError = false,
        [CallerMemberName] string memberName = "")
    {
        ResultHandler.HandleResult(
            result,
            result != 0,
            getLastError,
            memberName);
    }

    public static void ThrowIfNotZero(
        int result,
        bool getLastError = false,
        [CallerMemberName] string memberName = "")
    {
        ResultHandler.HandleResult(
            result,
            result == 0,
            getLastError,
            memberName);
    }

    public static void HandleResult(
        int result,
        bool success,
        bool getLastError = false,
        [CallerMemberName] string memberName = "")
    {
        if (success)
        {
            return;
        }

        ResultHandler.HandleFailure(result, getLastError, memberName);
    }

    public static void HandleResult(
        int result,
        bool success,
        Func<int>? getLastError,
        [CallerMemberName] string memberName = "")
    {
        if (success)
        {
            return;
        }

        var lastError = getLastError?.Invoke();
        ResultHandler.HandleFailure(result, lastError, memberName);
    }

    public static void HandleResult(
        int result,
        bool success,
        int? lastError,
        [CallerMemberName] string memberName = "")
    {
        if (success)
        {
            return;
        }

        ResultHandler.HandleFailure(result, lastError, memberName);
    }

    public static void HandleFailure(
        int result,
        bool getLastError = false,
        [CallerMemberName] string memberName = "")
    {
        var lastError = getLastError ? (int?)Marshal.GetLastPInvokeError() : null;
        ResultHandler.HandleFailure(result, lastError, memberName);
    }

    public static void HandleFailure(
        int result,
        Func<int>? getLastError,
        [CallerMemberName] string memberName = "")
    {
        var lastError = getLastError?.Invoke();
        ResultHandler.HandleFailure(result, lastError, memberName);
    }

    public static void HandleFailure(
        int result,
        int? lastError,
        [CallerMemberName] string memberName = "")
    {
        var lines = new List<string>
        {
            $"{memberName} failed with result {result}.",
        };

        if (lastError is not null)
        {
            lines.Add($"last error was '{lastError}'");
        }

        var message = string.Join(Environment.NewLine, lines);
        throw new Win32Exception(message);
    }
}
