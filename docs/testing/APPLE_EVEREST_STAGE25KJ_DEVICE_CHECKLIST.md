# Stage 25K-J device checks

This build tests project-owned factory fixtures. It contains no original Strawberry Jam gameplay map. Build 41 is the replacement K-J candidate; acceptance below remains pending until tested on the exact signed products. Build 38 stopped at MaxMechanics/factory_04 because the return berry's profile guard rejected checkpoint/order values assigned by the map loader. Build 37 failed at the masked star background and exposed map-sprite and cat-placement defects. Build 39 passed the 73/73 sweep on iPhone 12, but exposed dialogue-scope and Collab presentation/input differences; Apple TV was not tested. Earlier builds have no transferable build 41 physical acceptance. Build 40 failed before the main menu because its fixture chapter icon named a missing atlas key; build 41 uses the verified existing house icon and adds an actual-atlas gate before signing.

## Start here

1. Open **Celeste Everest Canary** and confirm the Options version is **0.1.1 (41)**.
2. Open **EVEREST / PORT OPTIONS**, then find **STAGE 25K-J FACTORY CANARIES**.
3. Choose **Run All Factory Lifecycle Checks**. Leave the game running while it visits the rooms. The final message must say **K-J FACTORY LIFECYCLE 73/73**. Report any incomplete message, frozen room, crash or missing texture. The detailed log records actual constructor and lifecycle dispatches; a host registration count alone is insufficient.
4. Use the individual room buttons for the checks below. Their labels combine the group and room, for example **Frost / factory_05**. Return through Pause when finished. These fixtures use an isolated temporary save; ordinary player saves are restored on exit.

The automatic sweep covers initialization for all 73 factories. It does not prove their interactions, animations, collision effects or cleanup. The following grouped play checks cover those behaviors.

## Shared rooms

| Button | What to do and expect |
|---|---|
| **CrystalCave / canary** | Walk through the blue dashed AssistRect: it is a translucent guide and does not collide. Stand on the narrow grass tile between matching grass neighbors and inspect its animated grass. Pick up/drop the crab (the map's intended crystal reskin), carry it into the cave and place it in the crab pedestal. Expect attraction, filling and confetti. Enter/leave the cave to see its fade. Stand beside the cat on the floor and press Talk (Up on the default keyboard/controller binding): hear the cat sound, see the petting animation, then regain ordinary Madeline, hair and controls. Retry inside the cave and confirm normal recovery. |
| **WaterGarden / canary** | Enter and jump out of the blue pool; check swimming, splash and moving surface. The small waterfall meets the water with ripples, splash and sound. The larger muted waterfall draws behind gameplay. Jump into the lamp to make it swing; inspect its sound and moving light. The two green spinners have a connector. Touch a spinner, die and respawn. |
| **CameraCorridor / canary** | Walk right off the west platform over the invisible slope, then walk back. Its invisible appearance is authored. Hold Down while travelling right to check the slide pose. Climb the steps and move vertically through the camera region near the middle; its anchor should change smoothly and clear on exit. Reach the east spring and return. The temporary status message reports each spring's actual Active/Visible/Collidable flags: west starts `1/1/1`, east `0/0/0`; the states reverse at the east end and recover on return. Component flags are also recorded in the log. |

## Max, Lunatic and Frost

| Button | What to do and expect |
|---|---|
| **MaxMechanics / factory_05** | Approach the tutorial and jump onto its zip mover. The tutorial actor and its interface must follow the moving platform. The passive sign reads **Gym / Tech Tutorial**, with a right pointer and no bird. There is no tutorial interaction to enter; movement remains available. |
| **MaxMechanics / factory_06** | Walk through the flag-controlled wall from left to right, then back. The opening/closing follows the surrounding flag regions. Test its solid side and verify it does not appear through the player while occupied. |
| **MaxMechanics / factory_07** | Approach the grouped spikes, trigger them, retreat and retry. Check their delay, grouped activation and normal death/respawn. |
| **MaxMechanics / factory_08** | Jump onto the NPC's platform and talk. Finish, repeat and skip the dialogue. Check the original Beginner lobby credits, plain black/white default dialogue frame and recovery of player control. This original credits dialogue has no portrait. The separate K-H regression below checks the animated portrait and short PASS text. |
| **MaxMechanics / factory_12** | Test both vertical one-way platforms from each side: walking, jumping, climbing, wall jumping and dashing. One face blocks and the other permits passage. Use the feather and hit a blocking face to exercise the composed horizontal-collision behavior. |
| **MaxMechanics / factory_10** | Compare the Rainbow spinner inside the selected color region with the one outside. Check their color behavior, then leave/reload to check cleanup. |
| **MaxMechanics / factory_14** | Walk right through and beyond the wide camera-catchup region. Its camera response changes inside and reverts outside. |
| **MaxMechanics / factory_15** | Walk left, through and right of the small camera border on the raised walkway. Check the spatial camera restriction and recovery. |
| **MaxMechanics / factory_16** | Cross the color-grade region in both directions and inspect the gradual change. |
| **MaxMechanics / factory_17–19** | Traverse each camera trigger and its surrounding flag regions in both directions. Factory 17 controls a vertical offset, 18 a vertical target, and 19 a horizontal interpolation. The raised floors in 17/18 keep the effects visible instead of clamping them away. |
| **MaxMechanics / factory_01, 02, 09, 13** | Wait at least two seconds near spawn, walk right past x352, wait two seconds, then return. Check dust/heat and the background visibility/fade controls. A half-second automatic visit is too short to judge the one-second fade. |
| **MaxMechanics / factory_03** | In the darkened room, inspect the light against the nearby wall as you approach and leave. |
| **MaxMechanics / factory_04** | Touch the return strawberry and check its delayed return flight, player recovery and behavior after retry. |
| **Frost / factory_01** | Approach/touch the fire barrier and check its appearance, collision/death and respawn. |
| **Frost / factory_02** | Walk near the bird; it should react and fly away. |
| **Frost / factory_03** | Inspect the foreground grass decal. It remains visible after the container converts it to its renderer. Re-enter the room and check cleanup. |
| **Frost / factory_04** | Watch the tutorial move to its authored node shortly after entry. Its **Gym / Tech Tutorial** sign should visibly follow rather than disappear above the camera. This is a passive sign, with no tutorial to enter. |
| **Frost / factory_05** | Inspect the pair of ice spinners, their connector and outlines. Touch one and verify death/respawn. |
| **Frost / factory_06** | Inspect the full hanging cable and lamps, including the raised endpoint. |
| **Frost / factory_07** | Initialization must complete. The automatic log proves the real ResetVariants callback was dispatched; this room begins at defaults, so it does not claim a visible reset from a changed setting. Use the K-F variant regression below for that check. |

## Atmosphere, base helpers and masks

In **Atmosphere / factory_01–05**, inspect petals, stars, godrays, waterfall and sparse bubbles. Walk from spawn to the right-hand flag regions and return to compare visibility of the flag-controlled stylegrounds. In factory 01, the two wind regions turn rightward wind on and off. In factory 04, compare the muted background waterfall behind the player with the bright foreground waterfall in front, its loop sound and displacement. Both waterfalls remain visible when unrelated styleground flags change. In factory 05, wait about **20 seconds**: particles are intentionally sparse and may not appear during the automatic sweep.

Use the existing **Stage 25K-E** and **Stage 25K-F** canaries for their visible masks, overlays, bloom, custom audio, variants, Crystalline triggers and Vortex platform regression. These are still authored canaries. Check the K-F alternate music/ambience and the later return to ordinary audio; repeated room entry and soft reload must not leave stale or duplicated audio. The Crystalline trigger should drive its paired rumble/flag effects, and the attached Vortex platform should carry the player correctly.

Use the existing **Stage 25K-H** canary for the short tutorial/NPC regression. Trigger the NPC dialogue: it should show the animated Madeline portrait, clean text **STAGE 25K-H NPC TALK PASS**, and the accepted ornate purple/brown frame. Finish and skip it, then confirm normal movement.

The new **Masks** and **BaseHelpers** rooms remain part of the 73/73 initialization sweep. Some original profiles depend on context or flags and have no obvious isolated visual effect. The explicit K-E/K-F/K-H interaction checks provide the corresponding visible regression; do not infer semantic acceptance merely from an empty-looking room.

## Collab interaction fixture

Choose **Play Collab Interaction Fixture** for these checks. The separate **Collab / factory_01–11** rooms contain unchanged original representative profiles for construction checks; their original chapter targets are intentionally absent.

1. In a fresh fixture lobby, confirm the heart door is locked and the rainbow hologram indicates incomplete collection. Activate both benches. Open the lobby map: the title is **FACTORY LOBBY**. Inspect its marker, pan/zoom, cancel and warp between benches. On touch, hold the **Grab/climb** control while moving the movement pad to pan; the Journal/book control changes zoom. A configured toggle Grab must be toggled off to resume destination selection. Controller right stick also pans; Grab plus the movement stick is available. Player, hair, facing and control must recover.
2. Talk to the **Finish A** panel. The fixture explicitly uses the house chapter icon on both devices and reference. Its first run has no silver berry. Dash into the miniheart, wait for completion/confirmation and return. Check the return point and that lobby timing resumes only after control/input. Check the journal: sticker **A** now appears.
3. Replay A, take its silver follower and finish without dying. Return to the lobby. Then complete **Finish B** normally and replay B for its silver. After silver collection the chapter card has its silver skin, and the collected total includes the silver (A: **1/0**, B with red and silver: **2/1**); the journal shows a silver badge in its best-deaths column. This order avoids the reference debug mode resetting a previously collected area's displayed berry list on re-entry.
4. After both normal completions, inspect sticker **B** and the door unlock. After both silver completions, approach the rainbow trigger. Check the combine animation, sound, camera/player recovery and collection. The deliberately missing-target third sticker must never appear.
5. In a separate fresh fixture, test the door's assist confirmation: cancel once, then accept. Separately test cutscene skip so it does not replace the normal unlock check above.
6. Test live continuation in B: collect its ordinary red strawberry, let it finish collecting, then **Save and Return**. Choose **Continue**: that strawberry remains absent. Choose **Start Over** on another attempt: it returns. Also test Discard. A must not offer Save and Return; B must offer it.

Continuation restores the authored respawn/session state, not arbitrary current player coordinates. This isolated fixture deliberately suppresses disk writes. It demonstrates live continuation and return behavior; it does not claim that fixture progress survives a process restart.

## Platform checks and reporting

On iPhone, check movement, jump, dash, death/respawn, Pause, touch, rotation, background/reopen and cold relaunch. Test a controller if available and otherwise report it unavailable.

On Apple TV, check Metal launch, controller/Pause, sound, Home/reopen, cold launch and soft reload. Run the same automatic sweep and grouped behavior checks.

Report each device separately. A useful result includes **73/73**, the shared rooms, Collab sequence, Max/Frost/effects, the K-E/K-F/K-H regressions and platform lifecycle checks. If something is uncertain, report its group/room and what happened; a screenshot is helpful but optional.

iPad physical acceptance remains pending when hardware is unavailable. The universal iOS product still needs device families `[1,2]`, arm64, minimum iOS 15 and full AOT. Stage 25K-B build 35 remains the last all-three-device physical GREEN unless all three device classes pass K-J.

## macOS comparison

The dedicated reference contains all nine K-J provider groups, the additional Sideways room, the three-map Collab fixture and the K-E/K-F/K-H visual regressions: 16 authored maps. It uses 20 exact desktop helper packages. The SJ masks, glow controller and jam jar use the original package's selected desktop IL, with its original hooks; a small generated module initializes those classes. Their method bodies, locals and exception handlers are checked unchanged. No Apple lowering is used as its own reference, and no original SJ gameplay map is mounted.

Run **Open Stage 25K-J Reference.command** in the prepared reference folder, then open **Mod Options → STAGE 25K-J REFERENCE ROOMS** and choose the matching group/room. The console also accepts short commands: `kj CrystalCave`, `kj WaterGarden`, `kj CameraCorridor`, `kj Frost factory_05`, `kj Atmosphere factory_02`, `kj Lobby`, `kj Sideways`, `kj Effects`, `kj Audio`, and `kj Dialogue`. The long `load` commands still work. The reference has separate temporary saves.

CrystalCave intentionally has an animated red crab and the simple crab pedestal, using the map's sprite overrides. Its cat sits on the floor and is reachable with Talk. Both lobby benches use the wooden bench asset. The invisible slope and the tile template fallback are intentional source behavior.

Use **Effects**, **Audio**, and **Dialogue** for the K-E/K-F/K-H comparisons. The K-E Apple module-state marker is omitted on desktop because it inspects the static Apple runtime; all gameplay entities remain, and GlowController receives its explicit empty-list default to avoid the desktop null-values bug. The K-F depth trigger names the reference probe's CLR type, as required by the original helper. The K-F audio and green depth-test rectangle are the same project-owned probes, using the original desktop audio/helper paths. The lifecycle counter, iOS touch/rotation, tvOS platform checks and Apple Save Manager are device checks and have no macOS counterpart. The original-profile Collab construction rooms still point at absent original chapters; use **Lobby** for the playable Collab sequence.

The pinned desktop CollabUtils2 has a destination-index defect: a selected warp can fall outside the visible destination list during panning; the supplied crash resolves to that original indexed access. For a safe panning comparison, open the original view-only map with **Tab** while standing normally; it supports panning without requiring warp destinations. Activate both benches before testing destination selection and warping. Build 40 validates the selected index and normalizes it when the destination list changes, including an empty list. This original reference defect is recorded separately from port acceptance.
