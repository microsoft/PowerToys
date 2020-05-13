using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ColorPicker
{
    class ActionBroker
    {
        public enum ActionTypes
        {
            Click,
        }

        public delegate void Callback(object sender, EventArgs e);

        private Dictionary<ActionTypes, Callback> callbacks = new Dictionary<ActionTypes, Callback>();

        public void AddCallBack(ActionTypes action, Callback callback)
        {
            Callback current;
            if (callbacks.TryGetValue(action, out current))
            {
                current += callback;
            }
            else
            {
                current = callback;
            }
            callbacks.Add(action, current);
        }

        public void ActionTriggered(ActionTypes action, object sender, EventArgs e)
        {
            Callback current;
            if (callbacks.TryGetValue(action, out current))
            {
                current(sender, e);
            }
        }
    }
}
