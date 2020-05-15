using System.Runtime.InteropServices;


namespace MenusWPF.Models
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MonitorResolutionSettings
    {
        [MarshalAs(UnmanagedType.BStr)]
        public string displayAdapterName;

        [MarshalAs(UnmanagedType.BStr)]
        public string monitorName;

        public Resolution currentResolution;

        public Resolution res1; // TODO this should be an array, but it requires more complex marshalling. 
        public Resolution res2;
        public Resolution res3;
        public Resolution res4;
        public Resolution res5;


        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public Resolution[] resolutionOptions;
    }
}
