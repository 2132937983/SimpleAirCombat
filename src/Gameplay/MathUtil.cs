using System.Numerics;
using Raylib_cs;

namespace AirCombat.GamePlay;

public static class MathUtil
{
    /// <summary>线段与球体相交检测（子弹速度快，必须用扫掠而不是点检测）。</summary>
    public static bool SegmentHitsSphere(Vector3 from, Vector3 to, Vector3 center, float radius)
    {
        var segment = to - from;
        float lengthSquared = segment.LengthSquared();
        float t = lengthSquared < 1e-6f
            ? 0f
            : Math.Clamp(Vector3.Dot(center - from, segment) / lengthSquared, 0f, 1f);

        var closest = from + segment * t;
        return Vector3.DistanceSquared(closest, center) <= radius * radius;
    }

    public static Color Lerp(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t),
            (int)(a.A + (b.A - a.A) * t));
    }

    /// <summary>用前向 + 上方向构造旋转基矩阵（System.Numerics 使用行向量约定）。</summary>
    public static Matrix4x4 Basis(Vector3 forward, Vector3 up)
    {
        if (Vector3.Cross(up, forward).LengthSquared() < 1e-6f)
        {
            up = MathF.Abs(forward.Y) > 0.99f ? Vector3.UnitZ : Vector3.UnitY;
        }

        var right = Vector3.Normalize(Vector3.Cross(up, forward));
        up = Vector3.Normalize(Vector3.Cross(forward, right));

        return new Matrix4x4(
            right.X, right.Y, right.Z, 0f,
            up.X, up.Y, up.Z, 0f,
            forward.X, forward.Y, forward.Z, 0f,
            0f, 0f, 0f, 1f);
    }

    /// <summary>方向向量 → 罗盘航向角（度）：-Z 为 0（N），+X 为 90（E）。</summary>
    public static float HeadingOf(Vector3 direction)
    {
        float degrees = MathF.Atan2(direction.X, -direction.Z) * (180f / MathF.PI);
        return (degrees % 360f + 360f) % 360f;
    }

    /// <summary>把角度差折算到 [-180, 180]。</summary>
    public static float ShortestAngle(float degrees)
    {
        degrees %= 360f;
        if (degrees > 180f)
        {
            degrees -= 360f;
        }
        else if (degrees < -180f)
        {
            degrees += 360f;
        }

        return degrees;
    }

    public static float Damp(float current, float target, float speed, float dt)
        => current + (target - current) * (1f - MathF.Exp(-speed * dt));

    public static Vector3 Damp(Vector3 current, Vector3 target, float speed, float dt)
        => Vector3.Lerp(current, target, 1f - MathF.Exp(-speed * dt));
}
