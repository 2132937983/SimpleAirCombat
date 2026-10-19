using System.Text;
using AirCombat.GamePlay;

Console.OutputEncoding = Encoding.UTF8;

if (args.Contains("--selftest"))
{
    return SelfTest.Run() ? 0 : 1;
}

if (args.Contains("--smoke"))
{
    return new Game().RunSmokeTest() ? 0 : 1;
}

PrintBanner();
new Game().Run();
return 0;

static void PrintBanner()
{
    Console.WriteLine("==============================================");
    Console.WriteLine("  AIR COMBAT  -  Raylib-cs  /  .NET 10");
    Console.WriteLine("==============================================");
    Console.WriteLine("  鼠标         转向（俯仰 / 偏航）");
    Console.WriteLine("  W / S        俯仰        A / D   偏航");
    Console.WriteLine("  空格 / 左键  开火        左Shift 加力");
    Console.WriteLine("  P            暂停        F11     全屏");
    Console.WriteLine("  ESC          退出        R       阵亡后重开");
    Console.WriteLine("----------------------------------------------");
    Console.WriteLine("  高度无限：没有地面，只有云层与深空。");
    Console.WriteLine("==============================================");
}
