// ---------------------------------------------------------------------------------------------
// WARFRONT verification harness - UnityEngine shim.
//
// This shim is NOT shipped in the game. It exists only so the pure-logic Warfront.Core assembly
// (and the EditMode tests that target it) can be compiled and executed by the .NET SDK on a machine
// without the Unity editor. It implements exactly the slice of the Unity API that Core uses.
// ---------------------------------------------------------------------------------------------
using System;

namespace UnityEngine
{
    public static class Mathf
    {
        public const float PI = 3.14159265358979f;
        public const float Infinity = float.PositiveInfinity;
        public const float NegativeInfinity = float.NegativeInfinity;
        public const float Epsilon = 1.401298E-45f;
        public const float Deg2Rad = 0.0174532924f;
        public const float Rad2Deg = 57.29578f;

        public static float Abs(float v) { return Math.Abs(v); }
        public static int Abs(int v) { return Math.Abs(v); }
        public static float Min(float a, float b) { return a < b ? a : b; }
        public static int Min(int a, int b) { return a < b ? a : b; }
        public static float Max(float a, float b) { return a > b ? a : b; }
        public static int Max(int a, int b) { return a > b ? a : b; }
        public static float Clamp(float v, float min, float max) { return v < min ? min : (v > max ? max : v); }
        public static int Clamp(int v, int min, int max) { return v < min ? min : (v > max ? max : v); }
        public static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * Clamp01(t); }
        public static float LerpUnclamped(float a, float b, float t) { return a + (b - a) * t; }
        public static float InverseLerp(float a, float b, float v) { return a == b ? 0f : Clamp01((v - a) / (b - a)); }
        public static float Sqrt(float v) { return (float)Math.Sqrt(v); }
        public static float Exp(float v) { return (float)Math.Exp(v); }
        public static float Pow(float a, float b) { return (float)Math.Pow(a, b); }
        public static float Log(float v) { return (float)Math.Log(v); }
        public static float Sign(float v) { return v >= 0f ? 1f : -1f; }
        public static float Sin(float v) { return (float)Math.Sin(v); }
        public static float Cos(float v) { return (float)Math.Cos(v); }
        public static float Tan(float v) { return (float)Math.Tan(v); }
        public static float Atan2(float y, float x) { return (float)Math.Atan2(y, x); }
        public static float Acos(float v) { return (float)Math.Acos(v); }
        public static float Floor(float v) { return (float)Math.Floor(v); }
        public static float Ceil(float v) { return (float)Math.Ceiling(v); }
        public static float Round(float v) { return (float)Math.Round(v); }
        public static int FloorToInt(float v) { return (int)Math.Floor(v); }
        public static int CeilToInt(float v) { return (int)Math.Ceiling(v); }
        public static int RoundToInt(float v) { return (int)Math.Round(v, MidpointRounding.AwayFromZero); }
        public static bool Approximately(float a, float b) { return Math.Abs(b - a) < Math.Max(1E-06f * Math.Max(Math.Abs(a), Math.Abs(b)), Epsilon * 8f); }
        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta) return target;
            return current + Sign(target - current) * maxDelta;
        }
        public static float Repeat(float t, float length) { return Clamp(t - Floor(t / length) * length, 0f, length); }
        public static float PingPong(float t, float length)
        {
            t = Repeat(t, length * 2f);
            return length - Math.Abs(t - length);
        }
        public static float SmoothStep(float from, float to, float t)
        {
            t = Clamp01(t);
            t = -2f * t * t * t + 3f * t * t;
            return to * t + from * (1f - t);
        }
        public static float SmoothDamp(float current, float target, ref float velocity, float smoothTime, float deltaTime)
        {
            float maxSpeed = float.PositiveInfinity;
            return SmoothDamp(current, target, ref velocity, smoothTime, maxSpeed, deltaTime);
        }
        public static float SmoothDamp(float current, float target, ref float velocity, float smoothTime, float maxSpeed, float deltaTime)
        {
            smoothTime = Math.Max(0.0001f, smoothTime);
            float omega = 2f / smoothTime;
            float x = omega * deltaTime;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float change = current - target;
            float originalTo = target;
            float maxChange = maxSpeed * smoothTime;
            change = Clamp(change, -maxChange, maxChange);
            target = current - change;
            float temp = (velocity + omega * change) * deltaTime;
            velocity = (velocity - omega * temp) * exp;
            float result = target + (change + temp) * exp;
            if (originalTo - current > 0f == result > originalTo)
            {
                result = originalTo;
                velocity = (result - originalTo) / deltaTime;
            }
            return result;
        }
    }

    public struct Vector2
    {
        public float x;
        public float y;

        public Vector2(float x, float y) { this.x = x; this.y = y; }

        public static Vector2 zero { get { return new Vector2(0f, 0f); } }
        public static Vector2 one { get { return new Vector2(1f, 1f); } }
        public static Vector2 up { get { return new Vector2(0f, 1f); } }
        public static Vector2 right { get { return new Vector2(1f, 0f); } }

        public float magnitude { get { return Mathf.Sqrt(x * x + y * y); } }
        public float sqrMagnitude { get { return x * x + y * y; } }
        public Vector2 normalized
        {
            get
            {
                float m = magnitude;
                return m > 1E-06f ? new Vector2(x / m, y / m) : zero;
            }
        }

        public static float Distance(Vector2 a, Vector2 b) { return (a - b).magnitude; }
        public static float Dot(Vector2 a, Vector2 b) { return a.x * b.x + a.y * b.y; }
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
        }

        public static Vector2 operator +(Vector2 a, Vector2 b) { return new Vector2(a.x + b.x, a.y + b.y); }
        public static Vector2 operator -(Vector2 a, Vector2 b) { return new Vector2(a.x - b.x, a.y - b.y); }
        public static Vector2 operator -(Vector2 a) { return new Vector2(-a.x, -a.y); }
        public static Vector2 operator *(Vector2 a, float d) { return new Vector2(a.x * d, a.y * d); }
        public static Vector2 operator *(float d, Vector2 a) { return new Vector2(a.x * d, a.y * d); }
        public static Vector2 operator /(Vector2 a, float d) { return new Vector2(a.x / d, a.y / d); }
        public static bool operator ==(Vector2 a, Vector2 b) { return (a - b).sqrMagnitude < 9.99999944E-11f; }
        public static bool operator !=(Vector2 a, Vector2 b) { return !(a == b); }

        public override bool Equals(object other) { return other is Vector2 && this == (Vector2)other; }
        public override int GetHashCode() { return x.GetHashCode() ^ (y.GetHashCode() << 2); }
        public override string ToString() { return "(" + x.ToString("F2") + ", " + y.ToString("F2") + ")"; }
    }

    public struct Vector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) { this.x = x; this.y = y; this.z = 0f; }

        public static Vector3 zero { get { return new Vector3(0f, 0f, 0f); } }
        public static Vector3 one { get { return new Vector3(1f, 1f, 1f); } }
        public static Vector3 up { get { return new Vector3(0f, 1f, 0f); } }
        public static Vector3 down { get { return new Vector3(0f, -1f, 0f); } }
        public static Vector3 left { get { return new Vector3(-1f, 0f, 0f); } }
        public static Vector3 right { get { return new Vector3(1f, 0f, 0f); } }
        public static Vector3 forward { get { return new Vector3(0f, 0f, 1f); } }
        public static Vector3 back { get { return new Vector3(0f, 0f, -1f); } }

        public float this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return x;
                    case 1: return y;
                    default: return z;
                }
            }
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    default: z = value; break;
                }
            }
        }

        public float magnitude { get { return Mathf.Sqrt(x * x + y * y + z * z); } }
        public float sqrMagnitude { get { return x * x + y * y + z * z; } }

        public Vector3 normalized
        {
            get
            {
                float m = magnitude;
                return m > 1E-05f ? new Vector3(x / m, y / m, z / m) : zero;
            }
        }

        public void Normalize()
        {
            float m = magnitude;
            if (m > 1E-05f)
            {
                x /= m;
                y /= m;
                z /= m;
            }
        }

        public void Set(float newX, float newY, float newZ) { x = newX; y = newY; z = newZ; }

        public static float Distance(Vector3 a, Vector3 b) { return (a - b).magnitude; }
        public static float Dot(Vector3 a, Vector3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
        public static Vector3 Cross(Vector3 a, Vector3 b)
        {
            return new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        }

        public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        }

        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t)
        {
            return new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        }

        public static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDistanceDelta)
        {
            Vector3 delta = target - current;
            float dist = delta.magnitude;
            if (dist <= maxDistanceDelta || dist < 1E-06f) return target;
            return current + delta / dist * maxDistanceDelta;
        }

        public static Vector3 ClampMagnitude(Vector3 vector, float maxLength)
        {
            if (vector.sqrMagnitude > maxLength * maxLength) return vector.normalized * maxLength;
            return vector;
        }

        public static Vector3 Scale(Vector3 a, Vector3 b) { return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z); }

        public static Vector3 Project(Vector3 vector, Vector3 onNormal)
        {
            float sqr = onNormal.sqrMagnitude;
            if (sqr < 1E-06f) return zero;
            return onNormal * (Dot(vector, onNormal) / sqr);
        }

        public static float Angle(Vector3 from, Vector3 to)
        {
            float denominator = Mathf.Sqrt(from.sqrMagnitude * to.sqrMagnitude);
            if (denominator < 1E-15f) return 0f;
            float dot = Mathf.Clamp(Dot(from, to) / denominator, -1f, 1f);
            return Mathf.Acos(dot) * Mathf.Rad2Deg;
        }

        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vector3 operator -(Vector3 a) { return new Vector3(-a.x, -a.y, -a.z); }
        public static Vector3 operator *(Vector3 a, float d) { return new Vector3(a.x * d, a.y * d, a.z * d); }
        public static Vector3 operator *(float d, Vector3 a) { return new Vector3(a.x * d, a.y * d, a.z * d); }
        public static Vector3 operator /(Vector3 a, float d) { return new Vector3(a.x / d, a.y / d, a.z / d); }
        public static bool operator ==(Vector3 a, Vector3 b) { return (a - b).sqrMagnitude < 9.99999944E-11f; }
        public static bool operator !=(Vector3 a, Vector3 b) { return !(a == b); }

        public override bool Equals(object other) { return other is Vector3 && this == (Vector3)other; }
        public override int GetHashCode() { return x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2); }
        public override string ToString() { return "(" + x.ToString("F2") + ", " + y.ToString("F2") + ", " + z.ToString("F2") + ")"; }
    }

    public struct Color
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; this.a = 1f; }

        public static Color white { get { return new Color(1f, 1f, 1f, 1f); } }
        public static Color black { get { return new Color(0f, 0f, 0f, 1f); } }
        public static Color clear { get { return new Color(0f, 0f, 0f, 0f); } }
        public static Color red { get { return new Color(1f, 0f, 0f, 1f); } }
        public static Color green { get { return new Color(0f, 1f, 0f, 1f); } }
        public static Color blue { get { return new Color(0f, 0f, 1f, 1f); } }
        public static Color yellow { get { return new Color(1f, 0.92f, 0.016f, 1f); } }
        public static Color cyan { get { return new Color(0f, 1f, 1f, 1f); } }
        public static Color magenta { get { return new Color(1f, 0f, 1f, 1f); } }
        public static Color gray { get { return new Color(0.5f, 0.5f, 0.5f, 1f); } }
        public static Color grey { get { return gray; } }

        public static Color Lerp(Color a, Color b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
        }

        public static Color operator *(Color a, float d) { return new Color(a.r * d, a.g * d, a.b * d, a.a * d); }
        public static Color operator *(Color a, Color b) { return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a); }
        public static Color operator +(Color a, Color b) { return new Color(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a); }
        public static bool operator ==(Color a, Color b)
        {
            return Math.Abs(a.r - b.r) < 1E-05f && Math.Abs(a.g - b.g) < 1E-05f && Math.Abs(a.b - b.b) < 1E-05f && Math.Abs(a.a - b.a) < 1E-05f;
        }
        public static bool operator !=(Color a, Color b) { return !(a == b); }

        public override bool Equals(object other) { return other is Color && this == (Color)other; }
        public override int GetHashCode() { return r.GetHashCode() ^ g.GetHashCode() ^ b.GetHashCode() ^ a.GetHashCode(); }
        public override string ToString() { return "RGBA(" + r.ToString("F3") + ", " + g.ToString("F3") + ", " + b.ToString("F3") + ", " + a.ToString("F3") + ")"; }
    }
}

namespace UnityEngine
{
    public static class Debug
    {
        public static bool Silent { get; set; }

        public static void Log(object message) { if (!Silent) Console.WriteLine("[Log] " + message); }
        public static void LogWarning(object message) { if (!Silent) Console.WriteLine("[Warn] " + message); }
        public static void LogError(object message) { if (!Silent) Console.WriteLine("[Error] " + message); }
        public static void LogException(Exception exception) { if (!Silent) Console.WriteLine("[Exception] " + exception); }
        public static void LogFormat(string format, params object[] args) { if (!Silent) Console.WriteLine("[Log] " + string.Format(format, args)); }
        public static void LogWarningFormat(string format, params object[] args) { if (!Silent) Console.WriteLine("[Warn] " + string.Format(format, args)); }
        public static void LogErrorFormat(string format, params object[] args) { if (!Silent) Console.WriteLine("[Error] " + string.Format(format, args)); }
    }

    public static class Application
    {
        public static string dataPath { get { return Environment.GetEnvironmentVariable("WARFRONT_SHIM_DATA_PATH") ?? string.Empty; } }
        public static string persistentDataPath { get { return System.IO.Path.Combine(System.IO.Path.GetTempPath(), "warfront_shim"); } }
        public static bool isPlaying { get { return true; } }
        public static string version { get { return "0.0.0-shim"; } }
        public static void Quit() { }
    }

    public static class Time
    {
        private static readonly DateTime Start = DateTime.UtcNow;
        public static float time { get { return (float)(DateTime.UtcNow - Start).TotalSeconds; } }
        public static float deltaTime { get { return 1f / 60f; } }
        public static float unscaledDeltaTime { get { return 1f / 60f; } }
    }
}
