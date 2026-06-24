using System;
using System.Collections.Generic;
using WinForge.Models;

namespace WinForge.Catalog;

/// <summary>
/// SSH 工具嘅內建遠端指令目錄 · A catalog of ready-made remote commands for the SSH module.
/// 呢度淨係「資料」（雙語文字 + 一句 POSIX 指令）。實際喺已連線嘅設定檔上面跑（見 SshModule）。
/// Data only: each op is a bilingual label/description plus one POSIX command string that the
/// page runs against the currently-selected, connected profile via SshService.RunCommandAsync.
/// </summary>
public static class SshOperations
{
    /// <summary>一個遠端指令範本 · One remote-command template.</summary>
    public sealed record RemoteOp(
        string Id, LocalizedText Title, LocalizedText Description, string Command, string Keywords)
    {
        public string Haystack =>
            $"{Title.En} {Title.Zh} {Description.En} {Description.Zh} {Command} {Keywords}".ToLowerInvariant();
    }

    public static IReadOnlyList<RemoteOp> All { get; } = new List<RemoteOp>
    {
        new("ssh.op.whoami", new("Who am I", "我係邊個"),
            new("Show the remote user, hostname and uptime.", "顯示遠端使用者、主機名同開機時間。"),
            "echo \"$(whoami)@$(hostname)\"; uptime", "whoami hostname uptime 使用者 主機"),
        new("ssh.op.os", new("OS release", "作業系統版本"),
            new("Print the remote OS name and version (/etc/os-release).", "印出遠端作業系統名稱同版本（/etc/os-release）。"),
            "cat /etc/os-release 2>/dev/null || uname -a", "os release uname version 系統 版本"),
        new("ssh.op.uname", new("Kernel & arch", "核心同架構"),
            new("Show the kernel version and machine architecture.", "顯示核心版本同機器架構。"),
            "uname -a", "uname kernel arch 核心 架構"),
        new("ssh.op.disk", new("Disk usage", "磁碟用量"),
            new("Show mounted filesystems and free space (df -h).", "顯示已掛載檔案系統同剩餘空間（df -h）。"),
            "df -h", "disk df space 磁碟 空間"),
        new("ssh.op.mem", new("Memory & swap", "記憶體同 swap"),
            new("Show RAM and swap usage (free -h).", "顯示記憶體同 swap 用量（free -h）。"),
            "free -h", "memory ram swap free 記憶體"),
        new("ssh.op.top", new("Top processes", "佔用最高程序"),
            new("List the 15 processes using the most CPU.", "列出佔 CPU 最高嘅 15 個程序。"),
            "ps aux --sort=-%cpu | head -n 16", "top ps cpu process 程序"),
        new("ssh.op.uptime", new("Uptime & load", "運行時間同負載"),
            new("Show uptime and load average.", "顯示運行時間同平均負載。"),
            "uptime", "uptime load 負載"),
        new("ssh.op.who", new("Logged-in users", "登入中使用者"),
            new("List users currently logged in (who).", "列出目前登入嘅使用者（who）。"),
            "who", "who users login 登入"),
        new("ssh.op.netstat", new("Listening ports", "監聽中連接埠"),
            new("List listening TCP/UDP ports (ss -tulpn).", "列出監聽中嘅 TCP/UDP 連接埠（ss -tulpn）。"),
            "ss -tulpn 2>/dev/null || netstat -tulpn", "ports listen ss netstat 連接埠"),
        new("ssh.op.ip", new("Network addresses", "網路位址"),
            new("Show network interfaces and IP addresses.", "顯示網路介面同 IP 位址。"),
            "ip a 2>/dev/null || ifconfig", "ip address network interface 網路 位址"),
        new("ssh.op.services", new("Failed services", "失敗服務"),
            new("List systemd services that failed (systemctl --failed).", "列出失敗咗嘅 systemd 服務（systemctl --failed）。"),
            "systemctl --failed 2>/dev/null || echo 'systemd not available'", "systemd services failed 服務"),
        new("ssh.op.updates", new("Pending updates", "待更新套件"),
            new("Check for available package updates (apt/dnf/yum).", "檢查可用嘅套件更新（apt/dnf/yum）。"),
            "(apt list --upgradable 2>/dev/null) || (dnf check-update 2>/dev/null) || (yum check-update 2>/dev/null)", "updates apt dnf yum package 更新 套件"),
        new("ssh.op.authkeys", new("Show authorized_keys", "顯示 authorized_keys"),
            new("Print the remote ~/.ssh/authorized_keys file.", "印出遠端 ~/.ssh/authorized_keys 檔案。"),
            "cat ~/.ssh/authorized_keys 2>/dev/null || echo 'no authorized_keys'", "authorized_keys ssh key 公鑰"),
        new("ssh.op.docker", new("Docker containers", "Docker 容器"),
            new("List running Docker containers (docker ps).", "列出運行中嘅 Docker 容器（docker ps）。"),
            "docker ps 2>/dev/null || echo 'docker not available'", "docker containers ps 容器"),
        new("ssh.op.lastlog", new("Recent logins", "最近登入"),
            new("Show the last 15 login records (last).", "顯示最近 15 條登入記錄（last）。"),
            "last -n 15 2>/dev/null || echo 'last not available'", "last login history 登入 記錄"),
        new("ssh.op.gpu", new("GPU status (nvidia)", "GPU 狀態（nvidia）"),
            new("Show NVIDIA GPU status if nvidia-smi is present.", "如果有 nvidia-smi 就顯示 NVIDIA GPU 狀態。"),
            "nvidia-smi 2>/dev/null || echo 'nvidia-smi not available'", "gpu nvidia smi 顯示卡"),
        new("ssh.op.reboot-check", new("Reboot required?", "需唔需要重開機？"),
            new("Check whether the remote needs a reboot (Debian/Ubuntu).", "檢查遠端需唔需要重開機（Debian/Ubuntu）。"),
            "[ -f /var/run/reboot-required ] && cat /var/run/reboot-required || echo 'No reboot required'", "reboot required 重開機"),
        new("ssh.op.temp", new("CPU temperature", "CPU 溫度"),
            new("Show CPU temperature sensors if available.", "如果有就顯示 CPU 溫度感測器。"),
            "sensors 2>/dev/null || cat /sys/class/thermal/thermal_zone0/temp 2>/dev/null || echo 'no sensors'", "temperature sensors thermal 溫度"),
    };
}
