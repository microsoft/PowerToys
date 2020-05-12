using System;
using System.Collections.Generic;
using System.Text;

namespace Telemetry.Events
{
    public enum PartA_PrivTags
           : ulong
    {
        /// <nodoc/>
        None = 0,

        /// <nodoc/>
        ProductAndServicePerformance = 0x0000000001000000u,

        /// <nodoc/>
        ProductAndServiceUsage = 0x0000000002000000u,
    }

    public interface IEvent
    {
        PartA_PrivTags PartA_PrivTags { get; }
    }
}
