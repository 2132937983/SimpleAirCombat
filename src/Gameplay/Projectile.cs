using System.Numerics;

namespace AirCombat.GamePlay;

public struct Projectile
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float Life;
    public bool FromPlayer;

    public Projectile(Vector3 position, Vector3 velocity, float life, bool fromPlayer)
    {
        Position = position;
        Velocity = velocity;
        Life = life;
        FromPlayer = fromPlayer;
    }
}
