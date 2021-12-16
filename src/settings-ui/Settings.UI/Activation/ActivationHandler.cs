// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.Settings.UI.Activation
{
    // For more information on understanding and extending activation flow see
    // https://github.com/Microsoft/WindowsTemplateStudio/blob/master/docs/activation.md
    internal abstract class ActivationHandler
    {
        public abstract bool CanHandle(object args);

        public abstract Task HandleAsync(object args);
    }

    [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "abstract T and abstract")]
    internal abstract class ActivationHandler<T> : ActivationHandler
        where T : class
    {
        public override async Task HandleAsync(object args)
        {
            await HandleInternalAsync(args as T).ConfigureAwait(false);
        }

        public override bool CanHandle(object args)
        {
            // CanHandle checks the args is of type you have configured
            return args is T && CanHandleInternal(args as T);
        }

        // Override this method to add the activation logic in your activation handler
        protected abstract Task HandleInternalAsync(T args);

        // You can override this method to add extra validation on activation args
        // to determine if your ActivationHandler should handle this activation args
        protected virtual bool CanHandleInternal(T args)
        {
            return true;
        }
    }
}
