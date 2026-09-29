namespace HexenSharp;

/// <summary>
/// The game's float maths, the same to the last bit on every machine: this class stands in for System.MathF
/// everywhere in the game's code (being in the game's own namespace, it's the MathF that <c>MathF.Sin</c> finds).
/// <para>
/// Online play keeps every player's copy of the game in step by running the same frames, so the same sums must come
/// out the same everywhere: on Windows and Linux, and in a browser. Adding, multiplying, dividing and square roots
/// are exact the world over (IEEE 754), but sine, arctangent, exp and pow come from each platform's maths library,
/// and those differ in the last bit now and then: enough, over a few thousand frames, to put two copies of the game
/// out of step. So those are worked out here from the exact operations alone, in double precision (well inside a
/// float's accuracy), and rounded to float at the end. The rest pass straight through to System.MathF.
/// </para>
/// </summary>
public static class MathF
{
    public const float PI = System.MathF.PI, Tau = System.MathF.Tau, E = System.MathF.E;

    // ------------------------------------------------------------ exact everywhere: straight through

    public static float Abs(float x) => System.MathF.Abs(x);
    public static float Min(float a, float b) => System.MathF.Min(a, b);
    public static float Max(float a, float b) => System.MathF.Max(a, b);
    public static float Floor(float x) => System.MathF.Floor(x);
    public static float Ceiling(float x) => System.MathF.Ceiling(x);
    public static float Round(float x) => System.MathF.Round(x);
    public static float Round(float x, MidpointRounding mode) => System.MathF.Round(x, mode);
    public static float Round(float x, int digits) => System.MathF.Round(x, digits);
    public static float Truncate(float x) => System.MathF.Truncate(x);
    public static float Sqrt(float x) => System.MathF.Sqrt(x);
    public static int Sign(float x) => System.MathF.Sign(x);
    public static float IEEERemainder(float x, float y) => System.MathF.IEEERemainder(x, y);

    // ------------------------------------------------------------ worked out here, the same everywhere

    public static float Sin(float x) => (float)DetMath.Sin(x);
    public static float Cos(float x) => (float)DetMath.Cos(x);
    public static float Tan(float x) => (float)(DetMath.Sin(x) / DetMath.Cos(x));
    public static float Atan(float x) => (float)DetMath.Atan(x);
    public static float Atan2(float y, float x) => (float)DetMath.Atan2(y, x);
    public static float Asin(float x) => (float)DetMath.Atan2(x, System.Math.Sqrt(System.Math.Max(0, 1 - (double)x * x)));
    public static float Acos(float x) => (float)DetMath.Atan2(System.Math.Sqrt(System.Math.Max(0, 1 - (double)x * x)), x);
    public static float Exp(float x) => (float)DetMath.Exp(x);
    public static float Pow(float x, float y) => (float)DetMath.Pow(x, y);
}

/// <summary>
/// Sine, cosine, arctangent, exp, log and pow in double precision from +, -, *, / alone (so bit for bit the same on
/// every machine), accurate to about 1e-15: far past the float the game keeps. The arctangent is fdlibm's.
/// </summary>
public static class DetMath
{
    const double PiOver2Hi = 1.5707963267341256e+00, PiOver2Lo = 6.0771005065061922e-11; // π/2 in two parts
    const double TwoOverPi = 0.63661977236758134;

    /// <summary>x less a whole number of quarter turns, into [-π/4, π/4], and which quarter it landed in.</summary>
    static double Quarter(double x, out int quadrant)
    {
        double k = System.Math.Floor(x * TwoOverPi + 0.5);
        quadrant = (int)((long)k & 3);
        return (x - k * PiOver2Hi) - k * PiOver2Lo;
    }

    static double SinPoly(double r)
    {
        double z = r * r;
        return r + r * z * (-1.0 / 6 + z * (1.0 / 120 + z * (-1.0 / 5040 + z * (1.0 / 362880 + z * (-1.0 / 39916800 + z * (1.0 / 6227020800))))));
    }

    static double CosPoly(double r)
    {
        double z = r * r;
        return 1 + z * (-0.5 + z * (1.0 / 24 + z * (-1.0 / 720 + z * (1.0 / 40320 + z * (-1.0 / 3628800 + z * (1.0 / 479001600 + z * (-1.0 / 87178291200)))))));
    }

    public static double Sin(double x)
    {
        if (double.IsNaN(x) || double.IsInfinity(x)) return double.NaN;
        double r = Quarter(x, out int q);
        return q switch { 0 => SinPoly(r), 1 => CosPoly(r), 2 => -SinPoly(r), _ => -CosPoly(r) };
    }

    public static double Cos(double x)
    {
        if (double.IsNaN(x) || double.IsInfinity(x)) return double.NaN;
        double r = Quarter(x, out int q);
        return q switch { 0 => CosPoly(r), 1 => -SinPoly(r), 2 => -CosPoly(r), _ => SinPoly(r) };
    }

    static readonly double[] AtanHi = { 4.63647609000806093515e-01, 7.85398163397448278999e-01, 9.82793723247329054082e-01, 1.57079632679489655800e+00 };
    static readonly double[] AtanLo = { 2.26987774529616870924e-17, 3.06161699786838301793e-17, 1.39033110312309984516e-17, 6.12323399573676603587e-17 };
    static readonly double[] AT =
    {
        3.33333333333329318027e-01, -1.99999999998764832476e-01, 1.42857142725034663711e-01, -1.11111104054623557880e-01,
        9.09088713343650656196e-02, -7.69187620504482999495e-02, 6.66107313738753120669e-02, -5.83357013379057348645e-02,
        4.97687799461593236017e-02, -3.65315727442169155270e-02, 1.62858201153657823623e-02,
    };

    public static double Atan(double x)
    {
        if (double.IsNaN(x)) return x;
        bool negative = x < 0;
        double a = System.Math.Abs(x);
        if (a >= 7.3786976294838206e19) return negative ? -(AtanHi[3] + AtanLo[3]) : AtanHi[3] + AtanLo[3]; // (2^66 and up)
        int id;
        if (a < 0.4375)
        {
            if (a < 7.450580596923828e-9) return x; // (2^-27: atan x is x)
            id = -1;
        }
        else if (a < 1.1875)
        {
            if (a < 0.6875) { id = 0; a = (2 * a - 1) / (2 + a); }
            else { id = 1; a = (a - 1) / (a + 1); }
        }
        else if (a < 2.4375) { id = 2; a = (a - 1.5) / (1 + 1.5 * a); }
        else { id = 3; a = -1 / a; }
        double z = a * a, w = z * z;
        double s1 = z * (AT[0] + w * (AT[2] + w * (AT[4] + w * (AT[6] + w * (AT[8] + w * AT[10])))));
        double s2 = w * (AT[1] + w * (AT[3] + w * (AT[5] + w * (AT[7] + w * AT[9]))));
        if (id < 0) return negative ? -(a - a * (s1 + s2)) : a - a * (s1 + s2);
        double r = AtanHi[id] - ((a * (s1 + s2) - AtanLo[id]) - a);
        return negative ? -r : r;
    }

    public static double Atan2(double y, double x)
    {
        if (double.IsNaN(x) || double.IsNaN(y)) return double.NaN;
        if (x == 0) return y > 0 ? System.Math.PI / 2 : y < 0 ? -System.Math.PI / 2 : 0;
        double t = Atan(y / x);
        if (x > 0) return t;
        return y < 0 ? t - System.Math.PI : t + System.Math.PI;
    }

    const double Ln2Hi = 6.93147180369123816490e-01, Ln2Lo = 1.90821492927058770002e-10, InvLn2 = 1.44269504088896338700e+00;

    public static double Exp(double x)
    {
        if (double.IsNaN(x)) return x;
        if (x > 709) return double.PositiveInfinity;
        if (x < -745) return 0;
        double k = System.Math.Floor(x * InvLn2 + 0.5);
        double r = (x - k * Ln2Hi) - k * Ln2Lo; // |r| ≤ ln2/2
        // e^r by its series, to the 14th power (past double precision for |r| ≤ 0.35)
        double sum = 1, term = 1;
        for (int n = 1; n <= 14; n++) { term *= r / n; sum += term; }
        return System.Math.ScaleB(sum, (int)k);
    }

    public static double Log(double x)
    {
        if (double.IsNaN(x) || x < 0) return double.NaN;
        if (x == 0) return double.NegativeInfinity;
        if (double.IsPositiveInfinity(x)) return x;
        // x = m · 2^e with m in [√½, √2)
        int e = System.Math.ILogB(x);
        double m = System.Math.ScaleB(x, -e);
        if (m > 1.4142135623730951) { m /= 2; e++; }
        // log m = 2 atanh s, s = (m - 1)/(m + 1), by its series
        double s = (m - 1) / (m + 1), s2 = s * s, term = s, sum = 0;
        for (int n = 1; n <= 41; n += 2) { sum += term / n; term *= s2; }
        return 2 * sum + e * Ln2Hi + e * Ln2Lo;
    }

    public static double Pow(double x, double y)
    {
        if (y == 0) return 1;
        if (x == 0) return y > 0 ? 0 : double.PositiveInfinity;
        if (x < 0)
        {
            // a negative base: only whole powers are real
            if (System.Math.Floor(y) != y) return double.NaN;
            double p = Exp(y * Log(-x));
            return System.Math.Abs(y % 2) == 1 ? -p : p;
        }
        return Exp(y * Log(x));
    }
}
