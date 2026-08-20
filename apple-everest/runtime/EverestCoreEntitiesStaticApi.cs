// Adapted from Everest's CustomCoreMessage implementation at the Stage 25 pin.
//
// The MIT License (MIT)
// Copyright (c) 2018 Everest Team
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
// The static Apple runtime registers this core entity explicitly because there
// is no runtime assembly scan under full AOT.
using System;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Entities;

public sealed class CustomCoreMessage : Entity
{
    private readonly string text;
    private readonly bool outline;
    private float alpha;

    public CustomCoreMessage(EntityData data, Vector2 offset)
        : base(data.Position + offset)
    {
        Tag = Tags.HUD;
        text = Dialog.Clean(data.Attr("dialog", "app_ending"))
            .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)[data.Int("line", 0)];
        outline = data.Bool("outline");
    }

    public override void Update()
    {
        Player player = Scene.Tracker.GetEntity<Player>();
        if (player != null)
            alpha = Ease.CubeInOut(Calc.ClampedMap(Math.Abs(X - player.X), 0f, 128f, 1f, 0f));
        base.Update();
    }

    public override void Render()
    {
        Vector2 camera = ((Level)Scene).Camera.Position;
        Vector2 centre = camera + new Vector2(160f, 90f);
        Vector2 position = (Position - camera + (Position - centre) * 0.2f) * 6f;
        if (SaveData.Instance != null && SaveData.Instance.Assists.MirrorMode)
            position.X = 1920f - position.X;
        if (outline)
            ActiveFont.DrawOutline(text, position, new Vector2(0.5f, 0.5f), Vector2.One * 1.25f,
                Color.White * alpha, 2f, Color.Black * alpha);
        else
            ActiveFont.Draw(text, position, new Vector2(0.5f, 0.5f), Vector2.One * 1.25f,
                Color.White * alpha);
    }
}
