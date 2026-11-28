// The handful of UnityEngine types the Motor, wire parser and arena data use, with Unity's float semantics.
using System;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float sqrMagnitude => x * x + y * y;
        public float magnitude => (float)Math.Sqrt(x * x + y * y);
        public Vector2 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : new Vector2(0, 0); } }
        public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;
        public static Vector2 MoveTowards(Vector2 current, Vector2 target, float maxDistanceDelta)
        {
            float dx = target.x - current.x, dy = target.y - current.y, sq = dx * dx + dy * dy;
            if (sq == 0 || maxDistanceDelta >= 0 && sq <= maxDistanceDelta * maxDistanceDelta) return target;
            float distance = (float)Math.Sqrt(sq);
            return new Vector2(current.x + dx / distance * maxDistanceDelta, current.y + dy / distance * maxDistanceDelta);
        }
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 a, float d) => new Vector2(a.x * d, a.y * d);
        public static Vector2 operator /(Vector2 a, float d) => new Vector2(a.x / d, a.y / d);
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 ClampMagnitude(Vector3 v, float max) { float m = v.magnitude; return m > max ? v * (max / m) : v; }
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + (b - a) * t;
    }

    public struct Color { public float r, g, b, a; public static Color white => new Color { r = 1, g = 1, b = 1, a = 1 }; }

    public static class ColorUtility
    {
        public static bool TryParseHtmlString(string text, out Color color) { color = default; return false; }
    }

    public static class Mathf
    {
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Max(params float[] values) { float m = values[0]; foreach (var v in values) if (v > m) m = v; return m; }
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Abs(float f) => Math.Abs(f);
        public static float Sin(float f) => (float)Math.Sin(f);
        public static float Cos(float f) => (float)Math.Cos(f);
        public static float Sqrt(float f) => (float)Math.Sqrt(f);
        public static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
        public static int FloorToInt(float f) => (int)Math.Floor(f);
        public static float Exp(float f) => (float)Math.Exp(f);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp(t, 0, 1);
    }
}
