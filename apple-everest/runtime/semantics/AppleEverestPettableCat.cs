#nullable disable
// Selected static implementation reviewed against HonlyHelper 1.7.5.
// Package identity, authored guard and linked target patches are independently bound.
using System.Collections;
using Microsoft.Xna.Framework;
using Monocle;


namespace Celeste.Mod;

internal sealed class AppleEverestPettableCat : NPC
{
    private readonly Sprite CatSprite;

    private readonly Sprite ThePetterSprite;

    private readonly string catFlag;

    private Coroutine pettingRoutine;

    private Vector2 CatAnchor;

    public AppleEverestPettableCat(Vector2 position, string CatFlag)
        : base(position)
    {
        CatSprite = new Sprite(GFX.Game, "characters/HonlyHelper/pettableCat/");
        CatSprite.AddLoop("idle", "spoons_idle", 0.15f);
        CatSprite.Add("pet", "spoons_pet", 0.15f);
        CatSprite.Play("idle");
        Add(CatSprite);
        ThePetterSprite = AppleEverestStaticRuntime.CreateStaticModSprite("HonlyHelper_CatPetter");
        catFlag = CatFlag ?? "CatHasBeenPet";
    }

    public AppleEverestPettableCat(EntityData data, Vector2 offset)
        : this(data.Position + offset, data.Attr("catFlag"))
    {
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        Add(Talker = new TalkComponent(new Rectangle(-30, 0, 64, 8), new Vector2(2f, -4f), OnPetting));
        CatAnchor = CatSprite.Position;
    }

    private void OnPetting(Player player)
    {
        Level.StartCutscene(OnPettingEnd);
        Add(pettingRoutine = new Coroutine(ThePetting(player)));
    }

    private IEnumerator ThePetting(Player player)
    {
        yield return PlayerApproachLeftSide(player, turnToFace: true, 6f);
        CatSprite.Play("pet");
        CatSprite.Position = CatAnchor + new Vector2(-4f, -8f);
        PlayerSprite sprite = player.Sprite;
        PlayerHair hair = player.Hair;
        bool visible = false;
        hair.Visible = false;
        sprite.Visible = visible;
        ThePetterSprite.Play("pet", restart: true);
        Add(ThePetterSprite);
        ThePetterSprite.Position = CatAnchor + new Vector2(-22f, -24f);
        Audio.Play("event:/HonlyHelper/catsfx", base.Center);
        yield return 2f;
        Level.EndCutscene();
        OnPettingEnd(Level);
        CatSprite.Play("idle");
        CatSprite.Position = CatAnchor;
        SceneAs<Level>().Session.SetFlag(catFlag);
    }

    private void OnPettingEnd(Level level)
    {
        Player entity = base.Scene.Tracker.GetEntity<Player>();
        if (entity != null)
        {
            entity.Sprite.Visible = (entity.Hair.Visible = true);
            entity.StateMachine.Locked = false;
            entity.StateMachine.State = 0;
        }
        pettingRoutine.Cancel();
        pettingRoutine.RemoveSelf();
        CatSprite.Play("idle");
        CatSprite.Position = CatAnchor;
        ThePetterSprite.RemoveSelf();
    }
}
