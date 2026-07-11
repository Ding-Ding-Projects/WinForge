# Dew Encryption upstream snapshot

WinForge vendors the complete tracked source tree of [codingmachineedge/dew-encryption](https://github.com/codingmachineedge/dew-encryption) under `ThirdParty/DewEncryption`.

- Upstream commit: `a207c7424f203ef0ea88bba825d51b15aba30939`
- Upstream branch: `main`
- Snapshot date: 2026-07-11
- License: MIT; the original copyright and permission notice is preserved at `ThirdParty/DewEncryption/LICENSE`.

The snapshot includes the upstream Python CLI/Tkinter app, Avalonia shell, installer and Explorer/Nautilus integration, documentation, assets, and automation. It is an auditable reference only: `ThirdParty/**` is excluded from WinForge's build and publish item globs, and WinForge never launches or embeds the upstream Python or Avalonia executable.

Compatible snapshot, history, restore, watcher, and encrypted-archive behavior is ported into WinForge-owned native WinUI 3 and C# services. The native port deliberately omits upstream arbitrary hooks, buildable-source execution, Docker registry transport, automatic source deletion, context-menu installers, and password-bearing command lines.

To refresh the snapshot, fetch the desired upstream commit and replace this prefix from that commit's tracked tree. Record the new immutable hash here, preserve the upstream license, and repeat the security and compatibility review before copying or adapting behavior into WinForge-owned paths.
