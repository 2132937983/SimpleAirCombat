using System.Numerics;
using System.Text;

namespace AirCombat.Assets;

/// <summary>
/// 用代码"雕刻"一架低多边形战斗机：机身、三角翼、平尾、垂尾、座舱盖、双发尾喷口。
/// 生成结果写成真实的 .obj 文件（可直接用 Blender 打开），再交给 raylib 加载。
/// 坐标约定：机头朝 +Z，上方 +Y，右侧 +X，长度为 1 个世界单位量级。
/// </summary>
public static class AircraftModelFactory
{
    public const string ModelFileName = "fighter.obj";

    public static MeshBuilder BuildFighter()
    {
        var builder = new MeshBuilder();
        BuildFuselage(builder);
        BuildWings(builder);
        BuildTail(builder);
        BuildCanopy(builder);
        BuildEngines(builder);
        return builder;
    }

    /// <summary>生成并写出 OBJ 文件，返回路径。</summary>
    public static string EnsureModelFile()
    {
        var mesh = BuildFighter();
        string content = mesh.ToObj("AirCombat_Fighter");

        try
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "Assets");
            Directory.CreateDirectory(dir);
            return WriteIfChanged(Path.Combine(dir, ModelFileName), content);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 安装目录只读时退回临时目录
            string dir = Path.Combine(Path.GetTempPath(), "AirCombat", "Assets");
            Directory.CreateDirectory(dir);
            return WriteIfChanged(Path.Combine(dir, ModelFileName), content);
        }
    }

    private static string WriteIfChanged(string path, string content)
    {
        if (!File.Exists(path) || File.ReadAllText(path) != content)
        {
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }

        return path;
    }

    private static void BuildFuselage(MeshBuilder builder)
    {
        var rings = new List<TubeRing>
        {
            new(4.30f, 0.03f, 0.03f),
            new(3.60f, 0.28f, 0.25f),
            new(2.70f, 0.45f, 0.40f),
            new(1.20f, 0.58f, 0.52f),
            new(-0.40f, 0.62f, 0.55f),
            new(-1.90f, 0.55f, 0.48f),
            new(-3.20f, 0.46f, 0.40f),
            new(-3.90f, 0.40f, 0.35f),
        };
        builder.AddTubeZ(rings, 8);
    }

    private static void BuildWings(MeshBuilder builder)
    {
        // 右翼后掠三角翼（XZ 平面），再镜像出左翼
        var wing = new List<Vector2>
        {
            new(0.45f, 0.95f),
            new(4.85f, -2.15f),
            new(4.55f, -2.80f),
            new(0.45f, -2.70f),
        };
        builder.AddSlabY(wing, -0.09f, 0.09f);
        builder.AddSlabY(wing.Select(p => new Vector2(-p.X, p.Y)).ToList(), -0.09f, 0.09f);
    }

    private static void BuildTail(MeshBuilder builder)
    {
        // 平尾
        var stabilizer = new List<Vector2>
        {
            new(0.35f, -2.60f),
            new(2.15f, -3.25f),
            new(2.00f, -3.70f),
            new(0.35f, -3.60f),
        };
        builder.AddSlabY(stabilizer, -0.06f, 0.06f);
        builder.AddSlabY(stabilizer.Select(p => new Vector2(-p.X, p.Y)).ToList(), -0.06f, 0.06f);

        // 垂尾（ZY 平面，沿 X 拉伸）
        var fin = new List<Vector2>
        {
            new(-1.75f, 0.25f),
            new(-1.75f, 1.95f),
            new(-2.95f, 2.05f),
            new(-3.75f, 0.40f),
        };
        builder.AddSlabX(fin, -0.07f, 0.07f);
    }

    private static void BuildCanopy(MeshBuilder builder)
    {
        var rings = new List<TubeRing>
        {
            new(2.05f, 0.06f, 0.05f, 0f, 0.35f),
            new(1.45f, 0.26f, 0.22f, 0f, 0.52f),
            new(0.35f, 0.30f, 0.26f, 0f, 0.56f),
            new(-0.65f, 0.24f, 0.20f, 0f, 0.52f),
            new(-1.25f, 0.06f, 0.05f, 0f, 0.45f),
        };
        builder.AddTubeZ(rings, 6);
    }

    private static void BuildEngines(MeshBuilder builder)
    {
        foreach (float side in new[] { -1f, 1f })
        {
            float x = 0.52f * side;
            var rings = new List<TubeRing>
            {
                new(-3.10f, 0.30f, 0.30f, x, -0.02f),
                new(-4.35f, 0.25f, 0.25f, x, -0.02f),
                new(-4.55f, 0.29f, 0.29f, x, -0.02f),
            };
            builder.AddTubeZ(rings, 8);
        }
    }
}
