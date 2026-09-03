#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestMossyWall : Entity
{
    private readonly bool left;
    private Vector2 shake;
    private readonly List<Image> moss = new();
    private static readonly Color FullColor = Calc.HexToColor("33C111");
    internal AppleEverestMossyWall(EntityData data, Vector2 offset) : base(data.Position + offset)
    {
        left = data.Bool("left", false);
        Depth = -20000;
        List<Collider> colliders = new();
        string directory = data.Attr("spriteDirectory", "JungleHelper/Moss");
        if (directory != "JungleHelper/Moss" || data.Attr("color1", "7A612D") != "7A612D" ||
            data.Attr("color2", "AABF3D") != "AABF3D" || data.Attr("color3", "33C111") != "33C111")
            throw new InvalidOperationException("moss wall is outside the frozen Beginner semantics");
        Calc.PushRandom();
        for (int y = 0; y < data.Height; y += 8)
        {
            string name = y == 0 ? "moss_top" : y + 16 <= data.Height ? "moss_mid" + Calc.Random.Next(1, 3) : "moss_bottom";
            Image image = new(GFX.Game[directory + "/" + name]) { Position = new Vector2(0f, y), FlipX = !left, Color = FullColor };
            Add(image);
            moss.Add(image);
            colliders.Add(left ? new Hitbox(2f, 8f, 8f, y) : new Hitbox(2f, 8f, -2f, y));
        }
        Calc.PopRandom();
        Collider = new ColliderList(colliders.ToArray());
        Add(new ClimbBlocker(edge: false));
        Add(new StaticMover
        {
            SolidChecker = solid => CollideCheck(solid, Position + (left ? -2f : 2f) * Vector2.UnitX),
            OnMove = amount => Position += amount,
            OnShake = amount => shake += amount,
            OnDisable = () => Collidable = false,
            OnEnable = () => Collidable = true
        });
    }
    public override void Update()
    {
        base.Update();
        // Neither exact map creates a Jungle lantern, enforced lantern skin,
        // or cassette block. GetClosestLanternDistanceTo is float.MaxValue;
        // all segments use the final colour and remain climb blockers.
        Color color = FullColor;
        if (!Collidable) { color.R /= 4; color.G /= 4; color.B /= 4; }
        foreach (Image image in moss) image.Color = color;
    }
    public override void Render() { Position += shake; base.Render(); Position -= shake; }
}
