using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace WinForge.Services;

public sealed record HaAcDefenderRequest(
    string RepositoryPath,
    string ProjectName,
    string ClimateEntity,
    double CoolAboveC,
    double HeatBelowC,
    int PollSeconds,
    bool DryRun);

public sealed record HaAcDefenderArtifacts(string Folder, string Dockerfile, string Compose, string App, string Readme, string DeployScript);

/// <summary>
/// Home Assistant AC Defender Docker deployment generator.
/// Creates a portable, no-secret bundle: runtime secrets stay as environment variables.
/// </summary>
public sealed class HomeAssistantAcDefenderService
{
    public const string KeyRepo = "ha.acdefender.repo";
    public const string DefaultProjectName = "ha-ac-defender";

    public string RepositoryPath
    {
        get => SettingsStore.Get(KeyRepo, "");
        set => SettingsStore.Set(KeyRepo, value ?? "");
    }

    public static string GitHubRoot()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GitHub");

    public static string DeploymentFolder(string repo)
        => Path.Combine(repo, "deploy", "home-assistant-ac-defender");

    public static string? LocateCandidate()
    {
        var root = GitHubRoot();
        if (!Directory.Exists(root)) return null;
        var preferred = Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly)
            .Select(p => new DirectoryInfo(p))
            .Where(d => d.Name.Contains("ac", StringComparison.OrdinalIgnoreCase)
                && d.Name.Contains("defender", StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.Name.Length)
            .FirstOrDefault();
        if (preferred is not null) return preferred.FullName;

        return Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly)
            .Select(p => new DirectoryInfo(p))
            .Where(d => d.Name.Contains("home", StringComparison.OrdinalIgnoreCase)
                || d.Name.Contains("assistant", StringComparison.OrdinalIgnoreCase)
                || d.Name.Contains("ha", StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.Name.Length)
            .Select(d => d.FullName)
            .FirstOrDefault();
    }

    public HaAcDefenderArtifacts Generate(HaAcDefenderRequest req)
    {
        var repo = Path.GetFullPath((req.RepositoryPath ?? "").Trim());
        if (!Directory.Exists(repo)) throw new DirectoryNotFoundException(repo);
        RepositoryPath = repo;

        var folder = DeploymentFolder(repo);
        Directory.CreateDirectory(folder);

        var app = Path.Combine(folder, "ac_defender.py");
        var dockerfile = Path.Combine(folder, "Dockerfile");
        var compose = Path.Combine(folder, "docker-compose.yml");
        var readme = Path.Combine(folder, "README.md");
        var deploy = Path.Combine(folder, "deploy-ssh.sh");

        File.WriteAllText(app, BuildPython(req), Encoding.UTF8);
        File.WriteAllText(dockerfile, BuildDockerfile(), Encoding.UTF8);
        File.WriteAllText(compose, BuildCompose(req), Encoding.UTF8);
        File.WriteAllText(readme, BuildReadme(req), Encoding.UTF8);
        File.WriteAllText(deploy, BuildDeployScript(req), Encoding.UTF8);

        return new HaAcDefenderArtifacts(folder, dockerfile, compose, app, readme, deploy);
    }

    public string ExportBundle(HaAcDefenderRequest req, string zipPath)
    {
        var artifacts = Generate(req);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(zipPath)) ?? ".");
        if (File.Exists(zipPath)) File.Delete(zipPath);
        ZipFile.CreateFromDirectory(artifacts.Folder, zipPath, CompressionLevel.Optimal, includeBaseDirectory: true);
        return zipPath;
    }

    public static string ProjectName(string raw)
    {
        var s = new string((raw ?? DefaultProjectName).ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        while (s.Contains("--", StringComparison.Ordinal)) s = s.Replace("--", "-", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(s) ? DefaultProjectName : s;
    }

    private static string BuildDockerfile() =>
        """
        FROM python:3.12-slim
        WORKDIR /app
        COPY ac_defender.py /app/ac_defender.py
        ENV PYTHONUNBUFFERED=1
        CMD ["python", "/app/ac_defender.py"]
        """;

    private static string BuildCompose(HaAcDefenderRequest req)
    {
        var project = ProjectName(req.ProjectName);
        var cool = req.CoolAboveC.ToString("0.##", CultureInfo.InvariantCulture);
        var heat = req.HeatBelowC.ToString("0.##", CultureInfo.InvariantCulture);
        var poll = Math.Clamp(req.PollSeconds, 10, 3600).ToString(CultureInfo.InvariantCulture);
        var dry = req.DryRun ? "1" : "0";
        return $$"""
        services:
          ac-defender:
            image: python:3.12-slim
            working_dir: /app
            command: python /app/ac_defender.py
            environment:
              HA_URL: ${HA_URL}
              HA_TOKEN: ${HA_TOKEN}
              CLIMATE_ENTITY: "{{Escape(req.ClimateEntity)}}"
              COOL_ABOVE_C: "{{cool}}"
              HEAT_BELOW_C: "{{heat}}"
              POLL_SECONDS: "{{poll}}"
              DRY_RUN: "{{dry}}"
            volumes:
              - ./ac_defender.py:/app/ac_defender.py:ro
            restart: unless-stopped
        """;
    }

    private static string BuildReadme(HaAcDefenderRequest req) =>
        $$"""
        # Home Assistant AC Defender

        Generated by WinForge. This bundle watches one Home Assistant climate entity and turns HVAC off
        when the reported temperature crosses the configured guard band.

        ## Files

        - `ac_defender.py` - runtime watcher using Python standard libraries only.
        - `Dockerfile` - optional image build for remote hosts.
        - `docker-compose.yml` - local/remote compose deployment.
        - `deploy-ssh.sh` - helper commands to run on a remote Docker host after upload.

        ## Configure

        Set secrets on the host, not in these files:

        ```sh
        export HA_URL="http://homeassistant.local:8123"
        export HA_TOKEN="your-long-lived-access-token"
        ```

        Defaults in this bundle:

        - Climate entity: `{{Escape(req.ClimateEntity)}}`
        - Cool-above trip: `{{req.CoolAboveC.ToString("0.##", CultureInfo.InvariantCulture)}} C`
        - Heat-below trip: `{{req.HeatBelowC.ToString("0.##", CultureInfo.InvariantCulture)}} C`
        - Poll interval: `{{Math.Clamp(req.PollSeconds, 10, 3600)}} s`
        - Dry run: `{{(req.DryRun ? "on" : "off")}}`
        """;

    private static string BuildDeployScript(HaAcDefenderRequest req)
    {
        var project = ProjectName(req.ProjectName);
        return $$"""
        #!/usr/bin/env sh
        set -eu
        : "${HA_URL:?Set HA_URL first}"
        : "${HA_TOKEN:?Set HA_TOKEN first}"
        docker compose -p {{project}} up -d
        docker compose -p {{project}} ps
        """;
    }

    private static string BuildPython(HaAcDefenderRequest req)
    {
        var cool = req.CoolAboveC.ToString("0.###", CultureInfo.InvariantCulture);
        var heat = req.HeatBelowC.ToString("0.###", CultureInfo.InvariantCulture);
        var poll = Math.Clamp(req.PollSeconds, 10, 3600).ToString(CultureInfo.InvariantCulture);
        var dry = req.DryRun ? "1" : "0";
        return $$"""
        import json
        import os
        import time
        import urllib.error
        import urllib.request

        HA_URL = os.environ.get("HA_URL", "").rstrip("/")
        HA_TOKEN = os.environ.get("HA_TOKEN", "")
        CLIMATE_ENTITY = os.environ.get("CLIMATE_ENTITY", "{{Escape(req.ClimateEntity)}}")
        COOL_ABOVE_C = float(os.environ.get("COOL_ABOVE_C", "{{cool}}"))
        HEAT_BELOW_C = float(os.environ.get("HEAT_BELOW_C", "{{heat}}"))
        POLL_SECONDS = max(10, int(os.environ.get("POLL_SECONDS", "{{poll}}")))
        DRY_RUN = os.environ.get("DRY_RUN", "{{dry}}").lower() in ("1", "true", "yes", "on")

        def request(method, path, body=None):
            if not HA_URL or not HA_TOKEN:
                raise RuntimeError("HA_URL and HA_TOKEN are required")
            data = None if body is None else json.dumps(body).encode("utf-8")
            req = urllib.request.Request(HA_URL + path, data=data, method=method)
            req.add_header("Authorization", "Bearer " + HA_TOKEN)
            req.add_header("Accept", "application/json")
            if data is not None:
                req.add_header("Content-Type", "application/json")
            with urllib.request.urlopen(req, timeout=20) as resp:
                raw = resp.read().decode("utf-8")
                return json.loads(raw) if raw else {}

        def current_temperature(state):
            attrs = state.get("attributes", {})
            for key in ("current_temperature", "temperature"):
                value = attrs.get(key)
                try:
                    return float(value)
                except (TypeError, ValueError):
                    pass
            return None

        def turn_off(reason):
            print(reason, flush=True)
            if DRY_RUN:
                print("dry-run: climate.turn_off skipped", flush=True)
                return
            request("POST", "/api/services/climate/turn_off", {"entity_id": CLIMATE_ENTITY})
            request("POST", "/api/events/winforge_ac_defender_trip", {
                "entity_id": CLIMATE_ENTITY,
                "reason": reason,
                "source": "winforge"
            })

        print("WinForge AC Defender started for " + CLIMATE_ENTITY, flush=True)
        while True:
            try:
                state = request("GET", "/api/states/" + CLIMATE_ENTITY)
                temp = current_temperature(state)
                mode = str(state.get("state", "unknown"))
                print("state=%s current_temperature=%s" % (mode, temp), flush=True)
                if temp is not None and mode in ("cool", "heat", "heat_cool", "auto"):
                    if mode == "cool" and temp >= COOL_ABOVE_C:
                        turn_off("cooling guard tripped at %.2f C" % temp)
                    elif mode == "heat" and temp <= HEAT_BELOW_C:
                        turn_off("heating guard tripped at %.2f C" % temp)
                    elif mode in ("heat_cool", "auto") and (temp >= COOL_ABOVE_C or temp <= HEAT_BELOW_C):
                        turn_off("auto guard tripped at %.2f C" % temp)
            except urllib.error.HTTPError as ex:
                print("http error %s: %s" % (ex.code, ex.read().decode("utf-8", "replace")), flush=True)
            except Exception as ex:
                print("error: " + str(ex), flush=True)
            time.sleep(POLL_SECONDS)
        """;
    }

    private static string Escape(string s) => (s ?? "").Replace("\"", "\\\"", StringComparison.Ordinal);
}
