using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ColorPicker
{
    public class ActionBroker
    {
        public enum ActionTypes
        {
            Click,
            Escape,
        }

        public delegate void Callback(object sender, EventArgs e);

        private Dictionary<ActionTypes, Callback> _callbacks = new Dictionary<ActionTypes, Callback>();

        public void AddCallback(ActionTypes action, Callback callback)
        {
            Callback current;
            if (_callbacks.TryGetValue(action, out current))
            {
                current += callback;
            }
            else
            {
                current = callback;
            }
            _callbacks.Add(action, current);
        }

        public void ActionTriggered(ActionTypes action, object sender, EventArgs e)
        {
            Callback current;
            if (_callbacks.TryGetValue(action, out current))
            {
                current(sender, e);
            }
        }
    }
}
