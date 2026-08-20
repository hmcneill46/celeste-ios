# Apple Everest host-tool third-party notices

The experimental Apple static-Everest builder obtains or references the
following open-source projects on the build host. Stock desktop runtimes are
not shipped in the Apple canary products. The one narrow exception is the
tracked MIT-licensed adaptation of Everest's `CustomCoreMessage`, called out
below; generated proprietary Celeste source remains ignored.

| Project | Pinned use | License / notice |
| --- | --- | --- |
| [Everest](https://github.com/EverestAPI/Everest) | Reference implementation, exact `stable-1.6458.0` source pin, and narrow tracked `CustomCoreMessage` adaptation | MIT; copyright Everest Team. The upstream `LICENSE` applies. |
| [MonoMod](https://github.com/MonoMod/MonoMod) | Exact host-only IL-freeze/reference pin | MIT; copyright 0x0ade and contributors. The upstream `LICENSE` applies. |
| [YamlDotNet](https://github.com/aaubry/YamlDotNet) | NuGet 16.1.3 in `AppleEverestBuilder` | MIT. The upstream license applies. |
| [Mono.Cecil](https://github.com/jbevain/cecil) | NuGet 0.11.6 in `AppleEverestBuilder` | MIT. The upstream license applies. |
| [Particle Palette Helper](https://github.com/KnowHT1515/ParticlePaletteHelper) | Ignored Stage 25C release fixture and source audit | MIT; copyright KnowHT. No third-party bytes are tracked or redistributed. |
| [Feather Maddy](https://github.com/Cubical22/Feather-Maddy) | Ignored Stage 25D release fixture and source audit | No explicit redistribution license was located. Its ZIP, DLL, and source are not tracked or redistributed. |
| [Lag Pauser](https://github.com/samgcode/LagPauser) | Ignored Stage 25D direct-managed-hook fixture and source audit | No explicit redistribution license was located. Its ZIP, DLL, and source are not tracked or redistributed. |
| [Cpop Helper](https://github.com/Wojowu/CpopHelper) | Ignored Stage 25E helper release fixture and source audit at `f8b70384…cb2` | No SPDX license file was found. The upstream README grants reuse with credit and at own risk. Its ZIP, DLL, content, and source are not tracked or redistributed. |
| QuizSample | Ignored Stage 25E dependent-map release fixture | No source repository or explicit redistribution license was located. Its ZIP, map, dialog, and other content are not tracked or redistributed. |
| [Dashless Dream Blocks](https://github.com/coloursofnoise/DashlessDreamBlocks) | Ignored Stage 25C deferred direct-hook fixture | MIT; copyright coloursofnoise. No third-party bytes are tracked or redistributed. |
| [GoldenTrainer](https://github.com/Paloys/GoldenTrainer) | Ignored Stage 25C distributed `IL.*`/direct-`ILHook` audit fixture | No explicit redistribution license was located. Its ZIP, DLL, and source are not tracked or redistributed. |
| [Trailine](https://github.com/WEGFan/Celeste-Trailine) | Ignored Stage 25C direct-hook audit fixture | MIT; copyright WEGFan. No third-party bytes are tracked or redistributed. |

NLua commit `b3524288712743fb2394dcf615d14d0dac3276e2` is retained only as
Everest dependency provenance. NLua and KeraLua are rejected by the
compatibility analyser and are absent from the device runtime.

The project-owned canary source and static lowering/runtime code were written
for this repository. No celeste-wasm source is copied; that project was used
only as architectural evidence during the earlier feasibility diagnosis.

Celeste, its assets, and FMOD remain user-supplied proprietary inputs and are
not covered by these open-source notices or committed to this repository.
