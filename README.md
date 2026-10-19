# Air Combat

3D 空战游戏，基于 [Raylib-cs](https://github.com/ChrisDill/Raylib-cs) 与 .NET 10 构建。  
所有 3D 模型均由代码程序化生成（OBJ 格式），无需外部美术资源。

## 功能特性

- **程序化战机模型** — 机身、三角翼、平尾/垂尾、座舱盖、双发尾喷口全部由 `MeshBuilder` 实时生成
- **玩家操控** — 鼠标转向 + 键盘俯仰/偏航，空格或左键开火，Shift 加力
- **敌机 AI** — 巡航站位 + 周期性攻击咬尾，距离越远命中率越低
- **弹道与命中** — 扫掠球体检测（`SegmentHitsSphere`），高速弹丸不会穿透
- **粒子系统** — 爆炸碎屑、火花、烟尘、机炮闪光
- **HUD** — 准星、血量条、速度表、罗盘、敌机标记、命中提示
- **云层** — 无限天空，无地面，只有云与深空
- **自检 / 冒烟测试** — `--selftest` 与 `--smoke` 命令行参数，可用于 CI

## 操作说明

| 按键 | 功能 |
|------|------|
| 鼠标 | 转向（俯仰 / 偏航） |
| W / S | 俯仰 |
| A / D | 偏航 |
| 空格 / 左键 | 开火 |
| 左Shift | 加力 |
| P | 暂停 |
| F11 | 全屏 |
| ESC | 退出 |
| R | 阵亡后重开 |

## 构建与运行

### 前置条件

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- 支持 OpenGL 3.3+ 的显卡

### 运行

```bash
dotnet run
```

### 自检模式

```bash
dotnet run -- --selftest   # 验证程序化 OBJ 生成与模型加载
dotnet run -- --smoke      # 无窗口冒烟测试（截图保存为 smoke.png）
```

## 项目结构

```
AirCombat.csproj           # 项目文件（Raylib-cs 8.1.0 / net10.0）
src/
  Program.cs               # 入口：启动参数解析与 Banner
  Assets/
    AircraftModelFactory.cs # 程序化战斗机 OBJ 生成
    MeshBuilder.cs          # 极简三角网格构造器 → OBJ 文本导出
  Gameplay/
    Game.cs                 # 主循环：窗口、相机、敌机生成、碰撞、绘制
    PlayerAircraft.cs       # 玩家战机：操控、血量、加力
    EnemyAircraft.cs        # 敌机 AI：巡航站位 + 攻击咬尾
    Projectile.cs           # 弹丸结构体
    ParticleSystem.cs       # 粒子系统：爆炸 / 烟雾 / 闪光
    Hud.cs                  # 2D 界面层：准星 / 血量 / 分数 / 罗盘
    MathUtil.cs             # 数学工具：扫掠检测、旋转基矩阵、航向角
    SelfTest.cs             # 无界面自检
```

## 技术要点

- **程序化建模**：`AircraftModelFactory` 用管状截面（`TubeRing`）+ 凸多边形拉伸（`AddSlabY`）拼出完整战机，导出为标准 OBJ 文件，再由 raylib 的 `LoadModel` 加载
- **扫掠碰撞**：子弹速度极快，使用线段-球体相交检测而非逐帧点检测，避免穿透
- **敌机 AI**：平时保持在玩家前方站位蛇形巡航，周期性进入攻击状态掉头咬人，冲太近自动脱离
- **字体回退**：依次尝试 Segoe UI → Arial → Helvetica → DejaVu Sans，均不可用时使用 raylib 内置字体

## 许可证

本项目仅供学习与个人娱乐使用。