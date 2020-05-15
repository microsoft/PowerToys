using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;


namespace MenusWPF.Models
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Resolution
    {
        public int width;
        public int height;
    }


}
