using System.Numerics;
using Raylib_cs;

namespace AirCombat.GamePlay;

/// <summary>玩家战机：鼠标/键盘控制俯仰与偏航，横滚只做视觉表现。</summary>
public sealed class PlayerAircraft
{
    public const float MaxHealth = 100f;
    public const float CruiseSpeed = 50f;
    public const float BoostSpeed = 82f;

    /// <summary>被弹判定半径（敌机瞄准时也用它换算角半径）。</summary>
    public const float HitRadius = 3.8f;

    private float _smoothedYawRate;

    public Vector3 Position = Vector3.Zero;
    public Vector3 Forward = -Vector3.UnitZ;
    public Vector3 Up = Vector3.UnitY;

    public float Yaw;
    public float Pitch;
    public float Roll;
    public float Speed = CruiseSpeed;
    public float Health = MaxHealth;
    public float FireCooldown;
    public float HitFlash;
    public float BoostAmount;
    public bool Alive = true;

    public Vector3 Right => Vector3.Normalize(Vector3.Cross(Up, Forward));

    public void Reset()
    {
        Position = Vector3.Zero;
        Forward = -Vector3.UnitZ;
        Up = Vector3.UnitY;
        Yaw = 0f;
        Pitch = 0f;
        Roll = 0f;
        Speed = CruiseSpeed;
        Health = MaxHealth;
        FireCooldown = 0f;
        HitFlash = 0f;
        BoostAmount = 0f;
        Alive = true;
        _smoothedYawRate = 0f;
    }

    public bool Damage(float amount)
    {
        if (!Alive)
        {
            return false;
        }

        Health = MathF.Max(0f, Health - amount);
        HitFlash = 1f;
        return Health <= 0f;
    }

    public void Update(float dt, Game game, bool allowInput, bool allowFire)
    {
        if (!Alive)
        {
            return;
        }

        float yawBefore = Yaw;

        if (allowInput)
        {
            var mouse = Raylib.GetMouseDelta();
            mouse.X = Math.Clamp(mouse.X, -220f, 220f);
            mouse.Y = Math.Clamp(mouse.Y, -220f, 220f);

            // 机头朝 -Z：正偏航是左转，所以鼠标右移 / D 需要减小偏航角
            Yaw -= mouse.X * 0.0022f;
            Pitch -= mouse.Y * 0.0022f;

            if (Raylib.IsKeyDown(KeyboardKey.A) || Raylib.IsKeyDown(KeyboardKey.Left))
            {
                Yaw += 1.6f * dt;
            }

            if (Raylib.IsKeyDown(KeyboardKey.D) || Raylib.IsKeyDown(KeyboardKey.Right))
            {
                Yaw -= 1.6f * dt;
            }

            if (Raylib.IsKeyDown(KeyboardKey.W) || Raylib.IsKeyDown(KeyboardKey.Up))
            {
                Pitch += 1.1f * dt;
            }

            if (Raylib.IsKeyDown(KeyboardKey.S) || Raylib.IsKeyDown(KeyboardKey.Down))
            {
                Pitch -= 1.1f * dt;
            }

            Pitch = Math.Clamp(Pitch, -1.15f, 1.15f);
        }
        else
        {
            // 标题界面的自动巡航，让画面动起来
            Yaw += 0.07f * dt;
        }

        float yawRate = (Yaw - yawBefore) / MathF.Max(dt, 0.0001f);
        _smoothedYawRate = MathUtil.Damp(_smoothedYawRate, yawRate, 10f, dt);
        Roll = MathUtil.Damp(Roll, Math.Clamp(-_smoothedYawRate * 0.34f, -0.95f, 0.95f), 7f, dt);

        // 偏航角保持在 [-π, π]，避免长时间飞行后浮点精度下降
        if (Yaw > MathF.PI)
        {
            Yaw -= MathF.Tau;
        }
        else if (Yaw < -MathF.PI)
        {
            Yaw += MathF.Tau;
        }

        var orientation =
            Matrix4x4.CreateRotationZ(Roll) *
            Matrix4x4.CreateRotationX(Pitch) *
            Matrix4x4.CreateRotationY(Yaw);

        Forward = Vector3.Normalize(-Vector3.TransformNormal(Vector3.UnitZ, orientation));
        Up = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitY, orientation));

        bool boosting = allowInput &&
                        (Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift));

        Speed = MathUtil.Damp(Speed, boosting ? BoostSpeed : CruiseSpeed, 2.6f, dt);
        BoostAmount = MathUtil.Damp(BoostAmount, boosting ? 1f : 0f, 4f, dt);

        Position += Forward * Speed * dt;

        FireCooldown -= dt;
        if (allowFire &&
            FireCooldown <= 0f &&
            (Raylib.IsKeyDown(KeyboardKey.Space) || Raylib.IsMouseButtonDown(MouseButton.Left)))
        {
            FireCooldown = 0.11f;
            game.SpawnPlayerBullet(Position + Forward * 3.1f, Forward, Right);
        }

        HitFlash = MathF.Max(0f, HitFlash - dt * 4f);
    }
}
