using System;
using System.Collections.Generic;
using System.Linq;

namespace WinForge.Services;

/// <summary>
/// DNS 記錄參考 · DNS records reference — a fully offline, never-throwing catalogue of ~30 DNS
/// record types with a bilingual purpose blurb and an example zone-file line each, plus a small
/// "which record for which task" cheat-sheet. Pure data; no network, no side-effects.
/// </summary>
public static class DnsRefService
{
    /// <summary>One DNS record type entry. All text is pre-localizable via <see cref="Loc"/>.</summary>
    public sealed class DnsRecord
    {
        public string Type { get; init; } = "";
        public int Code { get; init; }
        public string PurposeEn { get; init; } = "";
        public string PurposeZh { get; init; } = "";
        public string Example { get; init; } = "";

        // Bindable, localized projections for the ListView (classic {Binding}).
        public string Purpose => Loc.I.Pick(PurposeEn, PurposeZh);
        public string CodeText => Code >= 0 ? $"#{Code}" : "";
        public string TypeAndCode => Code >= 0 ? $"{Type}  ·  {CodeText}" : Type;
    }

    /// <summary>One "task → record" cheat-sheet row.</summary>
    public sealed class TaskHint
    {
        public string TaskEn { get; init; } = "";
        public string TaskZh { get; init; } = "";
        public string Record { get; init; } = "";

        public string Task => Loc.I.Pick(TaskEn, TaskZh);
    }

    private static readonly DnsRecord[] _all = new[]
    {
        new DnsRecord { Type = "A", Code = 1, PurposeEn = "Maps a host name to an IPv4 address.", PurposeZh = "將主機名指向一個 IPv4 地址。", Example = "www.example.com.  3600  IN  A     203.0.113.10" },
        new DnsRecord { Type = "AAAA", Code = 28, PurposeEn = "Maps a host name to an IPv6 address.", PurposeZh = "將主機名指向一個 IPv6 地址。", Example = "www.example.com.  3600  IN  AAAA  2001:db8::10" },
        new DnsRecord { Type = "CNAME", Code = 5, PurposeEn = "Aliases one name to another canonical name (no other records may sit beside it).", PurposeZh = "將一個名稱指向另一個正式名稱（同層唔可以有其他記錄）。", Example = "blog.example.com.  3600  IN  CNAME  hosting.example.net." },
        new DnsRecord { Type = "MX", Code = 15, PurposeEn = "Directs email for the domain to mail servers, by priority (lower = preferred).", PurposeZh = "將域名嘅電郵送去郵件伺服器，按優先次序（數字細＝優先）。", Example = "example.com.  3600  IN  MX  10  mail.example.com." },
        new DnsRecord { Type = "TXT", Code = 16, PurposeEn = "Holds arbitrary text — domain verification, SPF, DKIM, DMARC and more.", PurposeZh = "存放任意文字 — 網域驗證、SPF、DKIM、DMARC 等。", Example = "example.com.  3600  IN  TXT  \"v=spf1 include:_spf.example.com -all\"" },
        new DnsRecord { Type = "NS", Code = 2, PurposeEn = "Delegates a zone to its authoritative name servers.", PurposeZh = "將一個區域委派俾佢嘅權威名稱伺服器。", Example = "example.com.  86400  IN  NS  ns1.example.net." },
        new DnsRecord { Type = "SOA", Code = 6, PurposeEn = "Start of Authority — the zone's primary server, admin contact and timers.", PurposeZh = "權威起始 — 區域嘅主伺服器、管理員聯絡同各項計時。", Example = "example.com.  IN  SOA  ns1.example.net. admin.example.com. (2026070101 7200 3600 1209600 3600)" },
        new DnsRecord { Type = "SRV", Code = 33, PurposeEn = "Advertises the host and port of a specific service (priority, weight, port, target).", PurposeZh = "公佈某個服務嘅主機同埠（優先、權重、埠、目標）。", Example = "_sip._tcp.example.com.  3600  IN  SRV  10 60 5060 sipserver.example.com." },
        new DnsRecord { Type = "PTR", Code = 12, PurposeEn = "Reverse DNS — maps an IP address back to a host name.", PurposeZh = "反向 DNS — 將 IP 地址對返去主機名。", Example = "10.113.0.203.in-addr.arpa.  3600  IN  PTR  www.example.com." },
        new DnsRecord { Type = "CAA", Code = 257, PurposeEn = "Lists which certificate authorities may issue certs for the domain.", PurposeZh = "列明邊啲憑證機構先可以為呢個域名簽發憑證。", Example = "example.com.  3600  IN  CAA  0 issue \"letsencrypt.org\"" },
        new DnsRecord { Type = "DNSKEY", Code = 48, PurposeEn = "Publishes a DNSSEC public key used to verify signatures in the zone.", PurposeZh = "公佈用嚟核實區域簽名嘅 DNSSEC 公鑰。", Example = "example.com.  3600  IN  DNSKEY  257 3 13 mdsswUyr3D...==" },
        new DnsRecord { Type = "DS", Code = 43, PurposeEn = "Delegation Signer — links a child zone's DNSKEY into the parent for DNSSEC.", PurposeZh = "委派簽署者 — 為 DNSSEC 將子區域嘅 DNSKEY 連上父區域。", Example = "example.com.  86400  IN  DS  60485 13 2 D4B7D520E7BB5F0F..." },
        new DnsRecord { Type = "RRSIG", Code = 46, PurposeEn = "The DNSSEC signature over a record set, proving it is authentic.", PurposeZh = "DNSSEC 對某組記錄嘅簽名，證明佢係真確。", Example = "example.com.  3600  IN  RRSIG  A 13 2 3600 20260801000000 ..." },
        new DnsRecord { Type = "NSEC", Code = 47, PurposeEn = "DNSSEC authenticated denial-of-existence — proves a name/type does not exist.", PurposeZh = "DNSSEC 認證式否認 — 證明某名稱／類型並不存在。", Example = "alpha.example.com.  3600  IN  NSEC  beta.example.com. A RRSIG NSEC" },
        new DnsRecord { Type = "TLSA", Code = 52, PurposeEn = "DANE — pins a TLS certificate or key to a name via DNSSEC.", PurposeZh = "DANE — 透過 DNSSEC 將 TLS 憑證或金鑰綁定到某名稱。", Example = "_443._tcp.www.example.com.  3600  IN  TLSA  3 1 1 0b9fa5a59eed715c..." },
        new DnsRecord { Type = "SSHFP", Code = 44, PurposeEn = "Publishes an SSH host key fingerprint for verification.", PurposeZh = "公佈 SSH 主機金鑰指紋以供核實。", Example = "host.example.com.  3600  IN  SSHFP  4 2 123456789abcdef..." },
        new DnsRecord { Type = "SPF", Code = 99, PurposeEn = "Legacy SPF record type (now use TXT). Lists permitted mail senders.", PurposeZh = "舊式 SPF 記錄類型（而家用 TXT）。列明獲准嘅寄件伺服器。", Example = "example.com.  3600  IN  TXT  \"v=spf1 ip4:203.0.113.0/24 -all\"" },
        new DnsRecord { Type = "DKIM", Code = -1, PurposeEn = "DomainKeys — a TXT record at a selector holding the email signing public key.", PurposeZh = "DomainKeys — 放喺選擇器下嘅 TXT 記錄，存放電郵簽名公鑰。", Example = "sel1._domainkey.example.com.  IN  TXT  \"v=DKIM1; k=rsa; p=MIGfMA0G...\"" },
        new DnsRecord { Type = "DMARC", Code = -1, PurposeEn = "A TXT record at _dmarc setting the policy for failed SPF/DKIM mail.", PurposeZh = "放喺 _dmarc 嘅 TXT 記錄，設定 SPF／DKIM 失敗郵件嘅處理政策。", Example = "_dmarc.example.com.  IN  TXT  \"v=DMARC1; p=quarantine; rua=mailto:dmarc@example.com\"" },
        new DnsRecord { Type = "NAPTR", Code = 35, PurposeEn = "Naming Authority Pointer — rule-based rewriting for ENUM / SIP discovery.", PurposeZh = "命名權威指標 — 用規則改寫，供 ENUM／SIP 探索使用。", Example = "example.com.  3600  IN  NAPTR  100 10 \"U\" \"E2U+sip\" \"!^.*$!sip:info@example.com!\" ." },
        new DnsRecord { Type = "HTTPS", Code = 65, PurposeEn = "SVCB for HTTPS — advertises ALPN, port and hints so clients connect faster.", PurposeZh = "HTTPS 版 SVCB — 公佈 ALPN、埠同提示，令客戶端連接更快。", Example = "example.com.  3600  IN  HTTPS  1 . alpn=\"h3,h2\" ipv4hint=203.0.113.10" },
        new DnsRecord { Type = "SVCB", Code = 64, PurposeEn = "Service Binding — generic service parameters and endpoint hints for a name.", PurposeZh = "服務綁定 — 為某名稱提供通用服務參數同端點提示。", Example = "_foo.example.com.  3600  IN  SVCB  1 svc.example.net. port=8080" },
        new DnsRecord { Type = "ALIAS", Code = -1, PurposeEn = "Provider CNAME-flattening — CNAME-like behaviour that is legal at the zone apex.", PurposeZh = "供應商嘅 CNAME 扁平化 — 似 CNAME，但可以用喺區域頂點。", Example = "example.com.  3600  IN  ALIAS  target.hosting.net." },
        new DnsRecord { Type = "ANAME", Code = -1, PurposeEn = "Same idea as ALIAS under a different vendor name — apex CNAME flattening.", PurposeZh = "同 ALIAS 一樣，只係唔同供應商叫法 — 頂點 CNAME 扁平化。", Example = "example.com.  3600  IN  ANAME  target.hosting.net." },
        new DnsRecord { Type = "CERT", Code = 37, PurposeEn = "Stores a certificate or certificate-revocation list in the DNS.", PurposeZh = "喺 DNS 內存放憑證或憑證撤銷清單。", Example = "example.com.  3600  IN  CERT  1 0 0 MIICajCCAdOgAwIBAgIC..." },
        new DnsRecord { Type = "LOC", Code = 29, PurposeEn = "Encodes a geographic location (lat/long/altitude) for a name.", PurposeZh = "為某名稱記錄地理位置（緯度／經度／海拔）。", Example = "example.com.  3600  IN  LOC  52 22 23.000 N 4 53 32.000 E -2.00m" },
        new DnsRecord { Type = "HINFO", Code = 13, PurposeEn = "Host information — CPU and OS strings (often used to blunt ANY queries).", PurposeZh = "主機資訊 — CPU 同作業系統字串（常用嚟弱化 ANY 查詢）。", Example = "example.com.  3600  IN  HINFO  \"RFC8482\" \"\"" },
        new DnsRecord { Type = "RP", Code = 17, PurposeEn = "Responsible Person — a contact mailbox and TXT reference for the name.", PurposeZh = "負責人 — 該名稱嘅聯絡信箱同 TXT 參照。", Example = "example.com.  3600  IN  RP  admin.example.com. contact.example.com." },
        new DnsRecord { Type = "URI", Code = 256, PurposeEn = "Maps a name to a URI with priority and weight (service discovery).", PurposeZh = "將名稱對應到一個 URI，附優先同權重（服務探索）。", Example = "_http._tcp.example.com.  3600  IN  URI  10 1 \"https://www.example.com/\"" },
        new DnsRecord { Type = "OPENPGPKEY", Code = 61, PurposeEn = "Publishes an OpenPGP public key for an email address (DANE for PGP).", PurposeZh = "為某電郵地址公佈 OpenPGP 公鑰（PGP 版 DANE）。", Example = "hash._openpgpkey.example.com.  IN  OPENPGPKEY  mQENBF...==" },
        new DnsRecord { Type = "SMIMEA", Code = 53, PurposeEn = "Associates an S/MIME certificate with an email address via DNSSEC.", PurposeZh = "透過 DNSSEC 將 S/MIME 憑證同某電郵地址關聯。", Example = "hash._smimecert.example.com.  IN  SMIMEA  3 0 0 308202..." },
        new DnsRecord { Type = "CDS", Code = 59, PurposeEn = "Child copy of a DS record used to automate DNSSEC key rollover to the parent.", PurposeZh = "DS 記錄嘅子區域副本，用嚟自動將 DNSSEC 換鑰交上父區域。", Example = "example.com.  3600  IN  CDS  60485 13 2 D4B7D520E7BB5F0F..." },
        new DnsRecord { Type = "DNAME", Code = 39, PurposeEn = "Redirects an entire subtree of names to another domain.", PurposeZh = "將成個名稱子樹重新導向去另一個域名。", Example = "old.example.com.  3600  IN  DNAME  new.example.net." },
    };

    private static readonly TaskHint[] _hints = new[]
    {
        new TaskHint { TaskEn = "Verify domain ownership", TaskZh = "驗證域名擁有權", Record = "TXT" },
        new TaskHint { TaskEn = "Route incoming mail", TaskZh = "接收電郵", Record = "MX" },
        new TaskHint { TaskEn = "Point a subdomain at another name", TaskZh = "將子域名指向另一名稱", Record = "CNAME" },
        new TaskHint { TaskEn = "Point the root domain (apex) at a host", TaskZh = "將根域名（頂點）指向主機", Record = "A / AAAA / ALIAS" },
        new TaskHint { TaskEn = "Stop mail spoofing", TaskZh = "防止電郵假冒", Record = "TXT (SPF / DKIM / DMARC)" },
        new TaskHint { TaskEn = "Restrict who can issue TLS certs", TaskZh = "限制邊個可簽發 TLS 憑證", Record = "CAA" },
        new TaskHint { TaskEn = "Delegate a subdomain's DNS", TaskZh = "委派子域名嘅 DNS", Record = "NS" },
        new TaskHint { TaskEn = "Advertise a service host + port", TaskZh = "公佈服務主機同埠", Record = "SRV" },
        new TaskHint { TaskEn = "Set up reverse DNS for an IP", TaskZh = "為 IP 設定反向 DNS", Record = "PTR" },
        new TaskHint { TaskEn = "Speed up HTTPS with ALPN hints", TaskZh = "用 ALPN 提示加快 HTTPS", Record = "HTTPS / SVCB" },
        new TaskHint { TaskEn = "Sign a zone with DNSSEC", TaskZh = "用 DNSSEC 簽署區域", Record = "DNSKEY / DS / RRSIG" },
    };

    /// <summary>Distinct filter categories for the ComboBox (localized label pairs handled by caller).</summary>
    public static IReadOnlyList<string> Categories { get; } = new[]
    {
        "All", "Addressing", "Mail", "Security / DNSSEC", "Service", "Modern",
    };

    private static readonly Dictionary<string, string[]> _categoryMembers = new()
    {
        ["Addressing"] = new[] { "A", "AAAA", "CNAME", "NS", "SOA", "PTR", "ALIAS", "ANAME", "DNAME" },
        ["Mail"] = new[] { "MX", "TXT", "SPF", "DKIM", "DMARC" },
        ["Security / DNSSEC"] = new[] { "DNSKEY", "DS", "RRSIG", "NSEC", "TLSA", "SSHFP", "CAA", "CERT", "OPENPGPKEY", "SMIMEA", "CDS" },
        ["Service"] = new[] { "SRV", "NAPTR", "URI", "LOC", "HINFO", "RP" },
        ["Modern"] = new[] { "HTTPS", "SVCB", "ALIAS", "ANAME" },
    };

    /// <summary>All records, unfiltered. Never null.</summary>
    public static IReadOnlyList<DnsRecord> All => _all;

    /// <summary>The task → record cheat-sheet. Never null.</summary>
    public static IReadOnlyList<TaskHint> Hints => _hints;

    /// <summary>
    /// Filter by a free-text query (matched against type, code and both purpose blurbs) and an
    /// optional category. Never throws; a bad/empty query returns the full (category) list.
    /// </summary>
    public static List<DnsRecord> Search(string? query, string? category)
    {
        try
        {
            IEnumerable<DnsRecord> src = _all;

            if (!string.IsNullOrWhiteSpace(category) &&
                !category.Equals("All", StringComparison.OrdinalIgnoreCase) &&
                _categoryMembers.TryGetValue(category, out var members))
            {
                var set = new HashSet<string>(members, StringComparer.OrdinalIgnoreCase);
                src = src.Where(r => set.Contains(r.Type));
            }

            var q = query?.Trim();
            if (!string.IsNullOrEmpty(q))
            {
                src = src.Where(r =>
                    r.Type.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    r.CodeText.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    r.Code.ToString().Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    r.PurposeEn.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    r.PurposeZh.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    r.Example.Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            return src.ToList();
        }
        catch
        {
            return _all.ToList();
        }
    }
}
