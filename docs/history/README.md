# tvOS port engineering history

These documents record the development, reproducibility checks, failure
analysis, and physical acceptance of the Celeste tvOS port. They are retained
as an engineering audit trail, but describe historical stages rather than the
current user workflow.

To build or install Celeste, start with the root [README](../../README.md) and
the current [building guide](../BUILDING.md). The implemented architecture,
support matrix, and limitations are in [project status](../STATUS.md).

## Original plan

- [Initial feasibility audit and implementation plan](TVOS_PORT_PLAN.md) — the
  pre-implementation assessment; its provisional conclusions are historical.

## Chronological stage records

1. [Stage 1 — native tvOS dependency pipeline](stages/TVOS_NATIVE_BUILD_REPORT.md)
2. [Stage 2 — modern .NET/FNA tvOS host](stages/TVOS_HOST_STAGE2_REPORT.md)
3. [Stage 3A — managed Celeste regeneration and retarget](stages/TVOS_CELESTE_MANAGED_STAGE3A_REPORT.md)
4. [Stage 3B — first real Celeste frame](stages/TVOS_CELESTE_RUNTIME_STAGE3B_REPORT.md)
5. [Stage 3C — Prologue, temporary serialization, and haptics](stages/TVOS_CELESTE_PROLOGUE_STAGE3C_REPORT.md)
6. [Stage 5A — focused FMOD diagnostic](stages/TVOS_FMOD_DIAGNOSTIC_STAGE5A_REPORT.md)
7. [Stage 5B — normal gameplay audio](stages/TVOS_CELESTE_AUDIO_STAGE5B_REPORT.md)
8. [Stage 6 — durable UserDefaults persistence](stages/TVOS_CELESTE_PERSISTENCE_STAGE6_REPORT.md)
9. [Stage 8A — self-builder and local branding](stages/TVOS_SELF_BUILD_STAGE8A_REPORT.md)
10. [Stage 8B — public self-build repository release](stages/TVOS_REPOSITORY_RELEASE_STAGE8B_REPORT.md)
11. [Stage 8C — public prerequisite and failure UX](stages/TVOS_PUBLIC_PREREQUISITES_STAGE8C_REPORT.md)
12. [Stage 8D — locale-independent symbol validation](stages/TVOS_LOCALE_REPRODUCIBILITY_STAGE8D_REPORT.md)
13. [Stage 9B — compressed persistence](stages/TVOS_COMPRESSED_PERSISTENCE_STAGE9B_REPORT.md)
14. [Stage 10A — read-only local-network Save Manager](stages/TVOS_READONLY_SAVE_MANAGER_STAGE10A_REPORT.md)
15. [Stage 10B — validated Save Manager mutations](stages/TVOS_WRITABLE_SAVE_MANAGER_STAGE10B_REPORT.md)
16. [Stage 11 — selectable controller prompts](stages/TVOS_CONTROLLER_PROMPTS_STAGE11_REPORT.md)
17. [Stage 12B — graceful main-menu Quit](stages/TVOS_GRACEFUL_QUIT_STAGE12B_REPORT.md)
18. [Stage 13B — verified high-level soft reload](stages/TVOS_SOFT_RELOAD_STAGE13B_REPORT.md)
19. [Stage 13C — repository documentation cleanup](stages/TVOS_REPOSITORY_CLEANUP_STAGE13C_REPORT.md)
20. [Stage 14 — integrated release-candidate validation](stages/TVOS_RELEASE_CANDIDATE_STAGE14_REPORT.md)
21. [Stage 15 — one-time Save Manager QR pairing](stages/TVOS_SAVE_MANAGER_QR_STAGE15_REPORT.md)
22. [Stage 16B — native Metal Performance HUD toggle](stages/TVOS_METAL_PERFORMANCE_HUD_STAGE16B_REPORT.md)
23. [Stage 17B — production multi-distribution FNA input support](stages/TVOS_CELESTE_INPUT_COMPAT_STAGE17B_REPORT.md)
24. [Stage 17C — Steam Windows FNA input and acquisition guides](stages/TVOS_CELESTE_STEAM_WINDOWS_STAGE17C_REPORT.md)
25. [Stage 18B — timed builder progress, heartbeat, and CI-friendly output](stages/TVOS_BUILDER_UX_STAGE18B_REPORT.md)
26. [Stage 18C — production private GitHub Actions cloud builder](stages/TVOS_GITHUB_ACTIONS_CLOUD_BUILDER_STAGE18C_REPORT.md)
27. [Stage 19 — semantic production naming cleanup](stages/TVOS_SEMANTIC_PRODUCTION_NAMES_STAGE19_REPORT.md)
28. [Stage 20 — v1.0.0-rc.2 integrated release-candidate acceptance](stages/TVOS_RC2_INTEGRATED_ACCEPTANCE_STAGE20_REPORT.md)
29. [Stage 21 — beginner-friendly project presentation and documentation](stages/TVOS_BEGINNER_PROJECT_EXPERIENCE_STAGE21_REPORT.md)
30. [Stage 22B — stable-address Save Manager browser continuity](stages/TVOS_SAVE_MANAGER_CONTINUITY_STAGE22B_REPORT.md)
31. [Stage 24B — modern iOS native/FNA foundation](stages/IOS_MODERN_FOUNDATION_STAGE24B_REPORT.md)
32. [Stage 24C1 — canonical Celeste first frame and real FMOD on modern iOS](stages/IOS_CELESTE_FIRST_FRAME_STAGE24C1_REPORT.md)
33. [Stage 24C2 — controller-first iOS gameplay, persistence, and lifecycle](stages/IOS_CONTROLLER_GAMEPLAY_PERSISTENCE_STAGE24C2_REPORT.md)
34. [Stage 24D2 — production touch controls and iOS input UX](stages/IOS_TOUCH_CONTROLS_STAGE24D2_REPORT.md)
35. [Stage 24D3 — custom touch layouts and per-input Grab behaviour](stages/IOS_CUSTOM_TOUCH_LAYOUT_STAGE24D3_REPORT.md)
36. [Stage 24E1 — Files-native data portability and touch-layout sharing](stages/IOS_FILES_DATA_PORTABILITY_STAGE24E1_REPORT.md)
37. [Stage 24E2 — beginner iOS self-builder and integrated release acceptance](stages/IOS_RELEASE_ACCEPTANCE_STAGE24E2_REPORT.md)
38. [Stage 25B — production shared Apple Everest static-AOT foundation](stages/APPLE_EVEREST_STATIC_FOUNDATION_STAGE25B_REPORT.md)
39. [Stage 25C — real Everest ZIP compatibility ladder I](stages/APPLE_EVEREST_REAL_MODS_STAGE25C_REPORT.md)
40. [Stage 25D-C — scalable HookGen and direct managed-detour compatibility](stages/APPLE_EVEREST_MANAGED_DETOURS_STAGE25D_REPORT.md)
41. [Stage 25E — real helper dependency ecosystem and module settings](stages/APPLE_EVEREST_HELPER_ECOSYSTEM_STAGE25E_REPORT.md)
42. [Stage 25F-A — real module SaveData and Session durability](stages/APPLE_EVEREST_MODULE_DURABILITY_STAGE25F_REPORT.md)
43. [Stage 25F-B — bounded static MonoMod ModInterop compatibility](stages/APPLE_EVEREST_MODINTEROP_STAGE25FB_REPORT.md)
44. [Stage 25F-B2 — real ModInterop pair and bounded HookGen completion](stages/APPLE_EVEREST_MODINTEROP_STAGE25FB2_REPORT.md)
45. [Stage 25G — real multi-helper map composition audit](stages/APPLE_EVEREST_MULTI_HELPER_STAGE25G_REPORT.md)
46. [Stage 25H-A — deterministic build-time HookGen IL freeze](stages/APPLE_EVEREST_STATIC_IL_STAGE25H_REPORT.md)
47. [Stage 25H-B — composed frozen IL and compiler-singleton delegates](stages/APPLE_EVEREST_STATIC_IL_COMPOSITION_STAGE25HB_REPORT.md)
48. [Stage 25H-C — graph-driven IL closure and ordinary event breadth](stages/APPLE_EVEREST_GRAPH_IL_CLOSURE_STAGE25HC_REPORT.md)
49. [Stage 25H-D — bounded static direct ILHook freeze](stages/APPLE_EVEREST_DIRECT_ILHOOK_STAGE25HD_REPORT.md)
50. [Stage 25I-A — LittleEpic graph completion and custom-audio boundary](stages/APPLE_EVEREST_LITTLEEPIC_STAGE25IA_REPORT.md)
51. [Stage 25I-B — bounded custom FMOD and first real multi-helper map](stages/APPLE_EVEREST_CUSTOM_FMOD_LITTLEEPIC_STAGE25IB_REPORT.md)
52. [Stage 25J-A — durable custom-map and LevelSet progression](stages/APPLE_EVEREST_LEVELSET_PROGRESSION_STAGE25JA_REPORT.md)
53. [Stage 25J-B — second real map and multi-map progression](stages/APPLE_EVEREST_SECOND_REAL_MAP_STAGE25JB_REPORT.md)
54. [Stage 25J-C — first real multi-map LevelSet](stages/APPLE_EVEREST_REAL_LEVELSET_STAGE25JC_REPORT.md)
55. [Stage 25K-A — first real CollabUtils2 lobby/collab](stages/APPLE_EVEREST_FIRST_REAL_COLLAB_STAGE25KA_REPORT.md)
56. [Stage 25K-B — second real collab and broader CollabUtils2 completion](stages/APPLE_EVEREST_SECOND_REAL_COLLAB_STAGE25KB_REPORT.md)
57. [Stage 25K-C — Strawberry Jam compatibility audit](stages/APPLE_EVEREST_STRAWBERRY_JAM_AUDIT_STAGE25KC_REPORT.md)
58. [Stage 25K-D — configured ordering and map appendix compatibility](stages/APPLE_EVEREST_CONFIGURED_ORDERING_STAGE25KD_REPORT.md)
59. [Stage 25K-E — Strawberry Jam root/helper semantic lowering](stages/APPLE_EVEREST_SJ_ROOT_HELPER_SEMANTICS_STAGE25KE_REPORT.md)
60. [Stage 25K-F — Strawberry Jam audio and Crystalline/Vortex semantics](stages/APPLE_EVEREST_SJ_AUDIO_CRYSTALLINE_VORTEX_STAGE25KF_REPORT.md)
61. [Stage 25K-H — selected-factory base-entity closure and MaxHelpingHand semantics](stages/APPLE_EVEREST_BASE_ENTITY_MAXHELPINGHAND_STAGE25KH_REPORT.md)
62. [Stage 25K-I — unchanged Strawberry Jam slice retry, stopped at production preflight](stages/APPLE_EVEREST_FIRST_SJ_SLICE_RETRY_STAGE25KI_REPORT.md)

Diagnostic-only stages whose evidence was intentionally kept in ignored local
build directories are not reconstructed here. The index covers every tracked
stage report preserved in this repository.
