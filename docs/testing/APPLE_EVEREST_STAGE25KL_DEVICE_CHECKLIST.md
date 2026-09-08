# Stage 25K-L device checks

Status: host gates pass; the new device products and physical checks are pending.
The intended next version is **0.1.1 (46)**. Confirm the version and the supplied
product identity before testing. Build 41 passes belong to K-J and do not carry
over. Run this checklist separately on iPhone and Apple TV; iPad is deliberately
`IPADOS_PHYSICAL_DEFERRED_LEGACY_COMPATIBILITY_POLICY`.

Build45 passed the user's checklists on both devices except the credits page and
fresh lobby spawn. The wide bookmark passed on both. Build46 restores those two
pinned behaviors; repeat the affected checks and the progression checks below.
Also retry **Play Static Mod Map
(Debug): 1-Beginner**: it should load with the same custom terrain and sprites.
Exit debug mode before running the persistent progression checklist. The ten
credits playback markers do not appear as tutorial ghosts during normal lobby
play, matching the original desktop behavior.

## Reference and expected differences

Open **Launch macOS Everest Reference.command** in the repository root. Select
a numbered save, then use **Mod Options → STAGE 25K-L ORIGINAL REFERENCE → Play
unchanged Beginner lobby**. The `kl` console command opens the same original
lobby with its authored intro. This isolated reference has the complete pinned
SJ installation; its 128 original maps avoid missing-destination artifacts.
The Apple build deliberately installs only the lobby and Bing, so other map
entrances must not open or accidentally launch a vanilla chapter.

The passive tutorial should say **Gym / Tech Tutorial**, with a right pointer
and no bird. There is no tutorial to enter. The real credits NPC should use its
original credits and plain portrait-free presentation. The **Stage 25K-H**
diagnostic separately shows **STAGE 25K-H NPC TALK PASS**, animated Madeline and
the ornate purple/brown frame. The reference contains both original SJ content
and the retained K-J/E/F/H diagnostic rooms.

## 1. Normal entry and lobby

1. Select a numbered save in the Apple app. Open **EVEREST / PORT OPTIONS →
   APPLE EVEREST STATIC LAB → Play Real Collab Lobby: Strawberry Jam Collab**.
   This is the required persistent collab route. The debug map selector and a
   direct Bing launch do not satisfy it.
2. Compare the fresh intro, geometry, collision, foreground/background,
   animated scenery and audio with the reference. The source room is
   `sj2021beginnerlobby`; its first marker is local `(1012,680)`, world `(588,40)`.
   A fresh menu launch must start here, high in the lobby, matching the reference.
   A saved session or return from Bing must keep its existing saved/return position.
   Record any missing sprite, seams, crash, profile error or fallback graphic.
3. Walk through the lobby and sample its moving platforms, grass, water,
   lamps, particles, trigger effects, masks, crystal/pedestal, cave and other
   reachable helper behavior. Compare matching locations on the reference.
4. Inspect the Gym sign near the start (source world `(704,147)`). Talk to the
   credits NPC in the lower-right area (world `(2744,1264)`); finish and skip its
   dialogue on separate attempts, checking that controls return.
5. Activate wooden benches, open the map, pan, zoom, cancel and warp. On touch,
   hold **Grab/climb** while moving the movement pad to pan; the Journal/book
   control changes zoom. Toggle Grab off after panning if using toggle mode.
   A controller can also pan with the right stick. The pinned desktop has a
   known warp-selection indexing defect; for reference panning use the view-only
   map with **Tab** while standing normally and activate benches before warping.
6. Try several other chapter/gym entrances. On Apple they must stay unavailable
   and must never send you to vanilla Area 0. The original 21-heart threshold is
   retained; one installed chapter does not unlock the full Beginner heartside.

## 2. New terrain comparison

Inspect the lower-right mossy stone region near the sixth bench. The bench is
world `(2896,1264)`; the first new 5×5 terrain cell is `(3144,1464)`, about 248
pixels right and 200 down from that bench. The 26 cells occupy two nearby rows.
A screenshot from each platform at the same location is useful.

The host proof checks every cell against pinned Everest. Physical acceptance
still requires the real region to render correctly: no seams, missing textures,
crash or apparent substitution. Two cells 16 pixels apart need different tile
variants despite identical 3×3 neighborhoods. Do not infer visual PASS from the
host count alone.

## 3. Authored Bing panel and gameplay

1. Reach Bing's authored entrance (return point world `(1520,552)`). The panel
   title is **If my 'driveway' almost did you in...**, author **by Bing_Over_Google**,
   with its original medium-difficulty icon and **3 ordinary strawberries**.
   The blue title bookmark and its accent must extend to contain the full long
   title and continue all the way to the right screen edge, matching the reference.
   Check both landscape orientations on iPhone. A short chapter title keeps its
   normal visible left position and full right-edge coverage.
   Enter the Start page. The panel must show **Music: Hyperlife**, **Sticker: phant**,
   **Playtesting: Nano**, **Captain: Bissy**, with reference colors and spacing.
   With a saved session, switching **Start Over / Continue** keeps the same credits
   fixed in place. Close and reopen; text must not duplicate or leak to other panels.
   Check existing collection/completion presentation.
2. Start through that panel. The original first room is **00- intro**, marker
   local `(264,152)`, world `(-56,152)`. Compare its music, scenery and initial
   player state with the reference.
3. Play several rooms with movement, jump, dash, death, respawn, camera and
   transitions. Record the room names reached. The early sequence includes
   **01- Crusher**, **02- Bait N' Switch**, **03- Uberjump**, **04- Head Trauma**
   and **05- Boing**, with a berry side room **02B- a stwawbewwy??**.
4. If practical, collect the return strawberry in **07B- OwO whats this??**,
   entity 1459, local `(336,16)`. Check delayed return, player recovery,
   retry/respawn and progression. If it is too difficult or time-consuming,
   report how far you reached; its deterministic/canary evidence remains separate.
5. Completion is desirable but optional for development GREEN. If you reach
   **09- Fin**, complete Bing and check the return, journal, jam-jar/completion
   presentation and persistence after a cold launch. Report whether the silver
   follower was involved; it is separate from the three ordinary berries.

## 4. Persistence and return

1. After meaningful Bing progress, note the room, checkpoint/respawn, berries,
   deaths and visible state. Pause and use **Save and Quit**.
2. Terminate the app completely, cold launch, select the same numbered save
   and resume. Confirm the same SID, room and authored respawn, coherent time,
   deaths, flags/inventory where visible, helper state and correct music.
3. Use the real **Return to Lobby**, check the authored return point, then
   re-enter through Bing's panel. Check **Continue** preserves the saved run.
   Continuation restores the saved session/respawn, not arbitrary player pixels.
4. Open the Beginner journal. Check Bing's title, author, berry capacity,
   collected state, deaths, time and any completion against the reference.
   Only installed areas have playable Apple progression records.
5. Spot-check prior LittleEpic, Fear, Torremolinos 1/2 and K-A/K-B saved progress
   where available. Their maps may be quarantined when absent from this product;
   records must survive and return with an exactly compatible installation.
6. Use a spare numbered slot and exported backup for Save Manager checks:
   slot isolation, delete/recreate, import/replacement and previous-good recovery.
   Keep your ordinary progress intact. Report any check needing help rather than
   treating it as a pass. The host suites separately check AEVPSV1 durability.

## 5. Regressions and platform lifecycle

Run **Run All Factory Lifecycle Checks** to **73/73**, then the K-H ornate
dialogue/tutorial and representative K-E/K-F effects/audio/Crystalline/Vortex
checks. Use the [K-J checklist](APPLE_EVEREST_STAGE25KJ_DEVICE_CHECKLIST.md) for
the retained shared rooms and Collab interaction sequence, including silver
chapter cards and journal badges. Run the Chrono horn check as well.

On iPhone check touch, Pause/resume, rotation, background/reopen, cold audio
initialization and soft reload. Test a controller only if available. On Apple TV
check Metal launch, controller, Pause/resume, Home/reopen, cold audio, Save Manager
and soft reload. Watch first lobby/Bing load times, stutters and memory-related
termination; no texture/effect quality reduction is intended. The existing device log now reports `texture-usage` at content-ready and each
level load: mounted and decoded mod textures, estimated RGBA bytes and managed
live bytes. These reads do not decode additional textures. We can collect these
counts from the log; they are separate from total GPU/process memory and your
visual performance observations.

## Reporting

Report each device and version separately. Include lobby/J-region comparison,
panel, rooms reached, Save/Quit and cold-resume room, Return to Lobby, re-entry,
journal, 73/73, regressions and platform lifecycle. Say which optional return
berry/completion checks you reached. A freeze or difference needs its last room
or action; screenshots are welcome. An unperformed check remains pending.
