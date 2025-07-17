// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

// public partial class ParameterViewModel : ExtensionObjectViewModel
// {
//    public ExtensionObject<ICommandParameter> Model { get; private set; } = new(null);

// protected bool IsInitialized { get; private set; }

// // values from ICommandParameter
//    public string Name { get; private set; } = string.Empty;

// public ParameterType Type { get; private set; } = ParameterType.Text;

// public bool Required { get; private set; } = true;

// public ParameterViewModel(ICommandParameter? parameter, WeakReference<IPageContext> pageContext)
//        : base(pageContext)
//    {
//        Model = new(parameter);
//    }

// public override void InitializeProperties()
//    {
//        if (IsInitialized)
//        {
//            return;
//        }

// var model = Model.Unsafe;
//        if (model == null)
//        {
//            return;
//        }

// Name = model.Name ?? string.Empty;
//        Type = model.Type;
//        Required = model.Required;
//    }
// }
