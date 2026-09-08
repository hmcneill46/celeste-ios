using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

// CollabUtils2's selected text-only chapter credits profile. The build gate
// rejects credit tags on selected panels until that separate profile is closed.
internal sealed class AppleEverestCollabChapterCredits
{
    private FancyText.Text text;

    internal static bool HasCredits(AreaKey area) => Dialog.Has(AreaData.Get(area).Name + "_collabcredits");

    internal void Prepare(AreaKey area)
    {
        Clear();
        string name = AreaData.Get(area).Name;
        if (Dialog.Has(name + "_collabcreditstags"))
            throw new InvalidOperationException("Selected chapter credit tags require a closed static profile: " + name);
        if (HasCredits(area))
            text = FancyText.Parse(Dialog.Get(name + "_collabcredits").Replace("{break}", "{n}"),
                int.MaxValue, int.MaxValue, 1f, Color.Black);
    }

    internal void Clear() => text = null;

    internal void Draw(Vector2 center, int checkpointIndex, float panelHeight)
    {
        if (text == null || checkpointIndex > 0) return;
        float alpha = Calc.ClampedMap(panelHeight, 600f, 730f, 0f, 1f);
        float width = text.WidestLine();
        float height = text.Font.Get(text.BaseSize).LineHeight * (text.Nodes.OfType<FancyText.NewLine>().Count() + 1);
        // The pinned renderer reserves one 52-pixel tag row even when its
        // non-null tag list is empty. Preserve that visible text placement.
        const float tagsHeight = 52f;
        float scale = Math.Min(1f, Math.Min((410f - tagsHeight) / height, 800f / width));
        text.DrawJustifyPerLine(center + new Vector2(0f, 40f - tagsHeight / 2f), Vector2.One * 0.5f,
            Vector2.One * scale, 0.8f * alpha, 0, int.MaxValue);
    }
}
