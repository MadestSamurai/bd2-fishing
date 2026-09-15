# Third-party notices

BD2 Fishing project code is MIT licensed. Included libraries retain their respective licenses:

| Component | Version / origin | License |
| --- | --- | --- |
| SharpMonoInjector | Biney, vendored source | MIT, `licenses/SharpMonoInjector-MIT.txt` |
| Harmony / Lib.Harmony | 2.4.2, Andreas Pardeike | MIT, `licenses/Harmony-MIT.txt` |
| Mono.Cecil | 0.11.6, Jb Evain et al. | MIT, `licenses/Mono.Cecil-MIT.txt` |
| Roslyn | Microsoft.CodeAnalysis 4.14.0, .NET Foundation | MIT, `licenses/Roslyn-MIT.txt`, additional notices in `licenses/Roslyn-ThirdPartyNotices.rtf` |
| .NET runtime | Self-contained runtime selected by pinned NuGet resolution | MIT and included third-party notices, `licenses/dotnet-MIT.txt`, `licenses/dotnet-ThirdPartyNotices.txt` |

Transitive managed dependency versions are recorded in `packages.lock.json` files. Game assemblies and resources are not included. The interface contract contains symbol descriptions and one-way fingerprints, not game method bodies.
