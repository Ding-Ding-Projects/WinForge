using System;
using System.Collections.Generic;
using Microsoft.Win32;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

public static class NetProTweaks
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        // --- adapters (20) ---
        Tweak.Table("net.adapters.list", "List network adapters", "列出網絡卡",
            "Show all network adapters with status, link speed and interface description.", "顯示所有網絡卡嘅狀態、連線速度同介面描述呀。",
            "List", "列出", "Get-NetAdapter | Select-Object Name, InterfaceDescription, Status, LinkSpeed, MacAddress",
            keywords: "netadapter,list,nic,adapter,網絡卡,列出"),

        Tweak.Table("net.adapters.statistics", "Adapter statistics", "網絡卡統計",
            "Show sent/received bytes and packet counts for every network adapter.", "顯示每張網絡卡收發嘅位元組同封包數量呀。",
            "Stats", "統計", "Get-NetAdapterStatistics | Select-Object Name, ReceivedBytes, SentBytes, ReceivedUnicastPackets, SentUnicastPackets",
            keywords: "statistics,bytes,packets,統計,流量"),

        Tweak.Powershell("net.adapters.disable", "Disable an adapter", "停用網絡卡",
            "Disable a network adapter by name (edit the name in the command first).", "用名稱停用一張網絡卡（先改返指令入面嘅名）呀。",
            "Disable", "停用", "Disable-NetAdapter -Name 'Ethernet' -Confirm:$false",
            requiresAdmin: true, destructive: true, keywords: "disable,off,停用,關閉"),

        Tweak.Powershell("net.adapters.enable", "Enable an adapter", "啟用網絡卡",
            "Enable a previously disabled network adapter by name.", "用名稱啟用之前停用咗嘅網絡卡呀。",
            "Enable", "啟用", "Enable-NetAdapter -Name 'Ethernet' -Confirm:$false",
            requiresAdmin: true, keywords: "enable,on,啟用,開啟"),

        Tweak.Powershell("net.adapters.restart", "Restart an adapter", "重啟網絡卡",
            "Disable then re-enable a network adapter to reset its connection.", "停用再重新啟用網絡卡，等個連線重置返呀。",
            "Restart", "重啟", "Restart-NetAdapter -Name 'Ethernet' -Confirm:$false",
            requiresAdmin: true, destructive: true, keywords: "restart,reset,reconnect,重啟,重新連線"),

        Tweak.Powershell("net.adapters.rename", "Rename an adapter", "重新命名網絡卡",
            "Rename a network adapter to a friendlier name (edit both names first).", "幫網絡卡改個易記啲嘅名（兩個名都要先改返）呀。",
            "Rename", "改名", "Rename-NetAdapter -Name 'Ethernet' -NewName 'LAN'",
            requiresAdmin: true, keywords: "rename,name,改名,命名"),

        Tweak.Powershell("net.adapters.ipconfig", "IP configuration", "IP 設定",
            "Show full IP configuration: addresses, gateway and DNS per interface.", "顯示完整 IP 設定：每個介面嘅地址、閘道同 DNS 呀。",
            "Show", "顯示", "Get-NetIPConfiguration | Format-List InterfaceAlias, IPv4Address, IPv4DefaultGateway, DNSServer",
            keywords: "ipconfig,address,gateway,dns,設定,地址"),

        Tweak.Table("net.adapters.ipaddress", "IP addresses", "IP 地址",
            "List all IPv4 and IPv6 addresses assigned to every interface.", "列出每個介面分配到嘅所有 IPv4 同 IPv6 地址呀。",
            "Show", "顯示", "Get-NetIPAddress | Select-Object InterfaceAlias, IPAddress, AddressFamily, PrefixLength, PrefixOrigin",
            keywords: "ipaddress,ipv4,ipv6,地址"),

        Tweak.Table("net.adapters.advanced-props", "Advanced properties", "進階屬性",
            "Show advanced driver properties (offload, speed/duplex, jumbo frame, etc).", "顯示進階驅動屬性（卸載、速度雙工、巨型封包等等）呀。",
            "Show", "顯示", "Get-NetAdapterAdvancedProperty | Select-Object Name, DisplayName, DisplayValue",
            keywords: "advanced,offload,duplex,jumbo,進階,屬性"),

        Tweak.Powershell("net.adapters.set-mtu", "Set adapter MTU", "設定網絡卡 MTU",
            "Set the MTU size on an interface (edit the interface name and value first).", "設定介面嘅 MTU 大小（先改返介面名同數值）呀。",
            "Set MTU", "設定 MTU", "Set-NetIPInterface -InterfaceAlias 'Ethernet' -NlMtuBytes 1500",
            requiresAdmin: true, keywords: "mtu,frame,size,封包大小"),

        Tweak.Table("net.adapters.binding", "Adapter bindings", "網絡卡綁定",
            "Show which protocols and services are bound to each adapter.", "顯示每張網絡卡綁定咗邊啲協定同服務呀。",
            "Show", "顯示", "Get-NetAdapterBinding | Where-Object Enabled | Select-Object Name, DisplayName, ComponentID",
            keywords: "binding,protocol,ipv4,ipv6,綁定,協定"),

        Tweak.Table("net.adapters.link-speed", "Link speed", "連線速度",
            "Show the negotiated link speed of each connected adapter.", "顯示每張已連線網絡卡協商出嚟嘅連線速度呀。",
            "Show", "顯示", "Get-NetAdapter | Where-Object Status -eq 'Up' | Select-Object Name, LinkSpeed, MediaType, MediaConnectionState",
            keywords: "speed,linkspeed,duplex,速度"),

        Tweak.Powershell("net.adapters.stats-detail", "Detailed statistics", "詳細統計",
            "Show detailed per-adapter statistics including discards and errors.", "顯示每張網絡卡嘅詳細統計，包括丟棄同錯誤呀。",
            "Show", "顯示", "Get-NetAdapterStatistics | Format-List Name, ReceivedBytes, SentBytes, ReceivedDiscardedPackets, OutboundDiscardedPackets",
            keywords: "statistics,errors,discards,詳細,統計"),

        Tweak.Cmd("net.adapters.getmac", "Show MAC addresses", "顯示 MAC 地址",
            "List the MAC (physical) addresses of all adapters with transport names.", "列出所有網絡卡嘅 MAC（實體）地址同傳輸名稱呀。",
            "Show", "顯示", "getmac /v /fo list",
            keywords: "mac,physical,getmac,實體地址"),

        Tweak.Table("net.adapters.route", "Routing table", "路由表",
            "Show the IP routing table with destinations, gateways and metrics.", "顯示 IP 路由表，包括目的地、閘道同度量值呀。",
            "Show", "顯示", "Get-NetRoute | Format-Table DestinationPrefix, NextHop, RouteMetric, InterfaceAlias -AutoSize",
            keywords: "route,routing,gateway,路由,閘道"),

        Tweak.Powershell("net.adapters.set-dhcp", "Reset adapter to DHCP", "重設為 DHCP",
            "Switch an interface back to automatic DHCP addressing (edit the name first).", "將介面改返自動 DHCP 取得地址（先改返個名）呀。",
            "Set DHCP", "設 DHCP", "Set-NetIPInterface -InterfaceAlias 'Ethernet' -Dhcp Enabled; Set-DnsClientServerAddress -InterfaceAlias 'Ethernet' -ResetServerAddresses",
            requiresAdmin: true, destructive: true, keywords: "dhcp,automatic,reset,自動,重設"),

        Tweak.Powershell("net.adapters.power-mgmt", "Adapter power management", "網絡卡電源管理",
            "Show power-management settings such as selective suspend per adapter.", "顯示每張網絡卡嘅電源管理設定，例如選擇性暫停呀。",
            "Show", "顯示", "Get-NetAdapterPowerManagement | Format-List Name, SelectiveSuspend, WakeOnMagicPacket, DeviceSleepOnDisconnect",
            keywords: "power,suspend,sleep,電源,省電"),

        Tweak.Powershell("net.adapters.wol", "Wake-on-LAN setting", "網絡喚醒設定",
            "Enable Wake-on-LAN (wake on magic packet) for an adapter (edit name first).", "為網絡卡啟用網絡喚醒（魔術封包喚醒，先改返個名）呀。",
            "Enable WoL", "啟用喚醒", "Set-NetAdapterPowerManagement -Name 'Ethernet' -WakeOnMagicPacket Enabled",
            requiresAdmin: true, keywords: "wol,wake,magic,lan,喚醒"),

        Tweak.Powershell("net.adapters.conn-profile", "Connection profile", "連線設定檔",
            "Show the network category (Public/Private/Domain) and connectivity per profile.", "顯示每個連線設定檔嘅網絡類別（公用/私人/網域）同連線狀態呀。",
            "Show", "顯示", "Get-NetConnectionProfile | Format-List Name, InterfaceAlias, NetworkCategory, IPv4Connectivity, IPv6Connectivity",
            keywords: "profile,public,private,domain,設定檔,類別"),

        Tweak.Powershell("net.adapters.disable-ipv6", "Disable IPv6 on adapter", "停用網絡卡 IPv6",
            "Unbind the IPv6 protocol from a specific adapter (edit the name first).", "將 IPv6 協定從指定網絡卡解除綁定（先改返個名）呀。",
            "Disable IPv6", "停用 IPv6", "Disable-NetAdapterBinding -Name 'Ethernet' -ComponentID ms_tcpip6",
            requiresAdmin: true, destructive: true, keywords: "ipv6,disable,binding,停用"),

        Tweak.Table("net.adapters.dnsclient", "DNS client settings", "DNS 客戶端設定",
            "Show DNS client settings such as connection-specific suffix per interface.", "顯示每個介面嘅 DNS 客戶端設定，例如連線專用後綴呀。",
            "Show", "顯示", "Get-DnsClient | Format-Table InterfaceAlias, ConnectionSpecificSuffix, RegisterThisConnectionsAddress -AutoSize",
            keywords: "dns,client,suffix,dnsclient,後綴"),

        // --- ipdns (20) ---
        Tweak.Powershell("net.ipdns.ipconfig-all", "Full IP configuration", "完整 IP 設定",
            "Show full TCP/IP configuration for every adapter, including DNS, MAC and lease info.", "顯示每個網絡卡嘅完整 TCP/IP 設定，包括 DNS、MAC 同租約資料。",
            "Show", "顯示", "ipconfig /all",
            keywords: "ipconfig,all,ip,dns,設定,網絡"),

        Tweak.Cmd("net.ipdns.flushdns", "Flush DNS cache", "清除 DNS 快取",
            "Purge the local DNS resolver cache so stale lookups are discarded.", "清走本機 DNS 解析快取，咁就唔會用到過時嘅查詢結果。",
            "Flush", "清除", "ipconfig /flushdns",
            keywords: "ipconfig,flushdns,dns,快取,清除"),

        Tweak.Cmd("net.ipdns.registerdns", "Re-register DNS", "重新註冊 DNS",
            "Refresh DHCP leases and re-register this PC's DNS names with the server.", "刷新 DHCP 租約，並向伺服器重新註冊呢部電腦嘅 DNS 名稱。",
            "Register", "註冊", "ipconfig /registerdns",
            requiresAdmin: true, keywords: "ipconfig,registerdns,dns,註冊"),

        Tweak.Cmd("net.ipdns.release", "Release IP address", "釋放 IP 位址",
            "Release the current DHCP-assigned IPv4 address on all adapters.", "釋放所有網絡卡上面由 DHCP 分配嘅 IPv4 位址。",
            "Release", "釋放", "ipconfig /release",
            requiresAdmin: true, keywords: "ipconfig,release,ip,dhcp,釋放"),

        Tweak.Cmd("net.ipdns.renew", "Renew IP address", "更新 IP 位址",
            "Request a fresh DHCP lease and renew the IPv4 address on all adapters.", "重新向 DHCP 要一個新租約，更新所有網絡卡嘅 IPv4 位址。",
            "Renew", "更新", "ipconfig /renew",
            requiresAdmin: true, keywords: "ipconfig,renew,ip,dhcp,更新"),

        Tweak.Cmd("net.ipdns.displaydns", "Show DNS cache", "顯示 DNS 快取",
            "Display the contents of the local DNS resolver cache.", "顯示本機 DNS 解析快取入面有咩記錄。",
            "Show", "顯示", "ipconfig /displaydns",
            keywords: "ipconfig,displaydns,dns,快取,顯示"),

        Tweak.Powershell("net.ipdns.set-cloudflare", "Use Cloudflare DNS", "用 Cloudflare DNS",
            "Set DNS servers to Cloudflare 1.1.1.1 and 1.0.0.1 on the active adapter.", "將使用緊嘅網絡卡 DNS 設成 Cloudflare 1.1.1.1 同 1.0.0.1。",
            "Apply", "套用", "Get-NetAdapter -Physical | Where-Object Status -eq 'Up' | Set-DnsClientServerAddress -ServerAddresses 1.1.1.1,1.0.0.1",
            requiresAdmin: true, keywords: "dns,cloudflare,1.1.1.1,set"),

        Tweak.Powershell("net.ipdns.set-google", "Use Google DNS", "用 Google DNS",
            "Set DNS servers to Google 8.8.8.8 and 8.8.4.4 on the active adapter.", "將使用緊嘅網絡卡 DNS 設成 Google 8.8.8.8 同 8.8.4.4。",
            "Apply", "套用", "Get-NetAdapter -Physical | Where-Object Status -eq 'Up' | Set-DnsClientServerAddress -ServerAddresses 8.8.8.8,8.8.4.4",
            requiresAdmin: true, keywords: "dns,google,8.8.8.8,set"),

        Tweak.Powershell("net.ipdns.set-quad9", "Use Quad9 DNS", "用 Quad9 DNS",
            "Set DNS servers to Quad9 9.9.9.9 and 149.112.112.112 on the active adapter.", "將使用緊嘅網絡卡 DNS 設成 Quad9 9.9.9.9 同 149.112.112.112。",
            "Apply", "套用", "Get-NetAdapter -Physical | Where-Object Status -eq 'Up' | Set-DnsClientServerAddress -ServerAddresses 9.9.9.9,149.112.112.112",
            requiresAdmin: true, keywords: "dns,quad9,9.9.9.9,set"),

        Tweak.Powershell("net.ipdns.reset-dhcp", "Reset DNS to DHCP", "DNS 還原做 DHCP",
            "Clear manual DNS servers so the adapter gets DNS from DHCP again.", "清走手動設定嘅 DNS 伺服器，等網絡卡重新由 DHCP 攞返 DNS。",
            "Reset", "還原", "Get-NetAdapter -Physical | Where-Object Status -eq 'Up' | Set-DnsClientServerAddress -ResetServerAddresses",
            requiresAdmin: true, keywords: "dns,dhcp,reset,還原"),

        Tweak.Cmd("net.ipdns.route-print", "Print routing table", "顯示路由表",
            "Display the IPv4 and IPv6 routing table for this machine.", "顯示呢部機嘅 IPv4 同 IPv6 路由表。",
            "Show", "顯示", "route print",
            keywords: "route,print,routing,路由"),

        Tweak.Cmd("net.ipdns.add-route", "Add persistent route", "新增永久路由",
            "Add a sample persistent route to 10.0.0.0/8 via gateway 192.168.1.1 (edit before use).", "新增一條去 10.0.0.0/8、經閘道 192.168.1.1 嘅永久路由（用之前請自行改）。",
            "Add", "新增", "route -p add 10.0.0.0 mask 255.0.0.0 192.168.1.1",
            requiresAdmin: true, keywords: "route,add,persistent,路由"),

        Tweak.Table("net.ipdns.get-serveraddress", "Get DNS servers", "查 DNS 伺服器",
            "List the configured DNS server addresses for each interface.", "列出每個介面所設定嘅 DNS 伺服器位址。",
            "Show", "顯示", "Get-DnsClientServerAddress | Select-Object InterfaceAlias, AddressFamily, @{N='ServerAddresses';E={$_.ServerAddresses -join ', '}}",
            keywords: "dns,get,serveraddress,伺服器"),

        Tweak.Powershell("net.ipdns.clear-cache", "Clear DNS client cache", "清 DNS 用戶端快取",
            "Flush the DNS client cache using the PowerShell Clear-DnsClientCache cmdlet.", "用 PowerShell 嘅 Clear-DnsClientCache 指令清走 DNS 用戶端快取。",
            "Clear", "清除", "Clear-DnsClientCache",
            keywords: "dns,clear,cache,快取"),

        Tweak.Powershell("net.ipdns.resolve-name", "Resolve a hostname", "解析網域名稱",
            "Resolve example.com to its IP addresses using Resolve-DnsName.", "用 Resolve-DnsName 將 example.com 解析做佢嘅 IP 位址。",
            "Resolve", "解析", "Resolve-DnsName example.com",
            keywords: "dns,resolve,resolve-dnsname,解析"),

        Tweak.Cmd("net.ipdns.nslookup", "Test DNS lookup", "測試 DNS 查詢",
            "Run an nslookup against example.com to test name resolution.", "對 example.com 做 nslookup，測試名稱解析正唔正常。",
            "Lookup", "查詢", "nslookup example.com",
            keywords: "nslookup,dns,test,查詢"),

        Tweak.Powershell("net.ipdns.public-ip", "Show public IP", "顯示對外 IP",
            "Query the ipify API to show this connection's public IP address.", "查 ipify API，顯示呢條連線對外嘅公網 IP 位址。",
            "Show", "顯示", "Invoke-RestMethod -Uri 'https://api.ipify.org'",
            keywords: "public,ip,ipify,對外,公網"),

        Tweak.Cmd("net.ipdns.arp", "Show ARP table", "顯示 ARP 表",
            "Display the ARP cache mapping IP addresses to MAC addresses.", "顯示 ARP 快取，睇 IP 位址同 MAC 位址嘅對應。",
            "Show", "顯示", "arp -a",
            keywords: "arp,mac,table,ARP"),

        Tweak.Cmd("net.ipdns.reset-ip", "Reset TCP/IP stack", "重設 TCP/IP 堆疊",
            "Rewrite the TCP/IP stack registry keys to defaults; a reboot is required after.", "將 TCP/IP 堆疊嘅登錄機碼還原做預設值，做完之後要重開機。",
            "Reset", "重設", "netsh int ip reset",
            requiresAdmin: true, destructive: true, restart: RestartScope.Reboot, keywords: "netsh,ip,reset,tcpip,重設"),

        Tweak.Cmd("net.ipdns.reset-winsock", "Reset Winsock catalog", "重設 Winsock 目錄",
            "Reset the Winsock catalog to a clean state; a reboot is required after.", "將 Winsock 目錄重設返做乾淨狀態，做完之後要重開機。",
            "Reset", "重設", "netsh winsock reset",
            requiresAdmin: true, destructive: true, restart: RestartScope.Reboot, keywords: "netsh,winsock,reset,重設"),

        // --- wifi (20) ---
        Tweak.Cmd("net.wifi.show-profiles", "Show saved Wi-Fi profiles", "顯示已儲存嘅 Wi-Fi 設定檔",
            "List every wireless profile saved on this PC.", "列出呢部電腦儲存咗嘅所有無線設定檔。",
            "Show", "顯示", "netsh wlan show profiles",
            keywords: "wifi,netsh,wlan,profiles,設定檔"),

        Tweak.Shell("net.wifi.show-profile-key", "Show Wi-Fi password", "顯示 Wi-Fi 密碼",
            "Show a saved profile with its key in clear text. Replace PROFILE with the SSID name.", "用明文顯示某個設定檔嘅密碼。將 PROFILE 換成你個 SSID 名啦。",
            "Show key", "顯示密碼", "netsh.exe", "wlan show profile name=\"PROFILE\" key=clear",
            keywords: "wifi,password,key,clear,密碼"),

        Tweak.Cmd("net.wifi.show-interfaces", "Show wireless interfaces", "顯示無線網絡介面",
            "Show state, SSID, signal and channel for each Wi-Fi adapter.", "顯示每個 Wi-Fi 介面卡嘅狀態、SSID、訊號同頻道。",
            "Show", "顯示", "netsh wlan show interfaces",
            keywords: "wifi,interfaces,signal,介面,訊號"),

        Tweak.Cmd("net.wifi.show-drivers", "Show Wi-Fi drivers", "顯示 Wi-Fi 驅動程式",
            "Show wireless driver details and supported capabilities.", "顯示無線驅動程式嘅詳情同支援功能。",
            "Show", "顯示", "netsh wlan show drivers",
            keywords: "wifi,drivers,驅動"),

        Tweak.Cmd("net.wifi.show-networks-bssid", "Scan networks (with BSSID)", "掃描網絡（含 BSSID）",
            "List visible networks including each access point's BSSID.", "列出附近可見嘅網絡，包埋每個接入點嘅 BSSID。",
            "Scan", "掃描", "netsh wlan show networks mode=bssid",
            keywords: "wifi,scan,bssid,networks,掃描"),

        Tweak.Cmd("net.wifi.disconnect", "Disconnect Wi-Fi", "中斷 Wi-Fi 連線",
            "Disconnect from the currently connected wireless network.", "由目前連住嘅無線網絡度斷開。",
            "Disconnect", "斷開", "netsh wlan disconnect",
            keywords: "wifi,disconnect,斷開"),

        Tweak.Shell("net.wifi.connect", "Connect to a profile", "連接到設定檔",
            "Connect to a saved Wi-Fi profile. Replace PROFILE with the saved profile name.", "連接到已儲存嘅 Wi-Fi 設定檔。將 PROFILE 換成設定檔名啦。",
            "Connect", "連接", "netsh.exe", "wlan connect name=\"PROFILE\"",
            keywords: "wifi,connect,profile,連接"),

        Tweak.Cmd("net.wifi.export-profiles", "Export profiles to folder", "匯出設定檔到資料夾",
            "Create C:\\Temp\\WlanProfiles then export all Wi-Fi profiles (with keys) as XML into it.", "建立 C:\\Temp\\WlanProfiles，再將所有 Wi-Fi 設定檔（含密碼）匯出成 XML 入去。",
            "Export", "匯出", "if not exist \"C:\\Temp\\WlanProfiles\" mkdir \"C:\\Temp\\WlanProfiles\" & netsh wlan export profile folder=\"C:\\Temp\\WlanProfiles\" key=clear",
            keywords: "wifi,export,xml,匯出"),

        Tweak.Cmd("net.wifi.wlanreport", "Generate WLAN report", "產生 WLAN 報告",
            "Build the wireless diagnostics HTML report (last 3 days) under ProgramData.", "產生最近三日嘅無線診斷 HTML 報告，放喺 ProgramData 度。",
            "Generate", "產生", "netsh wlan show wlanreport",
            requiresAdmin: true, keywords: "wifi,wlanreport,report,診斷,報告"),

        Tweak.Cmd("net.wifi.show-capabilities", "Show wireless capabilities", "顯示無線功能",
            "Show the wireless capabilities supported by this system.", "顯示呢個系統支援嘅無線功能。",
            "Show", "顯示", "netsh wlan show wirelesscapabilities",
            keywords: "wifi,capabilities,功能"),

        Tweak.Cmd("net.wifi.show-autoconfig", "Show auto-config setting", "顯示自動設定狀態",
            "Show whether the WLAN AutoConfig logic is enabled on each interface.", "顯示每個介面嘅 WLAN 自動設定邏輯有冇開。",
            "Show", "顯示", "netsh wlan show autoconfig",
            keywords: "wifi,autoconfig,自動設定"),

        Tweak.Shell("net.wifi.block-network", "Block a network", "封鎖一個網絡",
            "Add a deny filter to block a specific SSID. Replace SSID with the network name.", "加一個拒絕篩選去封鎖指定 SSID。將 SSID 換成網絡名啦。",
            "Block", "封鎖", "netsh.exe", "wlan add filter permission=block ssid=\"SSID\" networktype=infrastructure",
            requiresAdmin: true, keywords: "wifi,block,filter,封鎖"),

        Tweak.Cmd("net.wifi.show-hostednetwork", "Show hosted network", "顯示主機網絡",
            "Show the status and settings of the (legacy) hosted network.", "顯示（舊版）主機網絡嘅狀態同設定。",
            "Show", "顯示", "netsh wlan show hostednetwork",
            keywords: "wifi,hostednetwork,hotspot,主機網絡"),

        Tweak.Cmd("net.wifi.show-filters", "Show network filters", "顯示網絡篩選",
            "List the allow/deny filters currently applied to wireless networks.", "列出目前套用喺無線網絡上嘅允許/拒絕篩選。",
            "Show", "顯示", "netsh wlan show filters",
            keywords: "wifi,filters,allow,block,篩選"),

        Tweak.Cmd("net.wifi.list-ssids", "List SSIDs in range", "列出範圍內 SSID",
            "List the SSID names of every wireless network currently in range.", "列出目前範圍內所有無線網絡嘅 SSID 名。",
            "List", "列出", "netsh wlan show networks mode=ssid",
            keywords: "wifi,ssid,networks,範圍"),

        Tweak.Cmd("net.wifi.show-signal", "Show signal strength", "顯示訊號強度",
            "Show the current connection's signal strength via interface details.", "透過介面詳情顯示目前連線嘅訊號強度。",
            "Show", "顯示", "netsh wlan show interfaces",
            keywords: "wifi,signal,strength,訊號"),

        Tweak.Shell("net.wifi.forget-network", "Forget a network", "忘記一個網絡",
            "Delete a saved Wi-Fi profile so Windows forgets it. Replace PROFILE with the profile name.", "刪除已儲存嘅 Wi-Fi 設定檔，等 Windows 唔記得佢。將 PROFILE 換成設定檔名啦。",
            "Forget", "忘記", "netsh.exe", "wlan delete profile name=\"PROFILE\"",
            destructive: true, keywords: "wifi,forget,delete,profile,忘記,刪除"),

        Tweak.Cmd("net.wifi.show-randomization", "Show MAC randomization", "顯示 MAC 隨機化",
            "Show whether random hardware (MAC) addresses are in use per interface.", "顯示每個介面有冇用緊隨機硬件（MAC）位址。",
            "Show", "顯示", "netsh wlan show interfaces",
            keywords: "wifi,mac,randomization,隨機化"),

        Tweak.Cmd("net.wifi.show-all-details", "Show all profiles (details)", "顯示所有設定檔（詳情）",
            "Show every saved profile with full settings and stored keys.", "顯示每個已儲存設定檔嘅完整設定同密碼。",
            "Show", "顯示", "netsh wlan show profile name=* key=clear",
            keywords: "wifi,profiles,details,key,詳情"),

        Tweak.Cmd("net.wifi.refresh-networks", "Refresh networks", "重新整理網絡",
            "Trigger a fresh scan and re-list available wireless networks.", "觸發重新掃描並重新列出可用嘅無線網絡。",
            "Refresh", "重新整理", "netsh wlan show networks mode=ssid",
            keywords: "wifi,refresh,scan,重新整理"),

        // --- firewall (20) ---
        Tweak.Table("net.firewall.show-all-profiles", "Show all profiles state", "顯示所有設定檔狀態",
            "Display the enabled state and default actions of all three firewall profiles.", "顯示三個防火牆設定檔嘅啟用狀態同預設動作。",
            "Show", "顯示", "Get-NetFirewallProfile | Format-Table Name,Enabled,DefaultInboundAction,DefaultOutboundAction -AutoSize",
            requiresAdmin: true, keywords: "firewall,profile,防火牆,設定檔"),

        Tweak.Cmd("net.firewall.turn-all-on", "Turn firewall on (all profiles)", "開啟防火牆（所有設定檔）",
            "Enable Windows Defender Firewall for the domain, private and public profiles.", "為網域、私人同公用設定檔啟用 Windows Defender 防火牆。",
            "Turn On", "開啟", "netsh advfirewall set allprofiles state on",
            requiresAdmin: true, keywords: "firewall,on,enable,防火牆,開啟"),

        Tweak.Cmd("net.firewall.turn-all-off", "Turn firewall off (all profiles)", "關閉防火牆（所有設定檔）",
            "Disable Windows Defender Firewall on every profile. Leaves the machine unprotected.", "喺所有設定檔停用 Windows Defender 防火牆，會令電腦冇保護。",
            "Turn Off", "關閉", "netsh advfirewall set allprofiles state off",
            requiresAdmin: true, destructive: true, keywords: "firewall,off,disable,防火牆,關閉"),

        Tweak.Cmd("net.firewall.reset", "Reset firewall to defaults", "重設防火牆做預設值",
            "Restore Windows Firewall to its out-of-box default policy, removing all custom rules.", "將 Windows 防火牆還原做出廠預設政策，會移除所有自訂規則。",
            "Reset", "重設", "netsh advfirewall reset",
            requiresAdmin: true, destructive: true, keywords: "firewall,reset,default,防火牆,重設"),

        Tweak.Table("net.firewall.list-inbound", "List enabled inbound rules", "列出已啟用嘅入站規則",
            "Show the first 30 enabled inbound firewall rules.", "顯示頭 30 條已啟用嘅入站防火牆規則。",
            "List", "列出", "Get-NetFirewallRule -Enabled True -Direction Inbound | Select-Object -First 30 DisplayName,Action,Profile | Format-Table -AutoSize",
            requiresAdmin: true, keywords: "firewall,inbound,rules,入站,規則"),

        Tweak.Table("net.firewall.list-outbound", "List enabled outbound rules", "列出已啟用嘅出站規則",
            "Show the first 30 enabled outbound firewall rules.", "顯示頭 30 條已啟用嘅出站防火牆規則。",
            "List", "列出", "Get-NetFirewallRule -Enabled True -Direction Outbound | Select-Object -First 30 DisplayName,Action,Profile | Format-Table -AutoSize",
            requiresAdmin: true, keywords: "firewall,outbound,rules,出站,規則"),

        Tweak.Cmd("net.firewall.enable-remote-assistance", "Enable a rule group (Remote Assistance)", "啟用規則群組（遠端協助）",
            "Enable the built-in Remote Assistance firewall rule group as an example of enabling a rule.", "啟用內置嘅遠端協助防火牆規則群組，示範點樣啟用規則。",
            "Enable", "啟用", "netsh advfirewall firewall set rule group=\"Remote Assistance\" new enable=Yes",
            requiresAdmin: true, keywords: "firewall,enable,rule,啟用,規則"),

        Tweak.Cmd("net.firewall.block-app-inbound", "Block an app inbound", "封鎖應用程式入站",
            "Add an inbound block rule for an executable. Edit the program path before running.", "為一個程式加入站封鎖規則，執行前請改程式路徑。",
            "Block", "封鎖", "netsh advfirewall firewall add rule name=\"Block App Inbound\" dir=in action=block program=\"C:\\Path\\To\\App.exe\" enable=yes",
            requiresAdmin: true, destructive: true, keywords: "firewall,block,app,inbound,封鎖,程式"),

        Tweak.Cmd("net.firewall.allow-ping", "Allow ping (ICMP echo)", "允許 Ping（ICMP）",
            "Add an inbound rule allowing ICMPv4 echo requests so the PC responds to ping.", "加入站規則允許 ICMPv4 回應請求，等部電腦識回應 ping。",
            "Allow", "允許", "netsh advfirewall firewall add rule name=\"Allow ICMPv4-In\" protocol=icmpv4:8,any dir=in action=allow",
            requiresAdmin: true, keywords: "firewall,ping,icmp,allow,允許"),

        Tweak.Cmd("net.firewall.enable-logging", "Enable firewall logging", "啟用防火牆記錄",
            "Turn on dropped-connection and allowed-connection logging for all profiles.", "為所有設定檔開啟丟棄連線同允許連線嘅記錄。",
            "Enable", "啟用", "netsh advfirewall set allprofiles logging droppedconnections enable & netsh advfirewall set allprofiles logging allowedconnections enable",
            requiresAdmin: true, keywords: "firewall,logging,log,記錄,日誌"),

        Tweak.Table("net.firewall.show-current-profile", "Show current active profile", "顯示目前作用中設定檔",
            "Display which firewall profile is currently active on connected networks.", "顯示目前已連接網絡上邊個防火牆設定檔正在生效。",
            "Show", "顯示", "Get-NetConnectionProfile | Format-Table InterfaceAlias,NetworkCategory,IPv4Connectivity -AutoSize",
            requiresAdmin: true, keywords: "firewall,profile,current,設定檔,目前"),

        Tweak.Cmd("net.firewall.default-inbound-block", "Set default inbound to block", "設定預設入站為封鎖",
            "Set the default inbound action to block on all profiles so unmatched inbound traffic is dropped.", "將所有設定檔嘅預設入站動作設為封鎖，未配對嘅入站流量會被丟棄。",
            "Apply", "套用", "netsh advfirewall set allprofiles firewallpolicy blockinbound,allowoutbound",
            requiresAdmin: true, keywords: "firewall,default,inbound,block,預設,封鎖"),

        Tweak.Cmd("net.firewall.export-policy", "Export firewall policy to file", "匯出防火牆政策到檔案",
            "Export the complete current firewall policy to a .wfw file on disk.", "將目前完整嘅防火牆政策匯出做磁碟上嘅 .wfw 檔案。",
            "Export", "匯出", "netsh advfirewall export \"%USERPROFILE%\\Desktop\\firewall-policy.wfw\"",
            requiresAdmin: true, keywords: "firewall,export,policy,匯出,政策"),

        Tweak.Table("net.firewall.list-blocked-apps", "List blocked program rules", "列出已封鎖程式規則",
            "Show enabled rules whose action is Block, i.e. apps and ports being blocked.", "顯示動作為封鎖嘅已啟用規則，即係被封鎖嘅程式同連接埠。",
            "List", "列出", "Get-NetFirewallRule -Enabled True -Action Block | Select-Object -First 30 DisplayName,Direction,Profile | Format-Table -AutoSize",
            requiresAdmin: true, keywords: "firewall,blocked,apps,封鎖,程式"),

        Tweak.Table("net.firewall.show-notifications", "Show notification setting", "顯示通知設定",
            "Display whether the firewall notifies you when it blocks an app, per profile.", "顯示每個設定檔封鎖程式時防火牆會唔會通知你。",
            "Show", "顯示", "Get-NetFirewallProfile | Format-Table Name,NotifyOnListen,Enabled -AutoSize",
            requiresAdmin: true, keywords: "firewall,notification,notify,通知"),

        Tweak.Cmd("net.firewall.enable-rdp", "Enable Remote Desktop rule", "啟用遠端桌面規則",
            "Enable the built-in Remote Desktop firewall rule group to allow incoming RDP.", "啟用內置嘅遠端桌面防火牆規則群組，允許入站 RDP。",
            "Enable", "啟用", "netsh advfirewall firewall set rule group=\"remote desktop\" new enable=Yes",
            requiresAdmin: true, keywords: "firewall,rdp,remote desktop,遠端桌面"),

        Tweak.Table("net.firewall.show-log-path", "Show firewall log path", "顯示防火牆記錄路徑",
            "Display the log file path and max size configured for each firewall profile.", "顯示每個防火牆設定檔嘅記錄檔路徑同最大大小。",
            "Show", "顯示", "Get-NetFirewallProfile | Format-Table Name,LogFileName,LogMaxSizeKilobytes -AutoSize",
            requiresAdmin: true, keywords: "firewall,log,path,記錄,路徑"),

        Tweak.Cmd("net.firewall.restore-default-rules", "Restore default policy file", "從預設政策檔還原",
            "Re-import a previously exported policy file to restore firewall rules. Edit the path first.", "重新匯入之前匯出嘅政策檔以還原防火牆規則，執行前請改路徑。",
            "Restore", "還原", "netsh advfirewall import \"%USERPROFILE%\\Desktop\\firewall-policy.wfw\"",
            requiresAdmin: true, destructive: true, keywords: "firewall,restore,import,還原,匯入"),

        Tweak.Table("net.firewall.list-by-group", "List rules grouped by group", "依群組列出規則",
            "Show enabled inbound rules grouped by their rule group name.", "顯示已啟用入站規則，並依規則群組名稱分組。",
            "List", "列出", "Get-NetFirewallRule -Enabled True -Direction Inbound | Group-Object Group | Sort-Object Count -Descending | Select-Object -First 30 Count,Name | Format-Table -AutoSize",
            requiresAdmin: true, keywords: "firewall,group,rules,群組,規則"),

        Tweak.Table("net.firewall.count-enabled", "Count enabled rules", "統計已啟用規則數量",
            "Count how many firewall rules are currently enabled, split by direction.", "統計目前有幾多條防火牆規則已啟用，並按方向分開。",
            "Count", "統計", "Get-NetFirewallRule -Enabled True | Group-Object Direction | Format-Table Name,Count -AutoSize",
            requiresAdmin: true, keywords: "firewall,count,rules,統計,規則"),

        // --- diag (20) ---
        Tweak.Cmd("net.diag.ping-cloudflare", "Ping 1.1.1.1", "Ping 1.1.1.1",
            "Send 4 ICMP echo requests to Cloudflare DNS (1.1.1.1) to check basic reachability and latency.", "向 Cloudflare DNS (1.1.1.1) 發 4 個 ICMP 回應請求，睇下基本連通同延遲呀。",
            "Ping", "Ping", "ping -n 4 1.1.1.1",
            keywords: "ping,cloudflare,latency,延遲,連通"),

        Tweak.Powershell("net.diag.ping-gateway", "Ping default gateway", "Ping 預設閘道",
            "Ping your default gateway 4 times to confirm the local router/link is responding.", "Ping 你嘅預設閘道 4 次，確認本地路由器同連線有冇回應呀。",
            "Ping gateway", "Ping 閘道", "$gw = (Get-NetRoute -DestinationPrefix '0.0.0.0/0' | Sort-Object RouteMetric | Select-Object -First 1).NextHop; Write-Output \"Gateway: $gw\"; ping -n 4 $gw",
            keywords: "gateway,router,閘道,路由器,ping"),

        Tweak.Cmd("net.diag.tracert", "Trace route to 1.1.1.1", "追蹤路由去 1.1.1.1",
            "Trace the network hops to 1.1.1.1 without resolving hostnames (-d) for faster output.", "追蹤去 1.1.1.1 嘅每一跳，唔解析主機名 (-d) 出嚟快啲呀。",
            "Trace route", "追蹤路由", "tracert -d 1.1.1.1",
            keywords: "tracert,traceroute,hops,路由,追蹤"),

        Tweak.Cmd("net.diag.pathping", "Path ping to 1.1.1.1", "Path ping 去 1.1.1.1",
            "Run pathping to 1.1.1.1, combining traceroute with per-hop packet loss statistics over time.", "對 1.1.1.1 行 pathping，結合追蹤路由同每跳隨時間嘅丟包統計呀。",
            "Pathping", "Pathping", "pathping -n 1.1.1.1",
            keywords: "pathping,loss,丟包,每跳,統計"),

        Tweak.Cmd("net.diag.nslookup", "DNS lookup (nslookup)", "DNS 查詢 (nslookup)",
            "Resolve example.com with nslookup to verify DNS resolution is working.", "用 nslookup 解析 example.com，確認 DNS 解析正常呀。",
            "Lookup", "查詢", "nslookup example.com",
            keywords: "nslookup,dns,resolve,解析,查詢"),

        Tweak.Powershell("net.diag.testnet-ping", "Test-NetConnection (ping)", "Test-NetConnection (ping)",
            "Run Test-NetConnection against 1.1.1.1 for a PowerShell-native connectivity and latency summary.", "對 1.1.1.1 行 Test-NetConnection，攞 PowerShell 原生嘅連通同延遲摘要呀。",
            "Test", "測試", "Test-NetConnection -ComputerName 1.1.1.1 -InformationLevel Detailed",
            keywords: "test-netconnection,tnc,連通,測試"),

        Tweak.Powershell("net.diag.testnet-port", "Test TCP port 443", "測試 TCP 埠 443",
            "Check whether TCP port 443 (HTTPS) is reachable on example.com using Test-NetConnection.", "用 Test-NetConnection 睇下 example.com 嘅 TCP 443 埠 (HTTPS) 通唔通呀。",
            "Test port", "測試埠", "Test-NetConnection -ComputerName example.com -Port 443",
            keywords: "port,443,https,tcp,埠,測試"),

        Tweak.Powershell("net.diag.testnet-traceroute", "Test-NetConnection trace route", "Test-NetConnection 追蹤路由",
            "Use Test-NetConnection -TraceRoute to list hops to 1.1.1.1 with PowerShell.", "用 Test-NetConnection -TraceRoute 喺 PowerShell 列出去 1.1.1.1 嘅每一跳呀。",
            "Trace", "追蹤", "Test-NetConnection -ComputerName 1.1.1.1 -TraceRoute",
            keywords: "test-netconnection,traceroute,hops,追蹤,路由"),

        Tweak.Cmd("net.diag.netstat-ano", "Active connections (netstat -ano)", "活動連線 (netstat -ano)",
            "List all active TCP/UDP connections and listening ports with owning process IDs (-ano).", "列出所有活動 TCP/UDP 連線同監聽埠，連埋擁有嘅 process ID (-ano) 呀。",
            "Show connections", "顯示連線", "netstat -ano",
            keywords: "netstat,connections,pid,連線,埠"),

        Tweak.Cmd("net.diag.netstat-rn", "Routing table (netstat -rn)", "路由表 (netstat -rn)",
            "Display the IPv4/IPv6 routing table numerically with netstat -rn.", "用 netstat -rn 以數字形式顯示 IPv4/IPv6 路由表呀。",
            "Show routes", "顯示路由", "netstat -rn",
            keywords: "netstat,route,routing,路由,路由表"),

        Tweak.Table("net.diag.tcp-by-state", "TCP connections by state", "按狀態列 TCP 連線",
            "Group Get-NetTCPConnection results by state to see how many connections are Established, Listen, TimeWait, etc.", "將 Get-NetTCPConnection 結果按狀態分組，睇下幾多連線係 Established、Listen、TimeWait 等等呀。",
            "Group by state", "按狀態分組", "Get-NetTCPConnection | Group-Object State | Sort-Object Count -Descending | Format-Table Count, Name -AutoSize",
            keywords: "tcp,state,established,listen,狀態,連線"),

        Tweak.Table("net.diag.listening-ports", "Listening ports with process", "監聽埠連程序",
            "Show all listening TCP ports alongside the owning process name and PID.", "顯示所有監聽中嘅 TCP 埠，連埋擁有嘅程序名同 PID 呀。",
            "Show listeners", "顯示監聽", "Get-NetTCPConnection -State Listen | Select-Object LocalAddress, LocalPort, OwningProcess, @{N='Process';E={(Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue).ProcessName}} | Sort-Object LocalPort | Format-Table -AutoSize",
            keywords: "listening,ports,process,監聽,埠,程序"),

        Tweak.Powershell("net.diag.telnet-note", "Telnet port test (note)", "Telnet 埠測試 (備註)",
            "Telnet is the classic way to test a raw TCP port, but the client is off by default. Use Test-NetConnection instead, shown here against port 80.", "Telnet 係測試原始 TCP 埠嘅經典方法，但客戶端預設冇裝。建議改用 Test-NetConnection，呢度示範測 80 埠呀。",
            "Test (TNC)", "測試 (TNC)", "Write-Output 'Telnet client is optional on Windows. Using Test-NetConnection instead:'; Test-NetConnection -ComputerName example.com -Port 80",
            keywords: "telnet,port,tcp,埠,測試,備註"),

        Tweak.Powershell("net.diag.test-connection", "Test-Connection (4 pings)", "Test-Connection (4 次 ping)",
            "Send 4 pings to 1.1.1.1 using the PowerShell Test-Connection cmdlet for object-based results.", "用 PowerShell 嘅 Test-Connection cmdlet 對 1.1.1.1 發 4 次 ping，攞物件式結果呀。",
            "Test connection", "測試連線", "Test-Connection -ComputerName 1.1.1.1 -Count 4",
            keywords: "test-connection,ping,連線,測試"),

        Tweak.Powershell("net.diag.multi-host-latency", "Latency to multiple hosts", "多主機延遲",
            "Measure average round-trip latency to several well-known hosts (Cloudflare, Google, Quad9) at once.", "一次過量度去幾個知名主機 (Cloudflare、Google、Quad9) 嘅平均來回延遲呀。",
            "Measure latency", "量延遲", "foreach ($h in '1.1.1.1','8.8.8.8','9.9.9.9') { $r = Test-Connection -ComputerName $h -Count 4 -ErrorAction SilentlyContinue; if ($r) { $avg = ($r | Measure-Object -Property ResponseTime -Average).Average; Write-Output (\"{0} -> {1:N0} ms avg\" -f $h, $avg) } else { Write-Output \"$h -> no reply\" } }",
            keywords: "latency,multiple,hosts,延遲,多主機"),

        Tweak.Cmd("net.diag.proxy-show", "Show WinHTTP proxy", "顯示 WinHTTP 代理",
            "Display the current system-wide WinHTTP proxy configuration.", "顯示目前全系統嘅 WinHTTP 代理設定呀。",
            "Show proxy", "顯示代理", "netsh winhttp show proxy",
            keywords: "proxy,winhttp,代理,設定"),

        Tweak.Cmd("net.diag.proxy-reset", "Reset WinHTTP proxy", "重設 WinHTTP 代理",
            "Reset the WinHTTP proxy to direct access (no proxy). Requires administrator.", "將 WinHTTP 代理重設為直接連線 (冇代理)，需要管理員權限呀。",
            "Reset proxy", "重設代理", "netsh winhttp reset proxy",
            requiresAdmin: true, destructive: true, keywords: "proxy,reset,winhttp,代理,重設"),

        Tweak.Shell("net.diag.proxy-settings", "Open proxy settings", "開啟代理設定",
            "Open the Windows Settings page for network proxy configuration.", "開啟 Windows 設定入面嘅網絡代理設定頁面呀。",
            "Open settings", "開啟設定", "ms-settings:network-proxy", "",
            keywords: "proxy,settings,代理,設定"),

        Tweak.Table("net.diag.adapter-stats", "Network adapter usage", "網絡卡用量",
            "Show per-adapter sent/received byte statistics via Get-NetAdapterStatistics.", "用 Get-NetAdapterStatistics 顯示每張網絡卡嘅收發位元組統計呀。",
            "Show usage", "顯示用量", "Get-NetAdapterStatistics | Select-Object Name, ReceivedBytes, SentBytes, ReceivedUnicastPackets, SentUnicastPackets | Format-Table -AutoSize",
            keywords: "adapter,statistics,usage,網絡卡,用量,統計"),

        Tweak.Table("net.diag.net-category", "Show network category", "顯示網絡類別",
            "Display each connection profile's network category (Public, Private, or DomainAuthenticated) which governs firewall behaviour.", "顯示每個連線設定檔嘅網絡類別 (公用、私人或網域驗證)，呢個會影響防火牆行為呀。",
            "Show category", "顯示類別", "Get-NetConnectionProfile | Select-Object Name, InterfaceAlias, NetworkCategory, IPv4Connectivity | Format-Table -AutoSize",
            keywords: "network,category,profile,public,private,類別,設定檔"),
    };
}
