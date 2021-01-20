namespace Mages.Core.Runtime
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents an awaitable object definition.
    /// </summary>
    public sealed class Future : Dictionary<String, Object>
    {
        /// <summary>
        /// Creates a new future object.
        /// </summary>
        public Future()
        {
            Add("done", false);
            Add("result", null);
            Add("error", null);
            Add("notify", null);
        }

        /// <summary>
        /// Gets if the result is already present.
        /// </summary>
        public Boolean IsCompleted
        {
            get
            {
                var result = this.GetProperty("done") as Boolean?;
                return result.HasValue && result.Value;
            }
        }

        /// <summary>
        /// Gets the result, if any.
        /// </summary>
        public Object Result
        {
            get { return this.GetProperty("result"); }
        }

        /// <summary>
        /// Gets the error message, if any.
        /// </summary>
        public String Error
        {
            get { return this.GetProperty("error") as String; }
        }

        /// <summary>
        /// Sets the result in case of success.
        /// </summary>
        /// <param name="result">The concrete result, if any.</param>
        public void SetResult(Object result)
        {
            this["result"] = result;
            SetDone(result, null);
        }

        /// <summary>
        /// Sets the error message in case of failure.
        /// </summary>
        /// <param name="error">The specific error message.</param>
        public void SetError(String error)
        {
            this["error"] = error;
            SetDone(null, error);
        }

        /// <summary>
        /// Sets the callback to notify once finished. This function
        /// is immediately called if the result is already determined.
        /// </summary>
        /// <param name="callback">The callback action.</param>
        public void SetCallback(Action<Object, String> callback)
        {
            if (IsCompleted)
            {
                callback.Invoke(Result, Error);
            }
            else
            {
                this["notify"] = new Function(args =>
                {
                    callback.Invoke(Result, Error);
                    return null;
                });
            }
        }

        private void SetDone(Object result, String error)
        {
            var notify = this["notify"] as Function;
            this["done"] = true;

            if (notify != null)
            {
                notify.Invoke(new [] { result, error });
            }
        }
    }
}
