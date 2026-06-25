// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Microsoft.UI.Xaml;

namespace ColorPicker.Common
{
    /// <summary>
    /// Maps a view-model CLR type to a factory that creates its view, replacing
    /// WPF's implicit <c>DataTemplate DataType="{x:Type VM}"</c> resolution
    /// (which WinUI 3 does not support). Register pairs at startup; call
    /// <see cref="Resolve"/> to obtain a fresh view for a view-model instance.
    /// </summary>
    public sealed class ViewLocator
    {
        private readonly Dictionary<Type, Func<UIElement>> _factories = new();

        public void Register<TViewModel>(Func<UIElement> viewFactory)
        {
            ArgumentNullException.ThrowIfNull(viewFactory);
            _factories[typeof(TViewModel)] = viewFactory;
        }

        public UIElement Resolve(object viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            if (!_factories.TryGetValue(viewModel.GetType(), out var factory))
            {
                throw new KeyNotFoundException($"No view registered for view-model type '{viewModel.GetType().FullName}'.");
            }

            return factory();
        }
    }
}
