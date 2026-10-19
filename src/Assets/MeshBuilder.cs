using System.Globalization;
using System.Numerics;
using System.Text;

namespace AirCombat.Assets;

/// <summary>沿 Z 轴排列的管状截面（机身/座舱/尾喷口都用它拼）。</summary>
public readonly record struct TubeRing(float Z, float RadiusX, float RadiusY, float CenterX = 0f, float CenterY = 0f);

/// <summary>
/// 极简三角网格构造器：只收集三角形，导出为标准 OBJ 文本（平面法线 + 占位 UV），
/// 然后交给 raylib 的 LoadModel 解析。这样既不需要外部美术资源，也不需要 unsafe 指针。
/// </summary>
public sealed class MeshBuilder
{
    private readonly List<Vector3> _vertices = new();
    private readonly List<(int A, int B, int C, Vector3 Normal)> _faces = new();

    public int VertexCount => _vertices.Count;
    public int TriangleCount => _faces.Count;

    /// <summary>添加三角形，inside 用于自动纠正绕序（法线朝远离 inside 的方向）。</summary>
    public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
    {
        var normal = Vector3.Cross(b - a, c - a);
        if (normal.LengthSquared() < 1e-10f)
        {
            return;
        }

        if (Vector3.Dot(normal, (a + b + c) / 3f - inside) < 0f)
        {
            (b, c) = (c, b);
            normal = -normal;
        }

        _faces.Add((Push(a), Push(b), Push(c), Vector3.Normalize(normal)));
    }

    public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 inside)
    {
        AddTriangle(a, b, c, inside);
        AddTriangle(a, c, d, inside);
    }

    /// <summary>沿 Y 轴拉伸 XZ 平面上的凸多边形（机翼、平尾）。</summary>
    public void AddSlabY(IReadOnlyList<Vector2> polygonXz, float yBottom, float yTop)
    {
        if (polygonXz.Count < 3)
        {
            return;
        }

        var centroid = Centroid(polygonXz);
        var inside = new Vector3(centroid.X, (yBottom + yTop) * 0.5f, centroid.Y);
        int count = polygonXz.Count;

        for (int i = 0; i < count; i++)
        {
            var p = polygonXz[i];
            var q = polygonXz[(i + 1) % count];
            AddQuad(
                new Vector3(p.X, yTop, p.Y),
                new Vector3(q.X, yTop, q.Y),
                new Vector3(q.X, yBottom, q.Y),
                new Vector3(p.X, yBottom, p.Y),
                inside);
        }

        var top = new Vector3(centroid.X, yTop, centroid.Y);
        var bottom = new Vector3(centroid.X, yBottom, centroid.Y);
        for (int i = 0; i < count; i++)
        {
            var p = polygonXz[i];
            var q = polygonXz[(i + 1) % count];
            AddTriangle(top, new Vector3(p.X, yTop, p.Y), new Vector3(q.X, yTop, q.Y), inside);
            AddTriangle(bottom, new Vector3(q.X, yBottom, q.Y), new Vector3(p.X, yBottom, p.Y), inside);
        }
    }

    /// <summary>沿 X 轴拉伸 ZY 平面上的凸多边形（垂尾）。</summary>
    public void AddSlabX(IReadOnlyList<Vector2> polygonZy, float xMin, float xMax)
    {
        if (polygonZy.Count < 3)
        {
            return;
        }

        var centroid = Centroid(polygonZy);
        var inside = new Vector3((xMin + xMax) * 0.5f, centroid.Y, centroid.X);
        int count = polygonZy.Count;

        for (int i = 0; i < count; i++)
        {
            var p = polygonZy[i];
            var q = polygonZy[(i + 1) % count];
            AddQuad(
                new Vector3(xMin, p.Y, p.X),
                new Vector3(xMin, q.Y, q.X),
                new Vector3(xMax, q.Y, q.X),
                new Vector3(xMax, p.Y, p.X),
                inside);
        }

        var left = new Vector3(xMin, centroid.Y, centroid.X);
        var right = new Vector3(xMax, centroid.Y, centroid.X);
        for (int i = 0; i < count; i++)
        {
            var p = polygonZy[i];
            var q = polygonZy[(i + 1) % count];
            AddTriangle(left, new Vector3(xMin, p.Y, p.X), new Vector3(xMin, q.Y, q.X), inside);
            AddTriangle(right, new Vector3(xMax, q.Y, q.X), new Vector3(xMax, p.Y, p.X), inside);
        }
    }

    /// <summary>生成沿 Z 轴的封闭管体（机身、座舱盖、发动机舱）。</summary>
    public void AddTubeZ(IReadOnlyList<TubeRing> rings, int sides = 8)
    {
        if (rings.Count < 2 || sides < 3)
        {
            return;
        }

        var ringPoints = new Vector3[rings.Count][];
        for (int r = 0; r < rings.Count; r++)
        {
            var ring = rings[r];
            var points = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = MathF.Tau * i / sides;
                points[i] = new Vector3(
                    ring.CenterX + MathF.Cos(angle) * ring.RadiusX,
                    ring.CenterY + MathF.Sin(angle) * ring.RadiusY,
                    ring.Z);
            }

            ringPoints[r] = points;
        }

        for (int r = 0; r < rings.Count - 1; r++)
        {
            var inside = new Vector3(
                (rings[r].CenterX + rings[r + 1].CenterX) * 0.5f,
                (rings[r].CenterY + rings[r + 1].CenterY) * 0.5f,
                (rings[r].Z + rings[r + 1].Z) * 0.5f);

            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                AddQuad(ringPoints[r][i], ringPoints[r][j], ringPoints[r + 1][j], ringPoints[r + 1][i], inside);
            }
        }

        // 两端封口
        var firstCenter = new Vector3(rings[0].CenterX, rings[0].CenterY, rings[0].Z);
        var firstInside = new Vector3(rings[1].CenterX, rings[1].CenterY, rings[1].Z);
        var lastCenter = new Vector3(rings[^1].CenterX, rings[^1].CenterY, rings[^1].Z);
        var lastInside = new Vector3(rings[^2].CenterX, rings[^2].CenterY, rings[^2].Z);

        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            AddTriangle(firstCenter, ringPoints[0][j], ringPoints[0][i], firstInside);
            AddTriangle(lastCenter, ringPoints[^1][i], ringPoints[^1][j], lastInside);
        }
    }

    public void GetBounds(out Vector3 min, out Vector3 max)
    {
        min = new Vector3(float.MaxValue);
        max = new Vector3(float.MinValue);
        foreach (var v in _vertices)
        {
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }
    }

    /// <summary>导出 Wavefront OBJ 文本。</summary>
    public string ToObj(string name)
    {
        var sb = new StringBuilder(96 * 1024);
        sb.Append("# AirCombat procedural mesh - generated at runtime\n");
        sb.Append("o ").AppendLine(name);

        foreach (var v in _vertices)
        {
            sb.Append("v ").Append(Format(v.X)).Append(' ').Append(Format(v.Y)).Append(' ').AppendLine(Format(v.Z));
        }

        for (int i = 0; i < _vertices.Count; i++)
        {
            sb.AppendLine("vt 0 0");
        }

        foreach (var face in _faces)
        {
            sb.Append("vn ").Append(Format(face.Normal.X)).Append(' ')
              .Append(Format(face.Normal.Y)).Append(' ')
              .AppendLine(Format(face.Normal.Z));
        }

        for (int i = 0; i < _faces.Count; i++)
        {
            var face = _faces[i];
            int n = i + 1;
            sb.Append("f ")
              .Append(face.A).Append('/').Append(face.A).Append('/').Append(n).Append(' ')
              .Append(face.B).Append('/').Append(face.B).Append('/').Append(n).Append(' ')
              .Append(face.C).Append('/').Append(face.C).Append('/').Append(n)
              .AppendLine();
        }

        return sb.ToString();
    }

    private int Push(Vector3 vertex)
    {
        _vertices.Add(vertex);
        return _vertices.Count; // OBJ 顶点索引从 1 开始
    }

    private static Vector2 Centroid(IReadOnlyList<Vector2> polygon)
    {
        var sum = Vector2.Zero;
        for (int i = 0; i < polygon.Count; i++)
        {
            sum += polygon[i];
        }

        return sum / polygon.Count;
    }

    private static string Format(float value) => value.ToString("0.#####", CultureInfo.InvariantCulture);
}
