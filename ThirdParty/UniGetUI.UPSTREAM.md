# UniGetUI upstream snapshot

WinForge vendors the complete tracked source tree of [Devolutions/UniGetUI](https://github.com/Devolutions/UniGetUI) under `ThirdParty/UniGetUI`.

- Upstream commit: `21116375c8299d1db38a3c3b4c2eb7e18bc97c4e`
- Upstream branch: `main`
- Snapshot date: 2026-07-10
- License: MIT; the original copyright and permission notice is preserved at `ThirdParty/UniGetUI/LICENSE`.

The snapshot includes the upstream application, package-engine projects, tests, documentation, translations, build scripts, installer material, and assets. It is intentionally excluded from WinForge's SDK item globs. WinForge does not launch or embed the upstream executable: compatible behaviour is ported into the native WinUI 3 Package Manager and its managed services so the feature remains integrated, bilingual, and consistent with WinForge's architecture.

The upstream tree also contains third-party files and binary prerequisites with their own notices. Vendoring the pristine snapshot does not make those files WinForge runtime dependencies: `ThirdParty/**` is excluded from build and publish output, and any future code or asset copied into WinForge-owned paths requires a separate license and security review.

To refresh the snapshot, fetch the desired upstream commit and replace the prefix from that commit's tree. Record the new immutable commit hash here and review the upstream license before committing the update.
