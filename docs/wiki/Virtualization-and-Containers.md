# Virtualization & Containers · 虛擬化與容器

Tools for running and managing containers and virtual machines from one place. · 喺一個地方運行同管理容器同虛擬機嘅工具。

## Docker · Docker 容器管理

Manage Docker containers, images, volumes and networks locally. · 本機管理 Docker 容器、映像、磁碟區同網路。

For Home Assistant-side helpers such as AC Defender, deploy the container or Compose stack on the Docker host first, then expose the resulting entities through Home Assistant. WinForge's Home Assistant module controls the REST entities; this Docker page is for inspecting and managing the local container host.
對於 AC Defender 呢類 Home Assistant 端 helper，先喺 Docker 主機部署容器或 Compose stack，再透過 Home Assistant 暴露實體。WinForge 嘅 Home Assistant 模組負責控制 REST 實體；呢個 Docker 頁面係用嚟檢查同管理本機容器主機。

Open in-app: `WinForge.exe --page docker`

![Docker](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-docker.png)

## Docker over SSH · 透過 SSH 控制 Docker

Control containers on a remote Docker host over SSH. · 透過 SSH 控制遠端 Docker 主機上嘅容器。

Open in-app: `WinForge.exe --page dockerssh`

![Docker over SSH](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-dockerssh.png)

## WSL & VM Launcher · WSL 與 VM 啟動器

Launch WSL distros, Windows Sandbox and virtual machines. · 啟動 WSL 發行版、Windows 沙盒同虛擬機。

Open in-app: `WinForge.exe --page wsl`

![WSL & VM Launcher](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-wsl.png)

## VirtualBox Manager · VirtualBox 管理

Drive VBoxManage for VMs, snapshots and clones. · 驅動 VBoxManage 管理虛擬機、快照同複製。

Open in-app: `WinForge.exe --page virtualbox`

![VirtualBox Manager](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-virtualbox.png)

## Proxmox VE · Proxmox VE 虛擬化

Manage Proxmox VE nodes, QEMU VMs and LXC containers via the REST API. · 用 REST API 管理 Proxmox VE 節點、QEMU 虛擬機同 LXC 容器。

Open in-app: `WinForge.exe --page proxmox`

![Proxmox VE](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-proxmox.png)

[← Wiki Home](Home.md)
