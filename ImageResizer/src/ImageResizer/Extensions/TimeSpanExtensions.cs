namespace System
{
    static class TimeSpanExtensions
    {
        public static TimeSpan Multiply(this TimeSpan timeSpan, double scalar)
            => new TimeSpan((long)(timeSpan.Ticks * scalar));
    }
}
