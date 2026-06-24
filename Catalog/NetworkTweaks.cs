using System;
using System.Collections.Generic;
using Microsoft.Win32;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// 網絡與互聯網（多數係 Cmd／PowerShell 動作）·
/// Network &amp; Internet tweaks — mostly real cmd/PowerShell diagnostic and repair actions.
/// </summary>
public static class NetworkTweaks
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        Tweak.Cmd("network.flush-dns", "Flush DNS cache", "清除 DNS 快取",
            "Clears the DNS resolver cache so stale lookups are dropped.",
            "清除 DNS 解析快取，咁就唔會用返舊嘅查詢結果。",
            "Flush", "清除", "ipconfig /flushdns", keywords: "dns,flush,resolver,快取"),

        Tweak.Cmd("network.release-ip", "Release IP address", "釋放 IP 位址",
            "Releases the current DHCP-assigned IP address on all adapters.",
            "釋放所有網絡卡上面由 DHCP 派嘅 IP 位址。",
            "Release", "釋放", "ipconfig /release", keywords: "dhcp,release,ip,釋放"),

        Tweak.Cmd("network.renew-ip", "Renew IP address", "更新 IP 位址",
            "Requests a fresh DHCP lease for all adapters.",
            "向 DHCP 重新攞過一個 IP 租約畀所有網絡卡。",
            "Renew", "更新", "ipconfig /renew", keywords: "dhcp,renew,ip,更新"),

        Tweak.Cmd("network.reset-winsock", "Reset Winsock catalog", "重設 Winsock",
            "Resets the Winsock catalog to a clean state; fixes broken sockets.",
            "將 Winsock 目錄重設返乾淨，修復壞咗嘅 socket。",
            "Reset", "重設", "netsh winsock reset",
            requiresAdmin: true, restart: RestartScope.Reboot, keywords: "winsock,socket,reset,重設"),

        Tweak.Cmd("network.reset-tcpip", "Reset TCP/IP stack", "重設 TCP/IP",
            "Rewrites the TCP/IP stack registry keys to their defaults.",
            "將 TCP/IP 堆疊嘅登錄檔重設返做預設值。",
            "Reset", "重設", "netsh int ip reset",
            requiresAdmin: true, restart: RestartScope.Reboot, keywords: "tcpip,tcp,ip,reset,重設"),

        Tweak.Cmd("network.flush-arp", "Flush ARP cache", "清除 ARP 快取",
            "Deletes the ARP cache to clear stale IP-to-MAC mappings.",
            "清除 ARP 快取，去走舊咗嘅 IP 對 MAC 對應。",
            "Flush", "清除", "netsh interface ip delete arpcache",
            requiresAdmin: true, keywords: "arp,cache,mac,快取"),

        Tweak.Cmd("network.ipconfig-all", "Show full IP configuration", "顯示完整 IP 設定",
            "Lists every adapter's IP, gateway, DNS and MAC address.",
            "列出每張網絡卡嘅 IP、閘道、DNS 同 MAC 位址。",
            "Show", "顯示", "ipconfig /all", keywords: "ipconfig,ip,config,gateway,設定"),

        Tweak.Powershell("network.public-ip", "Show public IP", "顯示公共 IP",
            "Looks up this device's public IP address from ipify.org.",
            "經 ipify.org 查呢部機嘅公共 IP 位址。",
            "Look up", "查詢", "(Invoke-RestMethod -Uri 'https://api.ipify.org')",
            keywords: "public,ip,external,公共"),

        Tweak.Cmd("network.wifi-profiles", "Show saved Wi-Fi profiles", "顯示已儲存 Wi-Fi",
            "Lists every Wi-Fi network profile saved on this PC.",
            "列出呢部機儲存咗嘅所有 Wi-Fi 網絡設定檔。",
            "Show", "顯示", "netsh wlan show profiles", keywords: "wifi,wlan,profile,wireless,無線"),

        // 物件輸出 pipeline 直接交畀 Table 工廠，渲染成原生網格（無 Format-Table/Out-String）。
        // Object-emitting pipeline handed to the Table factory so the result renders as a native grid.
        Tweak.Table("network.active-connections", "Show active connections", "顯示使用中連線",
            "Lists up to 30 established TCP connections with their endpoints.",
            "列出最多 30 條已建立嘅 TCP 連線同佢哋嘅位址。",
            "Show", "顯示",
            "Get-NetTCPConnection -State Established | Select-Object -First 30 LocalAddress,LocalPort,RemoteAddress,RemotePort",
            keywords: "tcp,connection,netstat,連線"),

        Tweak.Powershell("network.dns-cloudflare", "Set DNS to Cloudflare (1.1.1.1)", "設 DNS 為 Cloudflare",
            "Points the first active physical adapter at Cloudflare's 1.1.1.1 DNS.",
            "將第一張使用中嘅實體網絡卡 DNS 指去 Cloudflare 嘅 1.1.1.1。",
            "Apply", "套用",
            "$a=(Get-NetAdapter -Physical | Where-Object Status -eq 'Up' | Select-Object -First 1).InterfaceIndex; Set-DnsClientServerAddress -InterfaceIndex $a -ServerAddresses '1.1.1.1','1.0.0.1'; Write-Output 'DNS set to Cloudflare 1.1.1.1 / 1.0.0.1'",
            requiresAdmin: true, keywords: "dns,cloudflare,1.1.1.1"),

        Tweak.Powershell("network.dns-google", "Set DNS to Google (8.8.8.8)", "設 DNS 為 Google",
            "Points the first active physical adapter at Google's 8.8.8.8 DNS.",
            "將第一張使用中嘅實體網絡卡 DNS 指去 Google 嘅 8.8.8.8。",
            "Apply", "套用",
            "$a=(Get-NetAdapter -Physical | Where-Object Status -eq 'Up' | Select-Object -First 1).InterfaceIndex; Set-DnsClientServerAddress -InterfaceIndex $a -ServerAddresses '8.8.8.8','8.8.4.4'; Write-Output 'DNS set to Google 8.8.8.8 / 8.8.4.4'",
            requiresAdmin: true, keywords: "dns,google,8.8.8.8"),

        Tweak.Powershell("network.dns-auto", "Reset DNS to automatic", "DNS 還原自動",
            "Clears manual DNS so the adapter gets DNS from DHCP again.",
            "清除手動 DNS，等網絡卡返去用 DHCP 派嘅 DNS。",
            "Reset", "還原",
            "$a=(Get-NetAdapter -Physical | Where-Object Status -eq 'Up' | Select-Object -First 1).InterfaceIndex; Set-DnsClientServerAddress -InterfaceIndex $a -ResetServerAddresses; Write-Output 'DNS reset to automatic (DHCP)'",
            requiresAdmin: true, keywords: "dns,auto,dhcp,自動"),

        Tweak.Cmd("network.ping-test", "Ping test (Cloudflare)", "Ping 測試",
            "Sends four ICMP pings to 1.1.1.1 to check internet reachability.",
            "Ping 1.1.1.1 四次，睇下上唔上到網。",
            "Ping", "測試", "ping -n 4 1.1.1.1", keywords: "ping,latency,test,測試"),
    };
}
