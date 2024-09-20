// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Models;

public class ExtensionObject<T> // where T : IInspectable
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
