using System;

namespace VolumeProfileX
{
    public sealed class PeriodProfile
    {
        public readonly long Index;
        public readonly ProfileWindow Win = new ProfileWindow();
        public readonly ProfileSnapshot Snap = new ProfileSnapshot();

        public PeriodProfile(long index)
        {
            Index = index;
            Win.Configure(1, DateTime.MinValue);
        }
    }

    public static class PeriodMath
    {
        public static readonly DateTime Origin = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        public static DateTime ToBaseTime(DateTime utc, AnchorTimeBase b, TimeSpan exchangeOffset)
        {
            switch (b)
            {
                case AnchorTimeBase.Utc: return utc;
                case AnchorTimeBase.Exchange: return utc + exchangeOffset;
                default:
                    try { return utc + TimeZoneInfo.Local.GetUtcOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)); }
                    catch (ArgumentException) { return utc; }
            }
        }

        public static long Index(DateTime utc, double hours, AnchorTimeBase b, TimeSpan exchangeOffset, DateTime? anchorUtc)
        {
            if (!(hours > 0)) hours = 1.0 / 3600.0;
            if (anchorUtc.HasValue)
                return utc < anchorUtc.Value ? long.MinValue : (long)Math.Floor((utc - anchorUtc.Value).TotalHours / hours);
            var local = ToBaseTime(utc, b, exchangeOffset);
            return (long)Math.Floor((local - Origin).TotalHours / hours);
        }
    }
}
