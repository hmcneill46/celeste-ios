# Stage 13C — repository documentation cleanup

Stage 13C starts from `6d1a9a16f378250c66fef362e78b4d8f6ddff185`.

This documentation-only change moved all 18 completed stage reports from the
repository root to `docs/history/stages/` and moved the superseded original
port plan to `docs/history/`. It added current and historical documentation
indexes, corrected current links and the stale pre-soft-reload wording, and
removed presentation-only report paths from old foundation verifiers.

The final tracked root retains `README.md` as the public landing page and
`CONTRIBUTING.md` as the standard contribution guide. No completed Stage report
remains there. Current guidance is under `docs/`; the chronological history is
discoverable through `docs/history/README.md` without dominating the landing
page.

Validation passed for 55 repository-local Markdown links, shell/Python syntax
for changed verifiers, builder help and host doctor, repository/privacy checks,
and the quick Stage 9B/10B/11/12B/13B verifier chain. A clean candidate clone
initialized every recursive submodule, presented the new hierarchy, passed the
same documentation/repository gates, and remained clean. No runtime,
persistence, networking, controller, audio, native, generated-game, or package
behaviour changes in this stage.
