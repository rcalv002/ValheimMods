using System;
using System.Collections.Generic;
using System.Globalization;

namespace HeadingCompass;

internal static class Heading
{
    private static readonly string[] Directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
    internal static double Normalize(double degrees) =>
        double.IsNaN(degrees) || double.IsInfinity(degrees) ? 0 : (degrees % 360 + 360) % 360;

    internal static string Direction(double degrees) => Directions[(int)Math.Floor((Normalize(degrees) + 22.5) / 45) % 8];
    internal static int Rounded(double degrees) => (int)Math.Floor(Normalize(degrees) + 0.5) % 360;
    internal static string Readout(double degrees)
    {
        int rounded = Rounded(degrees);
        return Direction(rounded) + "  " + rounded.ToString("000", CultureInfo.InvariantCulture) + "°";
    }

    internal readonly struct Tick
    {
        internal readonly float Fraction;
        internal readonly string Label;
        internal readonly bool Major;
        internal Tick(float fraction, string label, bool major) { Fraction = fraction; Label = label; Major = major; }
    }

    internal static IEnumerable<Tick> Ticks(double degrees)
    {
        const double span = 150;
        double left = Normalize(degrees) - span / 2;
        for (int mark = (int)Math.Ceiling(left / 15) * 15; mark <= left + span; mark += 15)
        {
            bool major = mark % 45 == 0;
            double normalized = Normalize(mark);
            yield return new Tick((float)((mark - left) / span),
                major ? Direction(normalized) : normalized.ToString("0", CultureInfo.InvariantCulture), major);
        }
    }
}
