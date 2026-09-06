#nullable disable
using System;
using System.Collections;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

// Frozen composition of Everest stable-1.6458.0 CustomBirdTutorial and
// MaxHelpingHand 1.40.9 CustomTutorialWithNoBird for the exact Beginner-lobby
// profile.  The distributed type removes BirdNPC.Sprite and suppresses the
// StartleAndFlyAway coroutine. Retain Actor updates/tracking and BirdNPC's
// riding and scene-end behavior after removal of its sprite.
internal sealed class AppleEverestCustomTutorialWithNoBird : Actor
{
    private readonly EntityID entityId;
    private readonly string birdId;
    private readonly bool onlyOnce;
    private readonly AppleEverestDirectionalTutorialGui gui;
    private bool triggered;
    private bool flewAway;

    internal AppleEverestCustomTutorialWithNoBird(EntityData data, Vector2 offset, EntityID entityId)
        : base(data.Position + offset)
    {
        this.entityId = entityId;
        birdId = data.Attr("birdId", "");
        onlyOnce = data.Bool("onlyOnce", false);
        bool caw = data.Bool("caw", false);
        bool faceLeft = data.Bool("faceLeft", false);
        bool hasPointer = data.Bool("hasPointer", true);
        string direction = data.Attr("direction", "Down");
        string infoKey = data.Attr("info", "");
        string controls = data.Attr("controls", "");

        // K-H intentionally accepts one exact authored profile.  These checks
        // make a future map variation fail before it can silently inherit this
        // lowering.
        if (birdId != "0" || onlyOnce || caw || faceLeft || !hasPointer || direction != "Right" ||
            infoKey != "SJ2021_lobby_gym_tutorial_info" ||
            controls != "dialog:SJ2021_lobby_gym_tutorial_controls" || data.Nodes.Length != 0)
            throw new InvalidOperationException("CustomTutorialWithNoBird is outside the Stage 25K-H profile");

        Add(new VertexLight(new Vector2(0f, -8f), Color.White, 1f, 8, 32));
        gui = new AppleEverestDirectionalTutorialGui(this, new Vector2(0f, -16f),
            Dialog.Clean(infoKey), Dialog.Clean(controls.Substring("dialog:".Length)), direction);
    }

    public override void Awake(Scene scene)
    {
        base.Awake(scene);
        TriggerShowTutorial();
        AppleEverestStaticRuntime.Log("stage25kh-tutorial=PASS activation=immediate bird=absent pointer=Right bird-id=" + birdId);
    }

    public override bool IsRiding(Solid solid) =>
        Scene.CollideCheck(new Rectangle((int)X - 4, (int)Y, 8, 2), solid);

    public override void SceneEnd(Scene scene)
    { Engine.TimeRate = 1f; base.SceneEnd(scene); }

    internal void TriggerShowTutorial()
    {
        if (triggered) return;
        triggered = true;
        Add(new Coroutine(ShowTutorial()));
    }

    internal void TriggerHideTutorial()
    {
        if (flewAway) return;
        flewAway = true;
        if (triggered) Add(new Coroutine(HideTutorial()));
        triggered = true;
        // MaxHelpingHand's typed BirdNPC hook returns without invoking orig for
        // this entity.  The flattened equivalent therefore performs no startle,
        // sound, flight, flag or removal work here.
        if (onlyOnce && Scene is Level level) level.Session.DoNotLoad.Add(entityId);
    }

    private IEnumerator ShowTutorial()
    {
        gui.Open = true;
        Scene.Add(gui);
        while (gui.Scale < 1f) yield return null;
    }

    private IEnumerator HideTutorial()
    {
        gui.Open = false;
        while (gui.Scale > 0f) yield return null;
        Scene.Remove(gui);
    }
}

// Typed copy of the selected BirdTutorialGui surface.  It deliberately owns
// pointer direction as data, replacing the distributed IL manipulator and its
// five private-field reflection operands with ordinary fields.
internal sealed class AppleEverestDirectionalTutorialGui : Entity
{
    private readonly Entity owner;
    private readonly object info;
    private readonly string control;
    private readonly string direction;
    private readonly Color background = Calc.HexToColor("061526");
    private readonly Color line = Color.White;
    private readonly Color text = Calc.HexToColor("6179e2");
    private float controlsWidth;
    private readonly float infoWidth;
    private readonly float infoHeight;

    internal bool Open;
    internal float Scale;

    internal AppleEverestDirectionalTutorialGui(Entity owner, Vector2 position, object info,
        string control, string direction)
    {
        AddTag(Tags.HUD);
        this.owner = owner;
        Position = position;
        this.info = info;
        this.control = control;
        this.direction = direction;
        infoWidth = info is string value ? ActiveFont.Measure(value).X : ((MTexture)info).Width;
        infoHeight = info is string ? ActiveFont.LineHeight : ((MTexture)info).Height;
        // Everest adds two pixels for a first text control immediately after
        // construction. BirdTutorialGui.Update recomputes the ordinary width on
        // the first frame; preserve that exact lifecycle.
        controlsWidth = ActiveFont.Measure(control).X + 2f;
    }

    public override void Update()
    {
        controlsWidth = ActiveFont.Measure(control).X;
        Scale = Calc.Approach(Scale, Open ? 1f : 0f, Engine.RawDeltaTime * 8f);
        base.Update();
    }

    public override void Render()
    {
        Level level = Scene as Level;
        if (level == null || level.FrozenOrPaused || level.RetryPlayerCorpse != null || Scale <= 0f)
            return;

        Vector2 bubble = owner.Position + Position - level.Camera.Position.Floor();
        if (SaveData.Instance != null && SaveData.Instance.Assists.MirrorMode) bubble.X = 320f - bubble.X;
        bubble *= 6f;
        float lineHeight = ActiveFont.LineHeight;
        float width = (Math.Max(controlsWidth, infoWidth) + 64f) * Scale;
        float height = infoHeight + lineHeight + 32f;
        float left = bubble.X - width / 2f;
        float top = bubble.Y - height - 32f;
        Draw.Rect(left - 6f, top - 6f, width + 12f, height + 12f, line);
        Draw.Rect(left, top, width, height, background);

        // MaxHelpingHand recomputes this position in its injected delegate and
        // does not apply vanilla MirrorMode to it.  Keep that observable detail.
        Vector2 pointer = (owner.Position + Position - level.Camera.Position.Floor()) * 6f;
        float pointerLeft = pointer.X - width / 2f;
        float pointerTop = pointer.Y - height - 32f;
        for (int index = 0; index <= 36; index++)
        {
            float size = (73 - index * 2) * Scale;
            if (direction == "Right")
            {
                Draw.Rect(pointerLeft + width + index, pointerTop + height / 2f - size / 2f,
                    1f, size, line);
                if (size > 12f)
                    Draw.Rect(pointerLeft + width + index, pointerTop + height / 2f - size / 2f + 6f,
                        1f, size - 12f, background);
            }
            else
            {
                // The registered K-H factory rejects every other direction.
                throw new InvalidOperationException("unregistered tutorial pointer direction: " + direction);
            }
        }

        if (width <= 3f) return;
        Vector2 draw = new(bubble.X, top + 16f);
        if (info is string infoText)
            ActiveFont.Draw(infoText, draw, new Vector2(0.5f, 0f), new Vector2(Scale, 1f), text);
        else
            ((MTexture)info).DrawJustified(draw, new Vector2(0.5f, 0f), Color.White,
                new Vector2(Scale, 1f));
        draw.Y += infoHeight + lineHeight * 0.5f;
        ActiveFont.Draw(control, draw + new Vector2(1f, 2f), new Vector2(0.5f, 0.5f),
            new Vector2(Scale, 1f), text);
        ActiveFont.Draw(control, draw + new Vector2(1f, -2f), new Vector2(0.5f, 0.5f),
            new Vector2(Scale, 1f), Color.White);
    }
}

// Frozen composition of Everest CustomNPC and MaxHelpingHand MoreCustomNPC for
// the one selected Beginner-lobby EntityData profile.  All potentially dynamic
// Max branches are rejected in the constructor; the selected ordinary NPC talk
// lifecycle is represented directly and needs no FieldInfo or ILHook.
internal sealed class AppleEverestMoreCustomNpc : NPC
{
    private readonly EntityID entityId;
    private readonly string dialog;
    private readonly Rectangle talkerZone;
    private Coroutine talkRoutine;

    internal AppleEverestMoreCustomNpc(EntityData data, Vector2 offset, EntityID entityId)
        : base(data.Position + offset)
    {
        this.entityId = entityId;
        dialog = data.Attr("dialogId", "");
        if (dialog != "StrawberryJam2021_0_Lobbies_1_Beginner_Credits" ||
            data.Attr("sprite", "") != "" || data.Float("spriteRate", 1f) != 1f ||
            data.Bool("onlyOnce", true) || data.Bool("endLevel", false) ||
            data.Float("indicatorOffsetX", 0f) != 0f || data.Float("indicatorOffsetY", 0f) != -40f ||
            data.Bool("approachWhenTalking", false) || data.Int("approachDistance", 16) != 16 ||
            data.Bool("flipX", false) || data.Bool("flipY", false) ||
            data.Attr("frames", "") != "" || data.Attr("spriteName", "") != "" ||
            data.Attr("onlyIfFlag", "") != "" || data.Attr("setFlag", "") != "" ||
            data.Bool("onlyIfFlagInverted", false) || data.Bool("setFlagInverted", false) ||
            data.Bool("autoSkipEnabled", false) || data.Attr("customFont", "") != "" ||
            data.Nodes.Length != 2)
            throw new InvalidOperationException("MoreCustomNPC is outside the Stage 25K-H profile");

        Vector2[] nodes = data.NodesOffset(offset);
        float top = Math.Min(nodes[0].Y, nodes[1].Y);
        float bottom = Math.Max(nodes[0].Y, nodes[1].Y) + 8f;
        float left = Math.Min(nodes[0].X, nodes[1].X);
        float right = Math.Max(nodes[0].X, nodes[1].X) + 8f;
        talkerZone = new Rectangle((int)(left - Position.X), (int)(top - Position.Y),
            (int)(right - left), (int)(bottom - top));
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        if (Session.GetFlag("DoNotTalk" + entityId)) return;
        Add(Talker = new TalkComponent(talkerZone, new Vector2(-0.5f, -40f), OnTalk));
        AppleEverestStaticRuntime.Log("stage25kh-npc=PASS sprite=none dialog=repeatable talk-zone=nodes");
    }

    private void OnTalk(Player player)
    {
        player.StateMachine.State = Player.StDummy;
        Level.StartCutscene(OnTalkEnd);
        Add(talkRoutine = new Coroutine(Talk()));
        AppleEverestStaticRuntime.Log("stage25kh-npc-talk=start dialog=" + dialog);
    }

    private IEnumerator Talk()
    {
        // The distributed MoreCustomNPC ILHook returns both values unchanged
        // because autoSkipEnabled=false and customFont="" in this profile.
        yield return Textbox.Say(dialog, null);
        Level.EndCutscene();
        OnTalkEnd(Level);
    }

    private void OnTalkEnd(Level level)
    {
        Player player = Scene.Tracker.GetEntity<Player>();
        if (player != null)
        {
            player.StateMachine.Locked = false;
            player.StateMachine.State = Player.StNormal;
        }
        if (talkRoutine != null)
        {
            talkRoutine.Cancel();
            talkRoutine.RemoveSelf();
        }
        Session.IncrementCounter(entityId + "DialogCounter");
        // onlyOnce=false and one dialog reset the counter after each talk.
        Session.SetCounter(entityId + "DialogCounter", 0);
        AppleEverestStaticRuntime.Log("stage25kh-npc-talk=end repeatable=true counter=0");
    }

    public override void Render()
    {
        // The exact selected profile has neither texture frames nor spriteName;
        // Everest CustomNPC and Max MoreCustomNPC therefore draw no NPC sprite.
    }
}
