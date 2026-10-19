using System.Numerics;
using Raylib_cs;

namespace AirCombat.GamePlay;

public struct Particle
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float Life;
    public float MaxLife;
    public float Size;
    public float Drag;
    public Color Color;
}

/// <summary>简单的立方体粒子系统：爆炸碎屑、火花、烟尘、机炮闪光。</summary>
public sealed class ParticleSystem
{
    private const int MaxParticles = 2000;
    private readonly List<Particle> _particles = new();
    private readonly Random _rng = new();

    public int Count => _particles.Count;

    public void Clear() => _particles.Clear();

    public void Add(Vector3 position, Vector3 velocity, float life, float size, Color color, float drag = 1.6f)
    {
        if (_particles.Count >= MaxParticles)
        {
            return;
        }

        _particles.Add(new Particle
        {
            Position = position,
            Velocity = velocity,
            Life = life,
            MaxLife = life,
            Size = size,
            Drag = drag,
            Color = color,
        });
    }

    public void Explosion(Vector3 position, float scale)
    {
        var hot = new Color(255, 225, 140);
        var fire = new Color(255, 150, 60);
        var smoke = new Color(90, 90, 100);

        for (int i = 0; i < (int)(36 * scale); i++)
        {
            var dir = RandomDirection();
            float speed = (8f + _rng.NextSingle() * 26f) * scale;
            Add(position, dir * speed, 0.6f + _rng.NextSingle() * 1.1f, 0.18f + _rng.NextSingle() * 0.35f,
                _rng.NextSingle() < 0.5f ? hot : fire, 1.4f);
        }

        for (int i = 0; i < (int)(10 * scale); i++)
        {
            var dir = RandomDirection();
            Add(position, dir * (2f + _rng.NextSingle() * 6f), 1.6f + _rng.NextSingle() * 1.6f,
                0.7f + _rng.NextSingle() * 1.3f, smoke, 0.9f);
        }
    }

    public void Sparks(Vector3 position, int count, Color color, float speed)
    {
        for (int i = 0; i < count; i++)
        {
            Add(position, RandomDirection() * (speed * 0.3f + _rng.NextSingle() * speed),
                0.15f + _rng.NextSingle() * 0.3f, 0.10f + _rng.NextSingle() * 0.12f, color, 3f);
        }
    }

    public void MuzzleFlash(Vector3 position, Vector3 direction, Vector3 right)
    {
        for (int i = 0; i < 4; i++)
        {
            var spread = right * ((_rng.NextSingle() - 0.5f) * 3f) + RandomDirection() * 0.6f;
            Add(position, direction * 18f + spread * 4f, 0.1f + _rng.NextSingle() * 0.08f,
                0.14f + _rng.NextSingle() * 0.1f, new Color(255, 240, 190), 5f);
        }
    }

    public void Update(float dt)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Life -= dt;
            if (p.Life <= 0f)
            {
                _particles.RemoveAt(i);
                continue;
            }

            p.Position += p.Velocity * dt;
            p.Velocity *= MathF.Exp(-p.Drag * dt);
            _particles[i] = p;
        }
    }

    public void Draw()
    {
        foreach (var p in _particles)
        {
            float life = Math.Clamp(p.Life / p.MaxLife, 0f, 1f);
            var color = Raylib.Fade(p.Color, life);
            Raylib.DrawCube(p.Position, p.Size, p.Size, p.Size, color);
        }
    }

    private Vector3 RandomDirection()
    {
        var v = new Vector3(
            _rng.NextSingle() * 2f - 1f,
            _rng.NextSingle() * 2f - 1f,
            _rng.NextSingle() * 2f - 1f);
        return v.LengthSquared() < 1e-4f ? Vector3.UnitY : Vector3.Normalize(v);
    }
}
