using System.Numerics;
using Raylib_cs;

namespace AirCombat.GamePlay;

/// <summary>2D 界面层：准星、血量、分数、敌机标记与各种提示。</summary>
public static class Hud
{
    private static readonly Color PanelColor = new(8, 14, 24, 150);
    private static readonly Color Accent = new(120, 200, 255);
    private static readonly Color Warn = new(255, 96, 82);

    public static void Draw(Game game, Camera3D camera)
    {
        int width = Raylib.GetScreenWidth();
        int height = Raylib.GetScreenHeight();
        var font = game.UiFont;

        Vector2 reticle = GetReticlePosition(game, camera, width, height);
        DrawEnemyMarkers(game, camera, font, width, height);
        DrawReticle(game, reticle);

        if (game.State != GameState.Intro)
        {
            DrawTopLeft(game, font);
            DrawCompass(game, font, width);
            DrawHealth(game, font, height);
            DrawSpeed(game, font, width, height);
        }

        if (game.DamageFlash > 0.01f)
        {
            Raylib.DrawRectangle(0, 0, width, height, Raylib.Fade(new Color(255, 40, 30), game.DamageFlash * 0.28f));
        }

        if (game.State == GameState.Playing && game.Player.Health < 35f)
        {
            DrawCriticalAlert(game, font, width, height);
        }

        if (game.State == GameState.Intro)
        {
            DrawIntro(font, width, height);
        }
        else if (game.State == GameState.Paused)
        {
            DrawOverlayPanel(width, height);
            TextCentered(font, "PAUSED", width * 0.5f, height * 0.44f, 54f, Color.White);
            TextCentered(font, "PRESS [P] TO RESUME", width * 0.5f, height * 0.54f, 22f, Accent);
        }
        else if (game.State == GameState.Dead)
        {
            DrawOverlayPanel(width, height);
            TextCentered(font, "HULL DESTROYED", width * 0.5f, height * 0.36f, 56f, Warn);
            TextCentered(font, $"SCORE {game.Score}      BANDITS DOWN {game.Kills}", width * 0.5f, height * 0.47f, 26f, Color.White);
            float pulse = 0.55f + 0.45f * MathF.Sin((float)Raylib.GetTime() * 5f);
            TextCentered(font, "PRESS [R] TO FLY AGAIN", width * 0.5f, height * 0.59f, 26f, Raylib.Fade(new Color(255, 230, 150), pulse));
        }
    }

    private static Vector2 GetReticlePosition(Game game, Camera3D camera, int width, int height)
    {
        Vector3 aimPoint = game.Player.Position + game.Player.Forward * 500f;
        var cameraForward = Vector3.Normalize(camera.Target - camera.Position);
        bool inFront = Vector3.Dot(aimPoint - camera.Position, cameraForward) > 1f;

        var screen = Raylib.GetWorldToScreen(aimPoint, camera);
        if (!inFront || screen.X < 0f || screen.X > width || screen.Y < 0f || screen.Y > height)
        {
            return new Vector2(width * 0.5f, height * 0.5f);
        }

        return screen;
    }

    private static void DrawReticle(Game game, Vector2 center)
    {
        float glow = game.MuzzleGlow;
        float radius = 10f + glow * 5f;
        var color = Raylib.Fade(new Color(220, 255, 235), 0.9f);

        Raylib.DrawCircleV(center, 1.6f, color);

        Vector2[] directions =
        {
            new(1f, 0f), new(-1f, 0f), new(0f, 1f), new(0f, -1f),
        };

        foreach (var dir in directions)
        {
            Raylib.DrawLineEx(center + dir * radius, center + dir * (radius + 8f), 1.6f, color);
        }

        if (game.HitMarker > 0.01f)
        {
            var hitColor = Raylib.Fade(new Color(255, 210, 120), game.HitMarker);
            float r = 13f;
            Raylib.DrawLineEx(center + new Vector2(-r, -r), center + new Vector2(-r * 0.4f, -r * 0.4f), 2.4f, hitColor);
            Raylib.DrawLineEx(center + new Vector2(r, -r), center + new Vector2(r * 0.4f, -r * 0.4f), 2.4f, hitColor);
            Raylib.DrawLineEx(center + new Vector2(-r, r), center + new Vector2(-r * 0.4f, r * 0.4f), 2.4f, hitColor);
            Raylib.DrawLineEx(center + new Vector2(r, r), center + new Vector2(r * 0.4f, r * 0.4f), 2.4f, hitColor);
        }

        if (game.Player.BoostAmount > 0.08f)
        {
            for (int i = 0; i < 22; i++)
            {
                float angle = i * MathF.Tau / 22f + game.Elapsed * 0.6f;
                var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                float start = 180f + (i % 5) * 34f;
                float length = 130f * game.Player.BoostAmount;
                var color2 = Raylib.Fade(new Color(230, 245, 255), 0.16f * game.Player.BoostAmount);
                Raylib.DrawLineEx(center + dir * start, center + dir * (start + length), 1.5f, color2);
            }
        }
    }

    private static void DrawEnemyMarkers(Game game, Camera3D camera, Font font, int width, int height)
    {
        var cameraForward = Vector3.Normalize(camera.Target - camera.Position);

        foreach (var enemy in game.Enemies)
        {
            if (!enemy.Alive)
            {
                continue;
            }

            var offset = enemy.Position - camera.Position;
            if (Vector3.Dot(offset, cameraForward) <= 0f)
            {
                continue;
            }

            var screen = Raylib.GetWorldToScreen(enemy.Position, camera);
            if (screen.X < 0f || screen.X > width || screen.Y < 0f || screen.Y > height)
            {
                continue;
            }

            float distance = offset.Length();
            var color = Raylib.Fade(new Color(255, 110, 100), 0.7f);
            Raylib.DrawCircleLines((int)screen.X, (int)screen.Y, 11f, color);
            Text(font, $"{distance:0}", screen.X + 14f, screen.Y - 8f, 14f, Raylib.Fade(color, 0.85f));
        }
    }

    /// <summary>顶部罗盘条：显示当前航向（N/E/S/W + 度数），并把敌机水平方位画成红点。</summary>
    private static void DrawCompass(Game game, Font font, int width)
    {
        var player = game.Player;
        float heading = MathUtil.HeadingOf(player.Forward);

        float tapeWidth = MathF.Min(660f, width * 0.56f);
        const float halfSpan = 70f;
        float pixelsPerDegree = tapeWidth / (halfSpan * 2f);
        float centerX = width * 0.5f;
        float panelY = 40f;
        float panelHeight = 50f;

        string readout = $"HDG {heading:000}";
        float readoutWidth = Measure(font, readout, 20f);
        Raylib.DrawRectangleRounded(new Rectangle(centerX - readoutWidth * 0.5f - 14f, 8f, readoutWidth + 28f, 28f), 0.4f, 8, PanelColor);
        TextCentered(font, readout, centerX, 12f, 20f, new Color(228, 242, 255));

        var panel = new Rectangle(centerX - tapeWidth * 0.5f, panelY, tapeWidth, panelHeight);
        Raylib.DrawRectangleRounded(panel, 0.28f, 8, new Color(8, 14, 24, 165));
        Raylib.DrawRectangleLinesEx(panel, 1f, new Color(120, 200, 255, 55));

        Raylib.BeginScissorMode((int)panel.X + 4, (int)panel.Y, (int)panel.Width - 8, (int)panel.Height);

        int first = (int)MathF.Floor((heading - halfSpan) / 5f) * 5;
        int last = (int)MathF.Ceiling((heading + halfSpan) / 5f) * 5;
        float tickTop = panelY + 8f;

        for (int degree = first; degree <= last; degree += 5)
        {
            float delta = MathUtil.ShortestAngle(degree - heading);
            if (MathF.Abs(delta) > halfSpan)
            {
                continue;
            }

            float x = centerX + delta * pixelsPerDegree;
            int normalized = ((degree % 360) + 360) % 360;
            bool major = normalized % 30 == 0;

            Raylib.DrawLineEx(new Vector2(x, tickTop), new Vector2(x, tickTop + (major ? 9f : 5f)),
                major ? 1.5f : 1f, new Color(175, 215, 245, major ? 230 : 120));

            if (!major)
            {
                continue;
            }

            string label = normalized switch
            {
                0 => "N",
                90 => "E",
                180 => "S",
                270 => "W",
                _ => normalized.ToString("000"),
            };
            var color = normalized % 90 == 0 ? new Color(140, 215, 255) : new Color(200, 220, 240);
            TextCentered(font, label, x, panelY + 19f, 13f, color);
        }

        float dotY = panelY + panelHeight - 9f;
        foreach (var enemy in game.Enemies)
        {
            if (!enemy.Alive)
            {
                continue;
            }

            var offset = enemy.Position - player.Position;
            float delta = MathUtil.ShortestAngle(MathUtil.HeadingOf(offset) - heading);
            if (MathF.Abs(delta) > halfSpan - 2f)
            {
                continue;
            }

            float x = centerX + delta * pixelsPerDegree;
            float alpha = Math.Clamp(1.35f - offset.Length() / 700f, 0.4f, 1f);
            var color = Raylib.Fade(new Color(255, 72, 64), alpha);
            Raylib.DrawCircleV(new Vector2(x, dotY), 3.5f, color);
            Raylib.DrawCircleLines((int)x, (int)dotY, 5f, Raylib.Fade(color, 0.5f));
        }

        Raylib.EndScissorMode();

        // 中央指示三角（逆时针绕序，否则会被背面剔除）
        Raylib.DrawTriangle(
            new Vector2(centerX + 5f, panelY + 1f),
            new Vector2(centerX - 5f, panelY + 1f),
            new Vector2(centerX, panelY + 8f),
            new Color(255, 210, 120));
    }

    private static void DrawTopLeft(Game game, Font font)
    {
        Raylib.DrawRectangleRounded(new Rectangle(24f, 22f, 236f, 76f), 0.25f, 8, PanelColor);
        Text(font, $"SCORE  {game.Score}", 40f, 32f, 26f, Color.White);
        Text(font, $"BANDITS DOWN  {game.Kills}", 40f, 64f, 16f, Accent);
    }

    private static void DrawHealth(Game game, Font font, int height)
    {
        float ratio = Math.Clamp(game.Player.Health / PlayerAircraft.MaxHealth, 0f, 1f);
        var bar = new Rectangle(40f, height - 62f, 280f, 16f);
        var fill = MathUtil.Lerp(Warn, new Color(120, 235, 160), ratio);

        Raylib.DrawRectangleRounded(new Rectangle(bar.X - 16f, bar.Y - 34f, bar.Width + 32f, bar.Height + 44f), 0.35f, 8, PanelColor);
        Raylib.DrawRectangleRounded(bar, 0.6f, 8, new Color(30, 42, 58, 180));
        if (ratio > 0.001f)
        {
            Raylib.DrawRectangleRounded(new Rectangle(bar.X, bar.Y, bar.Width * ratio, bar.Height), 0.6f, 8, fill);
        }

        Text(font, $"HULL  {(int)MathF.Ceiling(game.Player.Health)}", bar.X, bar.Y - 26f, 18f, Color.White);
    }

    private static void DrawSpeed(Game game, Font font, int width, int height)
    {
        string text = $"SPD  {(int)game.Player.Speed}";
        float textWidth = Measure(font, text, 22f);
        float x = width - textWidth - 40f;

        Raylib.DrawRectangleRounded(new Rectangle(x - 16f, height - 70f, textWidth + 32f, 44f), 0.4f, 8, PanelColor);
        Text(font, text, x, height - 62f, 22f, Color.White);

        float boostWidth = textWidth * game.Player.BoostAmount;
        Raylib.DrawRectangleRounded(new Rectangle(x, height - 30f, boostWidth, 3f), 0.9f, 4, new Color(120, 200, 255, 210));
    }

    private static void DrawCriticalAlert(Game game, Font font, int width, int height)
    {
        float pulse = 0.4f + 0.6f * MathF.Abs(MathF.Sin((float)Raylib.GetTime() * 4f));
        var color = Raylib.Fade(Warn, pulse * 0.75f);

        TextCentered(font, "HULL CRITICAL", width * 0.5f, height * 0.14f, 22f, color);
        Raylib.DrawRectangleLinesEx(new Rectangle(6f, 6f, width - 12f, height - 12f), 3f, Raylib.Fade(Warn, pulse * 0.35f));
    }

    private static void DrawIntro(Font font, int width, int height)
    {
        Raylib.DrawRectangle(0, 0, width, height, new Color(4, 9, 18, 190));

        TextCentered(font, "AIR COMBAT", width * 0.5f, height * 0.22f, 68f, Color.White);
        TextCentered(font, "RAYLIB-CS  /  .NET 10  /  PROCEDURAL 3D FIGHTER", width * 0.5f, height * 0.22f + 84f, 17f, Accent);

        string[] lines =
        {
            "MOUSE  STEER          W / S  PITCH          A / D  YAW",
            "SPACE / LEFT MOUSE  FIRE          LEFT SHIFT  BOOST",
            "P  PAUSE          F11  FULLSCREEN          ESC  QUIT",
        };

        float y = height * 0.52f;
        foreach (string line in lines)
        {
            TextCentered(font, line, width * 0.5f, y, 18f, new Color(196, 216, 238));
            y += 34f;
        }

        float pulse = 0.5f + 0.5f * MathF.Sin((float)Raylib.GetTime() * 4f);
        TextCentered(font, "CLICK OR PRESS SPACE TO START", width * 0.5f, height * 0.8f, 24f,
            Raylib.Fade(new Color(255, 232, 160), 0.35f + pulse * 0.65f));
    }

    private static void DrawOverlayPanel(int width, int height)
    {
        Raylib.DrawRectangle(0, 0, width, height, new Color(3, 7, 14, 195));
    }

    private static void Text(Font font, string text, float x, float y, float size, Color color)
    {
        Raylib.DrawTextEx(font, text, new Vector2(x, y), size, 0.5f, color);
    }

    private static void TextCentered(Font font, string text, float centerX, float y, float size, Color color)
    {
        float width = Measure(font, text, size);
        Raylib.DrawTextEx(font, text, new Vector2(centerX - width * 0.5f, y), size, 0.5f, color);
    }

    private static float Measure(Font font, string text, float size)
        => Raylib.MeasureTextEx(font, text, size, 0.5f).X;
}
