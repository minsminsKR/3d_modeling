using System;
using System.Globalization;

namespace HappyToy.V2
{
    // Observation-school progress is independent of play time and seam count.
    public sealed class SchoolProgressClock
    {
        public const int MaximumHours=1000000;
        public int Hours {get;private set;}
        public string Display=>Format(Hours);
        public bool AdvanceForTraversal(int direction)
        {
            if(direction!=1&&direction!=-1)throw new ArgumentOutOfRangeException(nameof(direction));
            if(direction<0||Hours==MaximumHours)return false;
            Hours++;return true;
        }
        public void Restore(int hours){Validate(hours);Hours=hours;}
        public static string Format(int hours)
        {Validate(hours);return hours.ToString("00",CultureInfo.InvariantCulture)+":00";}
        public static void Validate(int hours)
        {if(hours<0||hours>MaximumHours)throw new ArgumentOutOfRangeException(nameof(hours));}
    }
}
