using System.Numerics;
using AirCombat.Assets;
using Raylib_cs;

namespace AirCombat.GamePlay;

public enum GameState
{
    Intro,
    Playing,
    Paused,
    Dead,
}

/// <summary>空战主循环：无限天空、追尾镜头、敌机生成、射击与命中判定。</summary>
public sealed class Game
{
    public const int ScreenWidth = 1280;
    public const int ScreenHeight = 720;

    private static readonly Color SkyTop = new(16, 40, 84);
    private static readonly Color SkyBottom = new(176, 208, 236);
    private static readonly Color PlayerTint = new(206, 224, 242);
    private static readonly Color EnemyTint = new(122, 44, 48);

    private readonly Random _rng = new();
    private readonly PlayerAircraft _player = new();
    private readonly List<EnemyAircraft> _enemies = new();
    private readonly List<Projectile> _bullets = new();
    private readonly ParticleSystem _particles = new();
    private List<Cloud> _clouds = new();

    private Camera3D _camera;
    private Model _fighter;
    private Model _sphere;
    private Font _uiFont;
    private bool _ownsFont;

    private GameState _state = GameState.Intro;
    private float _elapsed;
    private float _spawnTimer = 1.5f;
    private int _kills;
    private float _hitFlash;
    private float _shake;
    private float _hitMarker;
    private float _muzzleGlow;
    private bool _gunSide;
    private bool _contentLoaded;

    public PlayerAircraft Player => _player;
    public IReadOnlyList<EnemyAircraft> Enemies => _enemies;
    public GameState State => _state;
    public Font UiFont => _uiFont;
    public int Kills => _kills;
    public int Score => _kills * 100;
    public float Elapsed => _elapsed;
    public float HitMarker => _hitMarker;
    public float DamageFlash => _hitFlash;
    public float MuzzleGlow => _muzzleGlow;
    public Random Random => _rng;

    public void Run()
    {
        SetupWindow(hidden: false);

        LoadContent();
        InitCamera();
        CreateClouds();

        while (!Raylib.WindowShouldClose())
        {
            float dt = MathF.Min(Raylib.GetFrameTime(), 1f / 20f);
            Update(dt);
            Draw();
        }

        UnloadContent();
        Raylib.CloseWindow();
    }

    /// <summary>隐藏窗口跑若干帧，用于命令行验证渲染与逻辑不崩溃。</summary>
    public bool RunSmokeTest(int frames = 420)
    {
        try
        {
            SetupWindow(hidden: true);
            LoadContent();
            InitCamera();
            CreateClouds();
            StartRun();

            for (int frame = 0; frame < frames; frame++)
            {
                if (frame % 8 == 0)
                {
                    SpawnPlayerBullet(_player.Position + _player.Forward * 3f, _player.Forward, _player.Right);
                }

                Update(1f / 60f);
                Draw();
            }

            Draw();
            Raylib.TakeScreenshot("smoke.png");

            Console.WriteLine(
                $"[smoke] OK frames={frames} state={_state} enemies={_enemies.Count} bullets={_bullets.Count} particles={_particles.Count} health={_player.Health:0}");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[smoke] FAILED: {ex}");
            return false;
        }
        finally
        {
            UnloadContent();
            Raylib.CloseWindow();
        }
    }

    private static void SetupWindow(bool hidden)
    {
        var flags = ConfigFlags.Msaa4xHint | ConfigFlags.VSyncHint | ConfigFlags.ResizableWindow;
        if (hidden)
        {
            flags |= ConfigFlags.HiddenWindow;
        }

        Raylib.SetConfigFlags(flags);
        Raylib.InitWindow(ScreenWidth, ScreenHeight, "Air Combat - Raylib-cs / .NET 10");
        Raylib.SetTargetFPS(60);
        Raylib.DisableCursor();
    }

    // ---------------------------------------------------------------- 资源

    private void LoadContent()
    {
        string modelPath = AircraftModelFactory.EnsureModelFile();
        _fighter = Raylib.LoadModel(modelPath);
        if (!Raylib.IsModelValid(_fighter) || _fighter.MeshCount <= 0)
        {
            throw new InvalidOperationException($"无法加载战机模型: {modelPath}");
        }

        _sphere = Raylib.LoadModelFromMesh(Raylib.GenMeshSphere(1f, 8, 8));
        _uiFont = LoadUiFont(64, out _ownsFont);

        Console.WriteLine($"[AirCombat] 程序化 3D 模型已生成: {modelPath}");
        Console.WriteLine($"[AirCombat] 模型网格数: {_fighter.MeshCount}");
        _contentLoaded = true;
    }

    private void UnloadContent()
    {
        if (_contentLoaded)
        {
            Raylib.UnloadModel(_fighter);
            Raylib.UnloadModel(_sphere);
            if (_ownsFont)
            {
                Raylib.UnloadFont(_uiFont);
            }

            _contentLoaded = false;
        }
    }

    private static Font LoadUiFont(int size, out bool owned)
    {
        string[] candidates =
        {
            @"C:\Windows\Fonts\segoeui.ttf",
            @"C:\Windows\Fonts\arial.ttf",
            "/System/Library/Fonts/Helvetica.ttc",
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        };

        foreach (string path in candidates)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var font = Raylib.LoadFontEx(path, size, null, 0);
            if (Raylib.IsFontValid(font) && font.Texture.Id != 0)
            {
                Raylib.SetTextureFilter(font.Texture, TextureFilter.Bilinear);
                owned = true;
                return font;
            }
        }

        owned = false;
        return Raylib.GetFontDefault();
    }

    // ---------------------------------------------------------------- 更新

    private void Update(float dt)
    {
        HandleStateInput();

        if (_state == GameState.Playing)
        {
            _elapsed += dt;
            _spawnTimer -= dt;

            int maxEnemies = Math.Min(9, 3 + (int)(_elapsed / 16f));
            if (_spawnTimer <= 0f && _enemies.Count < maxEnemies)
            {
                SpawnEnemy();
                _spawnTimer = MathF.Max(0.85f, 2.6f - _elapsed * 0.025f);
            }
        }

        if (_state is GameState.Intro or GameState.Playing)
        {
            _player.Update(dt, this, allowInput: _state == GameState.Playing, allowFire: _state == GameState.Playing);
        }

        if (_state != GameState.Paused)
        {
            UpdateEnemies(dt);
            UpdateBullets(dt);
            UpdateClouds();
            _particles.Update(dt);

            _hitFlash = MathF.Max(0f, _hitFlash - dt * 2.2f);
            _shake = MathF.Max(0f, _shake - dt * 2.6f);
            _hitMarker = MathF.Max(0f, _hitMarker - dt * 3.5f);
            _muzzleGlow = MathF.Max(0f, _muzzleGlow - dt * 9f);
        }

        UpdateCamera(dt);
    }

    private void HandleStateInput()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.F11))
        {
            Raylib.ToggleFullscreen();
        }

        switch (_state)
        {
            case GameState.Intro:
                if (Raylib.IsKeyPressed(KeyboardKey.Space) ||
                    Raylib.IsKeyPressed(KeyboardKey.Enter) ||
                    Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    StartRun();
                }

                break;

            case GameState.Playing:
                if (Raylib.IsKeyPressed(KeyboardKey.P))
                {
                    _state = GameState.Paused;
                }

                break;

            case GameState.Paused:
                if (Raylib.IsKeyPressed(KeyboardKey.P))
                {
                    _state = GameState.Playing;
                }

                break;

            case GameState.Dead:
                if (Raylib.IsKeyPressed(KeyboardKey.R))
                {
                    StartRun();
                }

                break;
        }
    }

    private void StartRun()
    {
        _enemies.Clear();
        _bullets.Clear();
        _particles.Clear();
        _player.Reset();

        _kills = 0;
        _elapsed = 0f;
        _spawnTimer = 1.6f;
        _hitFlash = 0f;
        _shake = 0f;
        _hitMarker = 0f;
        _state = GameState.Playing;

        InitCamera();
        Raylib.DisableCursor();
    }

    private void UpdateEnemies(float dt)
    {
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            var enemy = _enemies[i];
            if (!enemy.Alive)
            {
                _enemies.RemoveAt(i);
                continue;
            }

            enemy.Update(dt, this);

            if (Vector3.DistanceSquared(enemy.Position, _player.Position) > 1200f * 1200f)
            {
                _enemies.RemoveAt(i);
                continue;
            }

            if (_player.Alive &&
                _state != GameState.Intro &&
                Vector3.DistanceSquared(enemy.Position, _player.Position) < 6.5f * 6.5f)
            {
                _enemies.RemoveAt(i);
                _particles.Explosion(enemy.Position, 1.2f);
                _shake = MathF.Min(1.8f, _shake + 1.1f);
                DamagePlayer(15f);
            }
        }
    }

    private void UpdateBullets(float dt)
    {
        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            var bullet = _bullets[i];
            bullet.Life -= dt;
            if (bullet.Life <= 0f)
            {
                _bullets.RemoveAt(i);
                continue;
            }

            var previous = bullet.Position;
            bullet.Position += bullet.Velocity * dt;
            bool consumed = false;

            if (bullet.FromPlayer)
            {
                foreach (var enemy in _enemies)
                {
                    if (!enemy.Alive || !MathUtil.SegmentHitsSphere(previous, bullet.Position, enemy.Position, EnemyAircraft.HitRadius))
                    {
                        continue;
                    }

                    enemy.Health -= 1f;
                    enemy.HitFlash = 1f;
                    _hitMarker = 1f;
                    _particles.Sparks(bullet.Position, 6, new Color(255, 240, 200), 16f);

                    if (enemy.Health <= 0f)
                    {
                        enemy.Alive = false;
                        _kills++;
                        _shake = MathF.Min(1.2f, _shake + 0.3f);
                        _particles.Explosion(enemy.Position, 1.5f);
                    }

                    consumed = true;
                    break;
                }
            }
            else if (_player.Alive && MathUtil.SegmentHitsSphere(previous, bullet.Position, _player.Position, PlayerAircraft.HitRadius))
            {
                _particles.Sparks(bullet.Position, 6, new Color(255, 130, 90), 14f);
                DamagePlayer(8f);
                consumed = true;
            }

            if (consumed)
            {
                _bullets.RemoveAt(i);
                continue;
            }

            _bullets[i] = bullet;
        }
    }

    private void UpdateCamera(float dt)
    {
        Vector3 desiredPosition = _player.Position - _player.Forward * 15f + _player.Up * 4.0f;
        Vector3 desiredTarget = _player.Position + _player.Forward * 30f;

        _camera.Position = MathUtil.Damp(_camera.Position, desiredPosition, 9f, dt);
        _camera.Target = MathUtil.Damp(_camera.Target, desiredTarget, 15f, dt);

        var up = MathUtil.Damp(_camera.Up, _player.Up, 10f, dt);
        _camera.Up = up.LengthSquared() < 1e-4f ? Vector3.UnitY : Vector3.Normalize(up);
        _camera.FovY = 62f + _player.BoostAmount * 10f;

        if (_shake > 0.001f)
        {
            var jitter = new Vector3(
                _rng.NextSingle() * 2f - 1f,
                _rng.NextSingle() * 2f - 1f,
                _rng.NextSingle() * 2f - 1f);
            float amount = _shake * _shake;
            _camera.Position += jitter * (amount * 1.1f);
            _camera.Target += jitter * (amount * 0.45f);
        }
    }

    private void UpdateClouds()
    {
        const float half = 700f;
        const float halfY = 300f;

        for (int i = 0; i < _clouds.Count; i++)
        {
            var cloud = _clouds[i];
            var relative = cloud.Position - _player.Position;

            if (relative.X > half)
            {
                cloud.Position.X -= half * 2f;
            }
            else if (relative.X < -half)
            {
                cloud.Position.X += half * 2f;
            }

            if (relative.Z > half)
            {
                cloud.Position.Z -= half * 2f;
            }
            else if (relative.Z < -half)
            {
                cloud.Position.Z += half * 2f;
            }

            if (relative.Y > halfY)
            {
                cloud.Position.Y -= halfY * 2f;
            }
            else if (relative.Y < -halfY)
            {
                cloud.Position.Y += halfY * 2f;
            }

            _clouds[i] = cloud;
        }
    }

    // ---------------------------------------------------------------- 生成

    private void SpawnEnemy()
    {
        var forward = _player.Forward;
        var right = _player.Right;
        var up = Vector3.Cross(forward, right);

        // 生成在玩家前方不太偏的位置，并朝大致相同的方向飞（方便玩家追击）
        float yawOffset = (_rng.NextSingle() * 2f - 1f) * 0.55f;
        float pitchOffset = (_rng.NextSingle() * 2f - 1f) * 0.22f;
        var direction = Vector3.Normalize(forward + right * MathF.Tan(yawOffset) + up * MathF.Tan(pitchOffset));
        float distance = 140f + _rng.NextSingle() * 70f;

        var position = _player.Position + direction * distance;
        var heading = Vector3.Normalize(forward +
                                        right * ((_rng.NextSingle() * 2f - 1f) * 0.35f) +
                                        up * ((_rng.NextSingle() * 2f - 1f) * 0.15f));

        _enemies.Add(new EnemyAircraft
        {
            Position = position,
            Forward = heading,
            Speed = 44f + _rng.NextSingle() * 6f,
            Health = 3f,
            FireTimer = 1.4f + _rng.NextSingle() * 1.2f,
            Phase = _rng.NextSingle() * MathF.Tau,
            StationDistance = 130f + _rng.NextSingle() * 80f,
            StationSide = (_rng.NextSingle() * 2f - 1f) * 30f,
            AttackTimer = 2.5f + _rng.NextSingle() * 3f,
            // 基础命中率 30%~50%，随时间缓慢提升
            Accuracy = Math.Clamp(0.30f + _elapsed * 0.0015f + _rng.NextSingle() * 0.2f, 0.25f, 0.7f),
        });
    }

    public void SpawnPlayerBullet(Vector3 origin, Vector3 forward, Vector3 right)
    {
        float side = _gunSide ? 0.78f : -0.78f;
        _gunSide = !_gunSide;

        var muzzle = origin + right * side;
        _bullets.Add(new Projectile(muzzle, forward * 430f, 2.0f, true));
        _particles.MuzzleFlash(muzzle, forward, right);
        _muzzleGlow = 1f;
    }

    /// <summary>
    /// 敌机开火：按 accuracy 的概率决定这一发是"瞄准命中"还是"故意打偏"。
    /// 命中弹落在机体角半径内，脱靶弹则从机体边缘外侧擦过，视觉上就是很自然的近失弹。
    /// </summary>
    public void SpawnEnemyShot(Vector3 origin, Vector3 aimPoint, float accuracy, Random rng)
    {
        var offset = aimPoint - origin;
        float distance = MathF.Max(offset.Length(), 1f);
        var direction = offset / distance;

        // 命中所需的角度半径
        float hullAngle = MathF.Atan(PlayerAircraft.HitRadius / distance);
        bool aimsToHit = rng.NextSingle() < Math.Clamp(accuracy, 0.05f, 0.95f);

        float errorAngle = aimsToHit
            ? hullAngle * 0.8f * MathF.Sqrt(rng.NextSingle())
            : hullAngle * (1.2f + rng.NextSingle() * 1.2f);

        var axis = Vector3.Cross(direction, Vector3.UnitY);
        if (axis.LengthSquared() < 1e-4f)
        {
            axis = Vector3.Cross(direction, Vector3.UnitZ);
        }

        axis = Vector3.Normalize(axis);
        float spin = rng.NextSingle() * MathF.Tau;
        var perpendicular = Vector3.TransformNormal(axis, Matrix4x4.CreateFromAxisAngle(direction, spin));
        var shotDirection = Vector3.Normalize(direction * MathF.Cos(errorAngle) + perpendicular * MathF.Sin(errorAngle));

        _bullets.Add(new Projectile(origin, shotDirection * 300f, 2.8f, false));
    }

    private void DamagePlayer(float amount)
    {
        if (!_player.Alive)
        {
            return;
        }

        bool dead = _player.Damage(amount);
        _hitFlash = MathF.Min(1f, _hitFlash + 0.85f);
        _shake = MathF.Min(1.8f, _shake + 0.8f);
        _particles.Sparks(_player.Position, 12, new Color(255, 210, 130), 24f);

        if (dead)
        {
            _player.Alive = false;
            _particles.Explosion(_player.Position, 2.2f);
            _shake = 1.8f;
            _hitFlash = 1f;
            _state = GameState.Dead;
            Raylib.EnableCursor();
        }
    }

    private void InitCamera()
    {
        _camera = new Camera3D
        {
            Position = _player.Position - _player.Forward * 15f + _player.Up * 4.0f,
            Target = _player.Position + _player.Forward * 30f,
            Up = _player.Up,
            FovY = 62f,
        };
    }

    private void CreateClouds()
    {
        _clouds = new List<Cloud>(100);
        for (int i = 0; i < 100; i++)
        {
            _clouds.Add(new Cloud
            {
                Position = new Vector3(
                    _rng.NextSingle() * 1400f - 700f,
                    _rng.NextSingle() * 640f - 300f,
                    _rng.NextSingle() * 1400f - 700f),
                Radius = 7f + _rng.NextSingle() * 14f,
            });
        }
    }

    // ---------------------------------------------------------------- 绘制

    private void Draw()
    {
        Raylib.BeginDrawing();

        // 必须清理深度缓冲，否则上一帧的深度会挡住本帧的 3D 物体；
        // 2D 天空只是视觉背景，不参与深度写入。
        Raylib.ClearBackground(SkyBottom);
        Rlgl.DisableDepthTest();
        Raylib.DrawRectangleGradientV(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), SkyTop, SkyBottom);

        Raylib.BeginMode3D(_camera);
        DrawClouds();
        DrawEnemies();
        if (_player.Alive)
        {
            DrawPlayer();
        }

        DrawBullets();
        _particles.Draw();
        Raylib.EndMode3D();

        Hud.Draw(this, _camera);

        Raylib.EndDrawing();
    }

    private void DrawPlayer()
    {
        var tint = MathUtil.Lerp(PlayerTint, new Color(255, 120, 90), _player.HitFlash * 0.85f);
        DrawAircraft(_fighter, _player.Position, _player.Forward, _player.Up, 0.7f, tint);

        var right = _player.Right;
        foreach (float side in new[] { -1f, 1f })
        {
            var nozzle = _player.Position - _player.Forward * 3.15f + right * (0.36f * side);
            var flame = Raylib.Fade(new Color(130, 200, 255), 0.75f);
            Raylib.DrawSphereEx(nozzle, 0.2f + _player.BoostAmount * 0.08f, 6, 6, new Color(170, 225, 255));
            Raylib.DrawLine3D(nozzle, nozzle - _player.Forward * (1.1f + _player.BoostAmount * 3.6f), flame);
        }
    }

    private void DrawEnemies()
    {
        foreach (var enemy in _enemies)
        {
            var tint = MathUtil.Lerp(EnemyTint, new Color(255, 225, 185), enemy.HitFlash);
            DrawAircraft(_fighter, enemy.Position, enemy.Forward, enemy.Up, EnemyAircraft.Scale, tint);

            var nozzle = enemy.Position - enemy.Forward * 3.1f;
            Raylib.DrawSphereEx(nozzle, 0.26f, 6, 6, new Color(255, 120, 70));
        }
    }

    private void DrawBullets()
    {
        foreach (var bullet in _bullets)
        {
            var color = bullet.FromPlayer ? new Color(255, 250, 180) : new Color(255, 130, 70);
            Raylib.DrawLine3D(bullet.Position, bullet.Position - bullet.Velocity * 0.04f, color);
            Raylib.DrawSphereEx(bullet.Position, bullet.FromPlayer ? 0.22f : 0.3f, 5, 5, color);
        }
    }

    private void DrawClouds()
    {
        var cameraPosition = _camera.Position;

        foreach (var cloud in _clouds)
        {
            float distance = Vector3.Distance(cloud.Position, cameraPosition);
            float fade = Math.Clamp((distance - 160f) / 850f, 0f, 1f);
            var tint = MathUtil.Lerp(new Color(250, 252, 255), SkyBottom, fade);
            var scale = new Vector3(cloud.Radius * 1.7f, cloud.Radius * 0.62f, cloud.Radius * 1.15f);

            Raylib.DrawModelEx(_sphere, cloud.Position, Vector3.UnitY, 0f, scale, tint);
            Raylib.DrawModelEx(_sphere, cloud.Position + new Vector3(cloud.Radius * 0.85f, cloud.Radius * 0.18f, -cloud.Radius * 0.25f),
                Vector3.UnitY, 0f, scale * 0.68f, tint);
        }
    }

    private static void DrawAircraft(Model template, Vector3 position, Vector3 forward, Vector3 up, float scale, Color tint)
    {
        // 把正交基转成四元数，再交给 DrawModelEx 的轴角接口；
        // 这样不依赖 Model.Transform，避免 raylib 6 在带平移的模型变换下出现异常三角面。
        var basis = MathUtil.Basis(forward, up);
        var rotation = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(basis));
        var axis = new Vector3(rotation.X, rotation.Y, rotation.Z);
        float sin = axis.Length();
        float angle = 0f;

        if (sin > 1e-5f)
        {
            axis /= sin;
            angle = 2f * MathF.Atan2(sin, rotation.W) * (180f / MathF.PI);
        }
        else
        {
            axis = Vector3.UnitY;
        }

        Raylib.DrawModelEx(template, position, axis, angle, new Vector3(scale), tint);
    }

    private struct Cloud
    {
        public Vector3 Position;
        public float Radius;
    }
}
