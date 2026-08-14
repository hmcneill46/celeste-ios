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

Diagnostic-only stages whose evidence was intentionally kept in ignored local
build directories are not reconstructed here. The index covers every tracked
stage report preserved in this repository.
