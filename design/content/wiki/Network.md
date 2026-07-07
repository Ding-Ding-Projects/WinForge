# Network · 網絡

Networking, remote access and packet tools for WinForge. · WinForge 嘅網絡、遠端存取同封包工具。

## Connections · 連線

Live TCP/UDP socket list with owning processes (netstat/TCPView). · 即時 TCP／UDP 連線清單同擁有程序（netstat／TCPView）。

Open in-app: `WinForge.exe --page connections`

![Connections](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-connections.png)

## Hosts Editor · hosts 編輯器

Edit the hosts file and block domains. · 編輯 hosts 檔案同封鎖網域。

Open in-app: `WinForge.exe --page hosts`

![Hosts Editor](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-hosts.png)

## Packet Capture · 封包擷取

Capture and filter packets with tshark/dumpcap (pcap). · 用 tshark／dumpcap 擷取同過濾封包（pcap）。

Open in-app: `WinForge.exe --page wireshark`

![Packet Capture](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-wireshark.png)

## Nmap Scanner · 網絡掃描

Scan hosts, ports, services and OS with Nmap. · 用 Nmap 掃描主機、端口、服務同作業系統。

Open in-app: `WinForge.exe --page nmap`

![Nmap Scanner](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-nmap.png)

## VPN & Mesh · VPN 與網狀網

Manage NordVPN and Tailscale mesh connections. · 管理 NordVPN 同 Tailscale 網狀網連線。

Open in-app: `WinForge.exe --page vpn`

![VPN & Mesh](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-vpn.png)

## RustDesk · 遠端桌面

Self-hostable remote desktop control (TeamViewer alternative). · 可自架嘅遠端桌面控制（TeamViewer 替代品）。

Open in-app: `WinForge.exe --page rustdesk`

![RustDesk](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-rustdesk.png)

## Cloudflare & Tunnel · Cloudflare 與 Tunnel

Cloudflared tunnels, DNS routing, Access, DoH and WARP. · Cloudflared 隧道、DNS 路由、Access、DoH 同 WARP。

Open in-app: `WinForge.exe --page cloudflare`

![Cloudflare & Tunnel](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-cloudflare.png)

## Home Assistant · 家居助理

Drive the Home Assistant REST API for scenes, lights and more. · 驅動 Home Assistant REST API 控制場景、燈光等。

Climate controls include thermostat target temperature and HVAC mode for `climate.*` entities, so a Home Assistant deployment that exposes an AC Defender thermostat can be driven from WinForge after the integration is running.
冷氣控制包括為 `climate.*` 實體設定目標溫度同 HVAC 模式；如果 Home Assistant 部署已暴露 AC Defender thermostat，整合運行後就可以由 WinForge 控制。

AC Defender itself is a Home Assistant-side deployment concern: run it in Docker or Docker Compose, expose its entities to Home Assistant, then use this module's REST connection, Lights & Climate tab and Notify tab for operation and alerts.
AC Defender 本身係 Home Assistant 端嘅部署工作：用 Docker 或 Docker Compose 運行，將實體暴露畀 Home Assistant，之後用呢個模組嘅 REST 連線、燈與冷氣分頁同通知分頁操作同收告警。

Open in-app: `WinForge.exe --page homeassistant`

![Home Assistant](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-homeassistant.png)

## In-App Login · 內置登入

Shared WebView2 OAuth and sign-in for connected services. · 共用 WebView2 OAuth 同登入連接服務。

Open in-app: `WinForge.exe --page weblogin`

![In-App Login](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-weblogin.png)

[← Wiki Home](Home.md)
