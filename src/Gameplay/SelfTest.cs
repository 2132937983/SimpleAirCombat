using AirCombat.Assets;
using Raylib_cs;

namespace AirCombat.GamePlay;

/// <summary>无界面自检：验证程序化 OBJ 生成与 raylib 模型加载，便于 CI/命令行校验。</summary>
public static class SelfTest
{
    public static bool Run()
    {
        var mesh = AircraftModelFactory.BuildFighter();
        mesh.GetBounds(out var min, out var max);
        Console.WriteLine($"[selftest] 网格: {mesh.VertexCount} 顶点 / {mesh.TriangleCount} 三角形");
        Console.WriteLine($"[selftest] 包围盒: X[{min.X:0.00}, {max.X:0.00}] Y[{min.Y:0.00}, {max.Y:0.00}] Z[{min.Z:0.00}, {max.Z:0.00}]");

        Raylib.SetConfigFlags(ConfigFlags.HiddenWindow);
        Raylib.InitWindow(320, 240, "AirCombat selftest");

        try
        {
            string path = AircraftModelFactory.EnsureModelFile();
            var file = new FileInfo(path);
            var model = Raylib.LoadModel(path);

            bool valid = Raylib.IsModelValid(model) && model.MeshCount > 0;
            bool sized = mesh.TriangleCount > 100 && max.Z - min.Z > 4f && max.X - min.X > 4f;

            Console.WriteLine($"[selftest] OBJ 文件: {path} ({file.Length} 字节)");
            Console.WriteLine($"[selftest] LoadModel: valid={valid} meshes={model.MeshCount}");
            Console.WriteLine(valid && sized ? "[selftest] OK" : "[selftest] FAILED");

            Raylib.UnloadModel(model);
            return valid && sized;
        }
        finally
        {
            Raylib.CloseWindow();
        }
    }
}
