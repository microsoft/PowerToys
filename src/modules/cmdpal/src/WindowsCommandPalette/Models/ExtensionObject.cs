// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using Microsoft.CmdPal.Common.Services;
using Microsoft.Windows.CommandPalette.Extensions;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppExtensions;
using Windows.Win32;
using Windows.Win32.System.Com;
using WinRT;

namespace CmdPal.Models;

public class ExtensionObject<T> //  where T : IInspectable
{
    private readonly T _value;

    public ExtensionObject(T value)
    {
        _value = value;
    }

    // public T? Safe {
    //     get {
    //         try {
    //             if (_value!.Equals(_value)) return _value;
    //         } catch (COMException){ /* log something */ }
    //         return default;
    //     }
    // }
    public T Unsafe => _value;
}
