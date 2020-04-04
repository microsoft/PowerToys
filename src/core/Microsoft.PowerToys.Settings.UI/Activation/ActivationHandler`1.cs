// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace Microsoft.PowerToys.Settings.UI.Activation
{

    // Extend this class to implement new ActivationHandlers
    internal abstract class ActivationHandler<T> : ActivationHandler
        where T : class
    {
        public override async Task HandleAsync(object args)
        {
            await this.HandleInternalAsync(args as T);
        }

        public override bool CanHandle(object args)
        {
            // CanHandle checks the args is of type you have configured
            return args is T && this.CanHandleInternal(args as T);
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
