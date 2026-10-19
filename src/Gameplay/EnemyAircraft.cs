using System.Numerics;
using Raylib_cs;

namespace AirCombat.GamePlay;

/// <summary>
/// 敌机：平时保持在玩家前方一段"站位"上蛇形巡航，周期性进入攻击状态掉头咬人。
/// 这样敌机大多数时间都停在玩家视线正前方，玩家有足够时间瞄准射击。
/// </summary>
public sealed class EnemyAircraft
{
    public Vector3 Position;
    public Vector3 Forward = -Vector3.UnitZ;
    public Vector3 Up = Vector3.UnitY;

    public float Speed = 55f;
    public float Health = 3f;
    public float FireTimer = 1.5f;
    public float Age;
    public float Phase;
    public float HitFlash;
    public float Bank;
    public bool Alive = true;

    /// <summary>是否处于主动攻击（掉头咬尾）状态。</summary>
    public bool Attacking;
    public float AttackTimer = 3f;

    /// <summary>巡航时相对玩家的站位距离与横向偏移。</summary>
    public float StationDistance = 160f;
    public float StationSide;

    /// <summary>瞄准后的命中概率（0~1），距离越远实际命中率越低。</summary>
    public float Accuracy = 0.4f;

    public const float Scale = 0.7f;
    public const float HitRadius = 5.5f;

    public void Update(float dt, Game game)
    {
        if (!Alive)
        {
            return;
        }

        Age += dt;
        var player = game.Player;
        var toPlayer = player.Position - Position;
        float distance = MathF.Max(toPlayer.Length(), 0.001f);

        AttackTimer -= dt;
        if (Attacking && distance < 40f)
        {
            // 冲得太近就立刻脱离，别直接撞上来
            Attacking = false;
            AttackTimer = 4f + game.Random.NextSingle() * 2f;
        }

        if (AttackTimer <= 0f)
        {
            Attacking = !Attacking;
            AttackTimer = Attacking
                ? 2.4f + game.Random.NextSingle() * 1.4f
                : 5f + game.Random.NextSingle() * 3f;
        }

        Vector3 desired;
        if (Attacking)
        {
            var aimPoint = player.Position + player.Forward * player.Speed * (distance / 420f);
            desired = Vector3.Normalize(aimPoint - Position);
        }
        else if (distance < 55f)
        {
            // 太近就拉开，避免直接撞机
            desired = Vector3.Normalize(Position - player.Position + player.Forward * 30f + Vector3.UnitY * 6f);
        }
        else
        {
            // 站在玩家机头前方，左右 / 上下缓慢摆动
            var station = player.Position +
                          player.Forward * StationDistance +
                          player.Right * (MathF.Sin(Age * 0.6f + Phase) * 35f + StationSide) +
                          Vector3.UnitY * (MathF.Sin(Age * 0.42f + Phase * 1.7f) * 18f);

            desired = Vector3.Normalize(station - Position);
        }

        float turn = TurnTowards(desired, 1.05f, dt);
        Position += Forward * Speed * dt;

        FireTimer -= dt;
        if (FireTimer <= 0f && distance < 420f)
        {
            var aimPoint = player.Position + player.Forward * player.Speed * (distance / 340f);
            var aimDirection = Vector3.Normalize(aimPoint - Position);
            if (Vector3.Dot(Forward, aimDirection) > 0.965f)
            {
                // 距离越远越难打准
                float distanceFactor = Math.Clamp(1.25f - distance / 800f, 0.55f, 1.1f);
                game.SpawnEnemyShot(Position + Forward * 2.9f, aimPoint, Accuracy * distanceFactor, game.Random);
                FireTimer = 1.5f + game.Random.NextSingle() * 1f;
            }
        }

        HitFlash = MathF.Max(0f, HitFlash - dt * 5f);

        Bank = MathUtil.Damp(Bank, Math.Clamp(-turn * 1.4f, -0.95f, 0.95f), 4f, dt);
        var baseUp = Vector3.UnitY - Forward * Vector3.Dot(Vector3.UnitY, Forward);
        if (baseUp.LengthSquared() < 1e-4f)
        {
            baseUp = Vector3.UnitZ;
        }

        Up = Vector3.Normalize(Vector3.TransformNormal(Vector3.Normalize(baseUp),
            Matrix4x4.CreateFromAxisAngle(Forward, Bank)));
    }

    /// <summary>朝目标方向转向，返回带符号的转向速率（用于压坡度）。</summary>
    private float TurnTowards(Vector3 desired, float maxRadiansPerSecond, float dt)
    {
        var axis = Vector3.Cross(Forward, desired);
        float sin = axis.Length();
        float dot = Math.Clamp(Vector3.Dot(Forward, desired), -1f, 1f);

        if (sin < 1e-5f)
        {
            return 0f;
        }

        float angle = MathF.Atan2(sin, dot);
        float step = MathF.Min(angle, maxRadiansPerSecond * dt);
        axis /= sin;

        var rotation = Matrix4x4.CreateFromAxisAngle(axis, step);
        Forward = Vector3.Normalize(Vector3.TransformNormal(Forward, rotation));

        float signedTurn = Vector3.Dot(axis, Vector3.UnitY) * (step / MathF.Max(dt, 0.0001f));
        return -signedTurn;
    }
}
