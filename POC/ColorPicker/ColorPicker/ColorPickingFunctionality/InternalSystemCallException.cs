using System;


namespace ColorPicker.ColorPickingFunctionality
{ 
    class InternalSystemCallException : Exception
    {
        public InternalSystemCallException(string message) : base(message) { }
    }
}
