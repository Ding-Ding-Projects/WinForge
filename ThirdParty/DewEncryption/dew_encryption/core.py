from __future__ import annotations

import argparse
import datetime as dt
import fnmatch
import getpass
import hashlib
import json
import os
import platform
import shutil
import subprocess
import sys
import tempfile
import time
import zipfile
import urllib.error
import urllib.request
from dataclasses import dataclass
from dataclasses import asdict
from dataclasses import field
from pathlib import Path


APP_NAME = "Dew Encryption"
REPO_DIR_NAME = ".dew-encryption-repo"
ARCHIVE_DIR_NAME = "Dew Encryption Archives"
VERACRYPT_EXT = ".dew.hc"
DEW_DRIVE_PAYLOAD_NAME = "payload.enc"
DEW_DRIVE_METADATA_NAME = "metadata.json"
DEW_DRIVE_PRIVATE_METADATA_NAME = ".dew-drive-metadata.json"
DEW_DRIVE_METADATA_FORMAT = "dew-drive/2"


def app_base_dir() -> Path:
    if getattr(sys, "frozen", False):
        return Path(sys.executable).resolve().parent
    return Path(__file__).resolve().parent.parent


def config_file() -> Path:
    explicit = os.environ.get("DEW_ENCRYPTION_CONFIG")
    if explicit:
        return Path(explicit).expanduser().resolve()
    base = app_base_dir()
    if os.environ.get("DEW_ENCRYPTION_PORTABLE") == "1" or (base / "portable.flag").exists():
        return base / "settings.json"
    return Path(os.environ.get("APPDATA") or Path.home() / ".config") / "DewEncryption" / "settings.json"


class DewError(RuntimeError):
    pass


@dataclass(frozen=True)
class DewResult:
    source: Path
    repo: Path
    archive: Path
    commit: str


@dataclass(frozen=True)
class HistoryEntry:
    commit: str
    author_date: str
    subject: str


@dataclass(frozen=True)
class FileChange:
    status: str
    path: str




@dataclass
class DewDriveProfile:
    registry_ref: str

@dataclass
class VeraCryptSettings:
    encryption: str = "AES"
    hash: str = "SHA-512"
    filesystem: str = "exFAT"
    pim: str = "0"
    size_padding_mb: int = 64
    size_multiplier: float = 1.25
    minimum_size_mb: int = 32
    keep_source_after_encrypt: bool = False
    keep_container_after_decrypt: bool = True
    veracrypt_path: str = ""


@dataclass
class ThemeSettings:
    background: str = "#0f172a"
    foreground: str = "#e5e7eb"
    accent: str = "#38bdf8"
    font_family: str = "Segoe UI"
    font_size: int = 10


@dataclass
class HookAction:
    name: str = "New action"
    event: str = "open"
    kind: str = "script"
    target: str = ""
    payload: str = ""
    enabled: bool = True


@dataclass
class DewDriveProfile:
    name: str = "Default"
    local_path: str = ""
    registry: str = ""
    auto_push: bool = False
    encryption_mode: str = "7z"
    include_patterns: list[str] | None = None
    exclude_patterns: list[str] | None = None


@dataclass
class ContainerProfile:
    name: str = "Default"
    path: str = ""
    mount_path: str = ""
    theme: ThemeSettings | None = None
    hooks: list[HookAction] | None = None


@dataclass
class DewDriveProfile:
    name: str = "Default"
    local_path: str = ""
    folder: str = ""
    registry_ref: str = ""
    registry_image: str = ""
    encryption_mode: str = "7zip"
    last_sync: str = ""
    auto_push: bool = False
    include_patterns: list[str] = field(default_factory=list)
    exclude_patterns: list[str] = field(default_factory=list)


@dataclass
class DewDriveSettings:
    docker_path: str = ""
    default_registry: str = ""
    drives: list[DewDriveProfile] = field(default_factory=list)

    def __iter__(self):
        return iter(self.drives)

    def __len__(self) -> int:
        return len(self.drives)

    def __getitem__(self, index: int) -> DewDriveProfile:
        return self.drives[index]

    def __setitem__(self, index: int, value: DewDriveProfile) -> None:
        self.drives[index] = value

    def append(self, value: DewDriveProfile) -> None:
        self.drives.append(value)


@dataclass
class AppSettings:
    veracrypt: VeraCryptSettings
    containers: list[ContainerProfile] | None = None
    dew_drives: DewDriveSettings | None = None


def default_settings() -> AppSettings:
    return AppSettings(veracrypt=VeraCryptSettings(), containers=[], dew_drives=DewDriveSettings())


def _string_list(value: object) -> list[str]:
    if isinstance(value, str):
        return [item.strip() for item in value.replace("\n", ",").split(",") if item.strip()]
    if not isinstance(value, list):
        return []
    return [str(item) for item in value]


def load_settings() -> AppSettings:
    path = config_file()
    if not path.exists():
        return default_settings()
    try:
        raw = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return default_settings()
    vc_raw = raw.get("veracrypt", {}) if isinstance(raw, dict) else {}
    defaults = VeraCryptSettings()
    values = asdict(defaults)
    values.update({key: value for key, value in vc_raw.items() if key in values})
    drive_profiles: list[DewDriveProfile] = []
    for profile_raw in raw.get("dew_drive", []) if isinstance(raw, dict) else []:
        if not isinstance(profile_raw, dict):
            continue
        drive_values = asdict(DewDriveProfile())
        drive_values.update({key: value for key, value in profile_raw.items() if key in drive_values})
        drive_values["include_patterns"] = list(drive_values.get("include_patterns") or [])
        drive_values["exclude_patterns"] = list(drive_values.get("exclude_patterns") or [])
        drive_profiles.append(DewDriveProfile(**drive_values))

    profiles: list[ContainerProfile] = []
    for profile_raw in raw.get("containers", []) if isinstance(raw, dict) else []:
        if not isinstance(profile_raw, dict):
            continue
        theme_raw = profile_raw.get("theme") if isinstance(profile_raw.get("theme"), dict) else {}
        theme_values = asdict(ThemeSettings())
        theme_values.update({key: value for key, value in theme_raw.items() if key in theme_values})
        hooks: list[HookAction] = []
        for hook_raw in profile_raw.get("hooks", []):
            if isinstance(hook_raw, dict):
                hook_values = asdict(HookAction())
                hook_values.update({key: value for key, value in hook_raw.items() if key in hook_values})
                hooks.append(HookAction(**hook_values))
        profiles.append(ContainerProfile(
            name=str(profile_raw.get("name") or "Default"),
            path=str(profile_raw.get("path") or ""),
            mount_path=str(profile_raw.get("mount_path") or ""),
            theme=ThemeSettings(**theme_values),
            hooks=hooks,
        ))
    dew_drives_raw = raw.get("dew_drives", {}) if isinstance(raw, dict) else {}
    dew_drive_values = asdict(DewDriveSettings())
    if isinstance(dew_drives_raw, dict):
        dew_drive_values.update({
            key: value
            for key, value in dew_drives_raw.items()
            if key in dew_drive_values and key != "drives"
        })
    drives: list[DewDriveProfile] = []
    drives_raw = dew_drives_raw.get("drives", []) if isinstance(dew_drives_raw, dict) else []
    for drive_raw in drives_raw:
        if not isinstance(drive_raw, dict):
            continue
        drive_values = asdict(DewDriveProfile())
        drive_values.update({
            key: value
            for key, value in drive_raw.items()
            if key in drive_values and key not in {"include_patterns", "exclude_patterns"}
        })
        drive_values["include_patterns"] = _string_list(drive_raw.get("include_patterns", []))
        drive_values["exclude_patterns"] = _string_list(drive_raw.get("exclude_patterns", []))
        drives.append(DewDriveProfile(**drive_values))
    if not drives:
        drives = drive_profiles
    dew_drive_values["drives"] = drives
    return AppSettings(
        veracrypt=VeraCryptSettings(**values),
        containers=profiles,
        dew_drives=DewDriveSettings(**dew_drive_values),
    )


def save_settings(settings: AppSettings) -> None:
    path = config_file()
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(asdict(settings), indent=2), encoding="utf-8")


def find_executable(name: str, fallbacks: list[Path] | None = None) -> Path:
    found = shutil.which(name)
    if found:
        return Path(found)
    for candidate in fallbacks or []:
        if candidate.exists():
            return candidate
    raise DewError(f"Required executable not found: {name}")


def find_veracrypt() -> Path:
    configured = load_settings().veracrypt.veracrypt_path
    if configured and Path(configured).exists():
        return Path(configured)
    names = ["VeraCrypt.exe", "veracrypt.exe"] if os.name == "nt" else ["veracrypt"]
    for name in names:
        found = shutil.which(name)
        if found:
            return Path(found)
    fallbacks = [
        Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "VeraCrypt" / "VeraCrypt.exe",
        Path(os.environ.get("ProgramFiles(x86)", "C:/Program Files (x86)")) / "VeraCrypt" / "VeraCrypt.exe",
        Path("/usr/bin/veracrypt"),
        Path("/usr/local/bin/veracrypt"),
    ]
    for candidate in fallbacks:
        if candidate.exists():
            return candidate
    raise DewError("VeraCrypt was not found. Install VeraCrypt and make sure its CLI is available.")


def find_veracrypt_format() -> Path:
    """Volume creation on Windows is handled by "VeraCrypt Format.exe", not VeraCrypt.exe."""
    vc = find_veracrypt()
    if os.name != "nt":
        return vc
    candidates = [
        vc.parent / "VeraCrypt Format.exe",
        Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "VeraCrypt" / "VeraCrypt Format.exe",
        Path(os.environ.get("ProgramFiles(x86)", "C:/Program Files (x86)")) / "VeraCrypt" / "VeraCrypt Format.exe",
    ]
    for candidate in candidates:
        if candidate.exists():
            return candidate
    raise DewError("VeraCrypt Format.exe was not found. Reinstall VeraCrypt to create containers on Windows.")




def find_docker() -> Path:
    docker = shutil.which("docker")
    if docker:
        return Path(docker)
    fallbacks = [
        Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "Docker" / "Docker" / "resources" / "bin" / "docker.exe",
        Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "Docker" / "Docker" / "resources" / "docker.exe",
        Path("/usr/bin/docker"),
        Path("/usr/local/bin/docker"),
    ]
    for candidate in fallbacks:
        if candidate.exists():
            return candidate
    raise DewError("Docker was not found. Install Docker and make sure its CLI is available.")


def run_docker(args: list[str]) -> str:
    return run([str(find_docker()), *args])


def dew_drive_image_ref(profile: DewDriveProfile) -> str:
    registry_ref = profile.registry_ref.strip()
    if not registry_ref:
        raise DewError("Dew Drive registry reference is empty.")
    return registry_ref


def build_dew_drive_image(profile: DewDriveProfile, encrypted_payload: Path, metadata: dict[str, object]) -> str:
    registry_ref = dew_drive_image_ref(profile)
    encrypted_payload = Path(encrypted_payload).expanduser().resolve()
    if not encrypted_payload.exists():
        raise DewError(f"Encrypted payload does not exist: {encrypted_payload}")
    with tempfile.TemporaryDirectory(prefix="dew-drive-build-") as temp_dir:
        context = Path(temp_dir)
        dew_drive = context / "dew-drive"
        dew_drive.mkdir(parents=True, exist_ok=True)
        payload_target = dew_drive / encrypted_payload.name
        if encrypted_payload.is_dir():
            shutil.copytree(encrypted_payload, payload_target)
        else:
            shutil.copy2(encrypted_payload, payload_target)
        (dew_drive / "metadata.json").write_text(json.dumps(metadata, indent=2, sort_keys=True), encoding="utf-8")
        (context / "Dockerfile").write_text("FROM scratch\nCOPY dew-drive /dew-drive\n", encoding="utf-8")
        return run_docker(["build", "-t", registry_ref, str(context)])


def push_dew_drive(profile: DewDriveProfile) -> str:
    return run_docker(["push", dew_drive_image_ref(profile)])


def pull_dew_drive(registry_ref: str, output_dir: Path) -> Path:
    registry_ref = registry_ref.strip()
    if not registry_ref:
        raise DewError("Dew Drive registry reference is empty.")
    output_dir = Path(output_dir).expanduser().resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    run_docker(["pull", registry_ref])
    container = run_docker(["create", registry_ref])
    try:
        run_docker(["cp", f"{container}:/dew-drive", str(output_dir)])
    finally:
        run_docker(["rm", container])
    return output_dir / "dew-drive"


def log_step(message: str) -> None:
    """Aggressive diagnostic logging: goes to stderr so GUIs relay it without polluting stdout."""
    print(f"[dew] {message}", file=sys.stderr, flush=True)


def redact_command(cmd: list[str]) -> str:
    """Render a command for error messages with password/PIM values masked."""
    redacted: list[str] = []
    hide_next = False
    for part in cmd:
        if hide_next:
            redacted.append("****")
            hide_next = False
            continue
        lowered = part.lower()
        if lowered in {"/password", "--password", "/pim", "--pim"}:
            redacted.append(part)
            hide_next = True
            continue
        if lowered.startswith("-p") and not lowered.startswith("--") and len(part) > 2:
            redacted.append("-p****")
            continue
        redacted.append(part)
    return " ".join(redacted)


def command_passwords(cmd: list[str]) -> list[str]:
    passwords: list[str] = []
    take_next = False
    for part in cmd:
        if take_next:
            passwords.append(part)
            take_next = False
            continue
        lowered = part.lower()
        if lowered in {"/password", "--password"}:
            take_next = True
            continue
        if lowered.startswith("-p") and not lowered.startswith("--") and len(part) > 2:
            passwords.append(part[2:])
    return [password for password in passwords if len(password) >= 4]


def redact_output(text: str, cmd: list[str]) -> str:
    for password in command_passwords(cmd):
        text = text.replace(password, "****")
    return text


def run(cmd: list[str], cwd: Path | None = None, input_text: str | None = None) -> str:
    log_step(f"run: {redact_command(cmd)}" + (f" (cwd {cwd})" if cwd else ""))
    started = time.monotonic()
    proc = subprocess.run(
        cmd,
        cwd=str(cwd) if cwd else None,
        text=True,
        input=input_text,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        check=False,
    )
    log_step(f"exit {proc.returncode} after {time.monotonic() - started:.1f}s: {Path(cmd[0]).name}")
    if proc.returncode != 0:
        raise DewError(f"Command failed ({proc.returncode}): {redact_command(cmd)}\n{redact_output(proc.stdout, cmd)}")
    return proc.stdout.strip()


def run_bytes(cmd: list[str], cwd: Path | None = None) -> bytes:
    log_step(f"run: {redact_command(cmd)}" + (f" (cwd {cwd})" if cwd else ""))
    started = time.monotonic()
    proc = subprocess.run(
        cmd,
        cwd=str(cwd) if cwd else None,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    log_step(f"exit {proc.returncode} after {time.monotonic() - started:.1f}s: {Path(cmd[0]).name}")
    if proc.returncode != 0:
        output = proc.stderr.decode(errors="replace")
        raise DewError(f"Command failed ({proc.returncode}): {redact_command(cmd)}\n{redact_output(output, cmd)}")
    return proc.stdout


def selected_root(paths: list[Path]) -> Path:
    resolved = [p.resolve() for p in paths if p.exists()]
    if not resolved:
        raise DewError("No existing files or folders were selected.")
    if len(resolved) == 1:
        return resolved[0]
    common = Path(os.path.commonpath([str(p.parent if p.is_file() else p) for p in resolved]))
    return common.resolve()


def unique_paths(paths: list[str]) -> list[Path]:
    seen: set[str] = set()
    result: list[Path] = []
    for item in paths:
        path = Path(item).resolve()
        key = str(path).casefold() if os.name == "nt" else str(path)
        if key in seen:
            continue
        seen.add(key)
        result.append(path)
    return result


def archive_output_dir(source: Path) -> Path:
    if source.is_file():
        base = source.parent
    else:
        base = source
    out = base / ARCHIVE_DIR_NAME
    out.mkdir(parents=True, exist_ok=True)
    return out


def repo_for_source(source: Path) -> Path:
    return archive_output_dir(source.resolve()) / REPO_DIR_NAME


def ensure_repo(repo: Path, git: Path) -> None:
    repo.mkdir(parents=True, exist_ok=True)
    if not (repo / ".git").exists():
        run([str(git), "init"], cwd=repo)
        run([str(git), "config", "user.name", "Dew Encryption"], cwd=repo)
        run([str(git), "config", "user.email", "dew-encryption@local"], cwd=repo)


def copy_selection(paths: list[Path], repo: Path) -> None:
    work = repo / "files"
    if work.exists():
        shutil.rmtree(work)
    work.mkdir(parents=True, exist_ok=True)
    for source in paths:
        source = source.resolve()
        if not source.exists():
            continue
        destination = work / source.name
        if source.is_dir():
            ignore = shutil.ignore_patterns(".git", REPO_DIR_NAME, ARCHIVE_DIR_NAME)
            shutil.copytree(source, destination, ignore=ignore)
        else:
            shutil.copy2(source, destination)


def commit_repo(repo: Path, git: Path, label: str) -> str:
    run([str(git), "add", "-A"], cwd=repo)
    status = run([str(git), "status", "--porcelain"], cwd=repo)
    if status:
        run([str(git), "commit", "-m", label], cwd=repo)
    return current_commit(repo, git)


def current_commit(repo: Path, git: Path | None = None) -> str:
    git = git or find_executable("git")
    try:
        return run([str(git), "rev-parse", "--short", "HEAD"], cwd=repo)
    except DewError:
        return ""


def compress_repo(repo: Path, output_dir: Path, seven_zip: Path, password: str | None = None) -> Path:
    stamp = dt.datetime.now().strftime("%Y%m%d-%H%M%S")
    archive = output_dir / f"dew-encryption-{stamp}.7z"
    cmd = [str(seven_zip), "a", "-t7z", "-mx=9", str(archive), str(repo)]
    if password:
        cmd.insert(4, "-mhe=on")
        cmd.insert(4, f"-p{password}")
    run(cmd)
    return archive


def process(paths: list[Path], password: str | None = None) -> DewResult:
    git = find_executable("git")
    seven_zip = find_executable(
        "7z",
        [
            Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "7-Zip" / "7z.exe",
            Path(os.environ.get("ProgramFiles(x86)", "C:/Program Files (x86)")) / "7-Zip" / "7z.exe",
        ],
    )
    root = selected_root(paths)
    repo = repo_for_source(root)
    ensure_repo(repo, git)
    copy_selection(paths, repo)
    commit = commit_repo(repo, git, f"dew snapshot {dt.datetime.now().isoformat(timespec='seconds')}")
    archive = compress_repo(repo, archive_output_dir(root), seven_zip, password=password)
    return DewResult(source=root, repo=repo, archive=archive, commit=commit)


def snapshot(paths: list[Path], label: str | None = None) -> DewResult:
    git = find_executable("git")
    root = selected_root(paths)
    repo = repo_for_source(root)
    ensure_repo(repo, git)
    copy_selection(paths, repo)
    commit = commit_repo(repo, git, label or f"dew history {dt.datetime.now().isoformat(timespec='seconds')}")
    return DewResult(source=root, repo=repo, archive=Path(), commit=commit)


def history(repo: Path, limit: int = 100) -> list[HistoryEntry]:
    git = find_executable("git")
    if not (repo / ".git").exists():
        return []
    raw = run(
        [str(git), "log", f"--max-count={limit}", "--date=iso-local", "--pretty=format:%h%x09%ad%x09%s"],
        cwd=repo,
    )
    entries: list[HistoryEntry] = []
    for line in raw.splitlines():
        parts = line.split("\t", 2)
        if len(parts) == 3:
            entries.append(HistoryEntry(parts[0], parts[1], parts[2]))
    return entries


def commit_details(repo: Path, commit: str) -> tuple[str, list[FileChange]]:
    git = find_executable("git")
    message = run([str(git), "show", "--no-patch", "--date=iso-local", "--pretty=fuller", commit], cwd=repo)
    raw_changes = run([str(git), "show", "--name-status", "--format=", commit], cwd=repo)
    changes: list[FileChange] = []
    for line in raw_changes.splitlines():
        parts = line.split("\t", 1)
        if len(parts) == 2:
            changes.append(FileChange(parts[0], parts[1]))
    return message, changes


def create_archive_for_repo(repo: Path, password: str | None = None) -> Path:
    seven_zip = find_executable(
        "7z",
        [
            Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "7-Zip" / "7z.exe",
            Path(os.environ.get("ProgramFiles(x86)", "C:/Program Files (x86)")) / "7-Zip" / "7z.exe",
        ],
    )
    return compress_repo(repo, repo.parent, seven_zip, password=password)


def restore_commit(repo: Path, commit: str, source: Path) -> None:
    git = find_executable("git")
    source = source.resolve()
    repo_member = f"files/{source.name}"
    archive = run_bytes([str(git), "archive", "--format=zip", commit, repo_member], cwd=repo)
    with tempfile.TemporaryDirectory(prefix="dew-restore-") as temp_dir:
        temp = Path(temp_dir)
        zip_path = temp / "restore.zip"
        zip_path.write_bytes(archive)
        with zipfile.ZipFile(zip_path) as zf:
            zf.extractall(temp)
        restored = temp / repo_member
        if not restored.exists():
            raise DewError(f"Commit does not contain {repo_member}")
        if restored.is_dir():
            if source.exists() and source.is_file():
                source.unlink()
            source.mkdir(parents=True, exist_ok=True)
            for child in source.iterdir():
                if child.name == ARCHIVE_DIR_NAME:
                    continue
                if child.is_dir():
                    shutil.rmtree(child)
                else:
                    child.unlink()
            for child in restored.iterdir():
                target = source / child.name
                if child.is_dir():
                    shutil.copytree(child, target)
                else:
                    shutil.copy2(child, target)
        else:
            source.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(restored, source)


def container_path_for_source(source: Path) -> Path:
    source = source.resolve()
    return source.with_name(source.name + VERACRYPT_EXT)


def source_path_for_container(container: Path) -> Path:
    container = container.resolve()
    name = container.name
    if name.endswith(VERACRYPT_EXT):
        return container.with_name(name[: -len(VERACRYPT_EXT)])
    if container.suffix.lower() == ".hc":
        return container.with_suffix("")
    return container.with_name(container.name + ".decrypted")


def estimated_container_size(source: Path, settings: VeraCryptSettings | None = None) -> str:
    settings = settings or load_settings().veracrypt
    size = 0
    if source.is_dir():
        for item in source.rglob("*"):
            if item.is_file():
                size += item.stat().st_size
    else:
        size = source.stat().st_size
    padded = max(
        size + settings.size_padding_mb * 1024 * 1024,
        int(size * settings.size_multiplier) + 16 * 1024 * 1024,
        settings.minimum_size_mb * 1024 * 1024,
    )
    return f"{padded // (1024 * 1024)}M"


def free_windows_drive_letter() -> str:
    used = {Path(f"{letter}:\\").drive.upper() for letter in "DEFGHIJKLMNOPQRSTUVWXYZ" if Path(f"{letter}:\\").exists()}
    for letter in reversed("DEFGHIJKLMNOPQRSTUVWXYZ"):
        drive = f"{letter}:"
        if drive.upper() not in used:
            return letter
    raise DewError("No free drive letter is available for VeraCrypt.")


def wait_for_windows_mount(mount_root: Path, attempts: int = 10, delay: float = 0.5) -> bool:
    """VeraCrypt /silent exits 0 even when the mount failed (e.g. wrong password), so poll the drive."""
    for _ in range(attempts):
        if mount_root.exists():
            return True
        time.sleep(delay)
    return False


def copy_into_mount(source: Path, mount_root: Path) -> None:
    target = mount_root / source.name
    if source.is_dir():
        shutil.copytree(source, target)
    else:
        shutil.copy2(source, target)


def copy_from_mount(mount_root: Path, output: Path) -> None:
    items = [item for item in mount_root.iterdir() if item.name not in {"System Volume Information", "$RECYCLE.BIN"}]
    if not items:
        raise DewError("The VeraCrypt container did not contain files to decrypt.")
    if len(items) == 1:
        item = items[0]
        target = output
        if item.is_dir():
            if target.exists():
                raise DewError(f"Output path already exists: {target}")
            shutil.copytree(item, target)
        else:
            if output.exists() and output.is_dir():
                target = output / item.name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(item, target)
        return
    output.mkdir(parents=True, exist_ok=True)
    for item in items:
        target = output / item.name
        if item.is_dir():
            shutil.copytree(item, target)
        else:
            shutil.copy2(item, target)


def veracrypt_create_container(
    source: Path,
    password: str,
    keep_source: bool | None = None,
    settings: VeraCryptSettings | None = None,
) -> Path:
    settings = settings or load_settings().veracrypt
    keep_source = settings.keep_source_after_encrypt if keep_source is None else keep_source
    source = source.resolve()
    if not source.exists():
        raise DewError(f"Source does not exist: {source}")
    vc = find_veracrypt()
    container = container_path_for_source(source)
    if container.exists():
        raise DewError(f"Container already exists: {container}")
    size = estimated_container_size(source, settings)
    log_step(f"veracrypt create: container {container} size {size} fs {settings.filesystem} via {vc}")

    if os.name == "nt":
        # "VeraCrypt Format.exe" rejects the VeraCrypt.exe-only /quit switch: with /silent the
        # suppressed parse-error dialog makes it hang forever, so never pass /quit here.
        run([
            str(find_veracrypt_format()), "/create", str(container), "/size", size, "/password", password,
            "/encryption", settings.encryption, "/hash", settings.hash, "/filesystem", settings.filesystem,
            "/pim", settings.pim, "/silent", "/force",
        ])
        log_step(f"veracrypt container created: {container}")
        letter = free_windows_drive_letter()
        log_step(f"veracrypt mount: {container} -> {letter}:")
        mounted = False
        try:
            run([str(vc), "/volume", str(container), "/letter", letter, "/password", password, "/pim", settings.pim, "/silent", "/quit"])
            mount_root = Path(f"{letter}:\\")
            mounted = wait_for_windows_mount(mount_root)
            if not mounted:
                raise DewError(f"VeraCrypt did not mount {container} on {letter}: - wrong password or PIM, or the container is damaged.")
            log_step(f"copying {source} into {mount_root}")
            copy_into_mount(source, mount_root)
        finally:
            if mounted:
                log_step(f"veracrypt dismount: {letter}:")
                run([str(vc), "/dismount", letter, "/silent", "/quit"])
            else:
                log_step(f"skipping dismount: {letter}: never mounted")
    else:
        random_source = source if source.is_file() else next((p for p in source.rglob("*") if p.is_file()), source)
        run([
            str(vc), "--text", "--create", str(container), "--size", size, "--password", password,
            "--encryption", settings.encryption, "--hash", settings.hash, "--filesystem", settings.filesystem,
            "--pim", settings.pim, "--keyfiles", "", "--random-source", str(random_source), "--non-interactive",
        ])
        with tempfile.TemporaryDirectory(prefix="dew-vc-mount-") as mount_dir:
            mount_root = Path(mount_dir)
            try:
                run([str(vc), "--text", "--mount", str(container), str(mount_root), "--password", password, "--pim", settings.pim, "--keyfiles", "", "--protect-hidden", "no", "--non-interactive"])
                copy_into_mount(source, mount_root)
            finally:
                run([str(vc), "--text", "--dismount", str(container)])

    if not keep_source:
        if source.is_dir():
            shutil.rmtree(source)
        else:
            source.unlink()
    return container


def veracrypt_decrypt_container(
    container: Path,
    password: str,
    output: Path | None = None,
    keep_container: bool | None = None,
    settings: VeraCryptSettings | None = None,
) -> Path:
    settings = settings or load_settings().veracrypt
    keep_container = settings.keep_container_after_decrypt if keep_container is None else keep_container
    container = container.resolve()
    if not container.exists():
        raise DewError(f"Container does not exist: {container}")
    vc = find_veracrypt()
    output = (output or source_path_for_container(container)).resolve()

    if os.name == "nt":
        letter = free_windows_drive_letter()
        log_step(f"veracrypt mount: {container} -> {letter}:")
        mounted = False
        try:
            run([str(vc), "/volume", str(container), "/letter", letter, "/password", password, "/pim", settings.pim, "/silent", "/quit"])
            mount_root = Path(f"{letter}:\\")
            mounted = wait_for_windows_mount(mount_root)
            if not mounted:
                raise DewError(f"VeraCrypt did not mount {container} on {letter}: - wrong password or PIM, or the container is damaged.")
            copy_from_mount(mount_root, output)
        finally:
            if mounted:
                log_step(f"veracrypt dismount: {letter}:")
                run([str(vc), "/dismount", letter, "/silent", "/quit"])
            else:
                log_step(f"skipping dismount: {letter}: never mounted")
    else:
        with tempfile.TemporaryDirectory(prefix="dew-vc-mount-") as mount_dir:
            mount_root = Path(mount_dir)
            try:
                run([str(vc), "--text", "--mount", str(container), str(mount_root), "--password", password, "--pim", settings.pim, "--keyfiles", "", "--protect-hidden", "no", "--non-interactive"])
                copy_from_mount(mount_root, output)
            finally:
                run([str(vc), "--text", "--dismount", str(container)])

    if not keep_container:
        container.unlink()
    return output



def find_docker() -> Path:
    try:
        return find_executable("docker")
    except DewError as exc:
        raise DewError("Docker was not found. Install Docker and make sure the docker CLI is available.") from exc


def docker_run(cmd: list[str]) -> str:
    docker = find_docker()
    try:
        return run([str(docker), *cmd])
    except DewError as exc:
        raise DewError(f"Docker {' '.join(cmd[:2]) if len(cmd) > 1 else cmd[0]} failed: {exc}") from exc


def dew_drive_registry_ref(name_or_ref: str) -> str:
    # `restore NAME` is a friendly alias for the same registry reference used by
    # `pull`; callers may pass a fully qualified reference or a short local tag.
    return name_or_ref


def read_secure_password(label: str, password_file: str | None = None, password_env: str | None = None) -> str:
    if password_file:
        try:
            password = Path(password_file).expanduser().read_text(encoding="utf-8").splitlines()[0]
        except (OSError, IndexError) as exc:
            raise DewError(f"Unable to read password file: {password_file}") from exc
    elif password_env:
        password = os.environ.get(password_env, "")
        if not password:
            raise DewError(f"Password environment variable is empty or missing: {password_env}")
    else:
        password = getpass.getpass(label)
    if not password:
        raise DewError("An encryption password is required.")
    return password


def metadata_payload_path(dew_drive_dir: Path, metadata: dict) -> Path:
    candidates = [
        metadata.get("payload"),
        metadata.get("payload_path"),
        metadata.get("encrypted_payload"),
        metadata.get("container"),
        metadata.get("container_path"),
    ]
    for value in candidates:
        if isinstance(value, str) and value.strip():
            path = dew_drive_dir / value
            if path.exists():
                return path
            raise DewError(f"Encrypted payload referenced by metadata is missing: {value}")
    payloads = [p for p in dew_drive_dir.iterdir() if p.is_file() and p.name != "metadata.json"]
    if len(payloads) == 1:
        return payloads[0]
    raise DewError("Encrypted payload metadata is missing or ambiguous.")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as fh:
        for chunk in iter(lambda: fh.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def validate_restored_files(output: Path, metadata: dict) -> list[str]:
    files = metadata.get("files") or metadata.get("manifest")
    if not isinstance(files, list):
        return ["metadata does not include a files list; skipped per-file validation"]
    warnings: list[str] = []
    base = output if output.is_dir() else output.parent
    for entry in files:
        if not isinstance(entry, dict):
            continue
        rel = entry.get("path") or entry.get("name")
        if not isinstance(rel, str) or Path(rel).is_absolute() or ".." in Path(rel).parts:
            continue
        target = base / rel
        if not target.exists():
            raise DewError(f"Restored file is missing: {rel}")
        if "size" in entry and target.is_file() and target.stat().st_size != int(entry["size"]):
            raise DewError(f"Restored file size mismatch: {rel}")
        expected_hash = entry.get("sha256")
        if isinstance(expected_hash, str) and target.is_file() and sha256_file(target).lower() != expected_hash.lower():
            raise DewError(f"Restored file hash mismatch: {rel}")
    return warnings


def decrypt_dew_drive_payload(payload: Path, password: str, output: Path, metadata: dict) -> Path:
    mode = str(metadata.get("encryption_mode") or "").lower()
    if payload.name.endswith(VERACRYPT_EXT) or payload.suffix.lower() == ".hc" or mode in {"veracrypt", "vera", "vc"}:
        return veracrypt_decrypt_container(payload, password, output=output, keep_container=True)

    seven_zip = find_executable(
        "7z",
        [
            Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "7-Zip" / "7z.exe",
            Path(os.environ.get("ProgramFiles(x86)", "C:/Program Files (x86)")) / "7-Zip" / "7z.exe",
        ],
    )
    output.mkdir(parents=True, exist_ok=True)
    if os.name == "nt" and '"' in password:
        # 7-Zip's Windows command-line splitter treats every quote as a delimiter toggle, so a
        # -p value containing " reaches 7-Zip with the wrong bytes; its console prompt takes the
        # password verbatim from stdin instead.
        run([str(seven_zip), "x", "-y", f"-o{output}", str(payload)], input_text=password + "\n")
    else:
        run([str(seven_zip), "x", "-y", f"-p{password}", f"-o{output}", str(payload)])
    return output


def pop_embedded_dew_drive_metadata(restored: Path) -> dict | None:
    """Read and remove the private metadata file a new-format payload carries at its root.

    Detection is deliberately conservative: a user file that merely shares the reserved name
    (possible in images synced before the manifest moved inside the payload) is left in place
    untouched so those images keep restoring byte-for-byte. The manifest-key fallback accepts
    embedded metadata written before the format marker existed.
    """
    if not restored.is_dir():
        return None
    path = restored / DEW_DRIVE_PRIVATE_METADATA_NAME
    if not path.is_file():
        return None
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None
    if not isinstance(data, dict):
        return None
    if data.get("format") != DEW_DRIVE_METADATA_FORMAT and "manifest" not in data:
        return None
    path.unlink()
    return data


def restore_dew_drive_from_registry(
    registry_ref: str,
    output: Path,
    password: str,
    force: bool = False,
) -> tuple[Path, list[str]]:
    output = output.expanduser().resolve()
    output_exists = output.exists()
    if output_exists and not force:
        raise DewError(f"Output path already exists: {output}. Use --force to replace it.")
    docker_run(["pull", registry_ref])
    container_id = ""
    try:
        # Dew Drive images are built FROM scratch with no CMD; docker create requires a
        # command string, but it never runs, so any placeholder works.
        container_id = docker_run(["create", registry_ref, "dew-drive-noop"]).strip()
        if not container_id:
            raise DewError("Docker did not return a temporary container id.")
        with tempfile.TemporaryDirectory(prefix="dew-drive-") as temp_dir:
            temp = Path(temp_dir)
            copied = temp / "dew-drive"
            docker_run(["cp", f"{container_id}:/dew-drive", str(copied)])
            metadata_path = copied / "metadata.json"
            if not metadata_path.exists():
                raise DewError("Payload metadata is missing: /dew-drive/metadata.json")
            try:
                metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
            except json.JSONDecodeError as exc:
                raise DewError("Payload metadata is not valid JSON.") from exc
            if not isinstance(metadata, dict):
                raise DewError("Payload metadata must be a JSON object.")
            payload = metadata_payload_path(copied, metadata)
            restore_target = temp / "restored-output"
            try:
                restored = decrypt_dew_drive_payload(payload, password, restore_target, metadata)
            except DewError as exc:
                raise DewError(f"Decryption failed: {exc}") from exc
            embedded = pop_embedded_dew_drive_metadata(restored)
            if embedded:
                metadata = {**metadata, **embedded}
                log_step("validating against manifest from inside the encrypted payload")
            warnings = validate_restored_files(restored, metadata)
            output.parent.mkdir(parents=True, exist_ok=True)
            if output_exists:
                if output.is_dir():
                    shutil.rmtree(output)
                else:
                    output.unlink()
            shutil.move(str(restored), str(output))
            return output, warnings
    finally:
        if container_id:
            try:
                docker_run(["rm", container_id])
            except DewError:
                pass


def container_history_repo(container: Path) -> Path:
    container = container.resolve()
    out = container.parent / ARCHIVE_DIR_NAME / "Container History" / container.stem
    out.mkdir(parents=True, exist_ok=True)
    return out / REPO_DIR_NAME


def snapshot_container(container: Path, label: str | None = None) -> DewResult:
    container = container.resolve()
    if not container.exists():
        raise DewError(f"Container does not exist: {container}")
    git = find_executable("git")
    repo = container_history_repo(container)
    ensure_repo(repo, git)
    copy_selection([container], repo)
    commit = commit_repo(repo, git, label or f"dew container history {dt.datetime.now().isoformat(timespec='seconds')}")
    return DewResult(source=container, repo=repo, archive=Path(), commit=commit)


def container_history(container: Path, limit: int = 100) -> list[HistoryEntry]:
    return history(container_history_repo(container), limit=limit)

def hook_variables(container: Path, mount_path: Path | None = None, event: str = "open") -> dict[str, str]:
    now = dt.datetime.now().isoformat(timespec="seconds")
    return {
        "event": event,
        "container": str(container),
        "container_name": container.name,
        "container_stem": container.stem,
        "mount_path": str(mount_path or ""),
        "user": getpass.getuser(),
        "host": os.environ.get("COMPUTERNAME") or os.environ.get("HOSTNAME") or "",
        "timestamp": now,
    }


def expand_hook_text(text: str, variables: dict[str, str]) -> str:
    result = text
    for key, value in variables.items():
        result = result.replace("{" + key + "}", value)
    return result


def run_container_hooks(profile: ContainerProfile | None, event: str, container: Path, mount_path: Path | None = None) -> list[str]:
    if not profile:
        return []
    variables = hook_variables(container, mount_path, event)
    messages: list[str] = []
    for hook in profile.hooks or []:
        if not hook.enabled or hook.event != event:
            continue
        target = expand_hook_text(hook.target, variables)
        payload = expand_hook_text(hook.payload, variables)
        try:
            if hook.kind == "script":
                if not target:
                    raise DewError("script hook target is empty")
                env = os.environ.copy()
                env.update({f"DEW_{key.upper()}": value for key, value in variables.items()})
                completed = subprocess.run(target, shell=True, text=True, input=payload or None, env=env, check=False)
                if completed.returncode != 0:
                    raise DewError(f"script exited with {completed.returncode}")
            elif hook.kind in {"discord", "home_assistant"}:
                if not target:
                    raise DewError("webhook URL is empty")
                body = payload or json.dumps({"content" if hook.kind == "discord" else "event": f"Dew container {event}: {container.name}"})
                data = body.encode("utf-8")
                req = urllib.request.Request(target, data=data, method="POST")
                req.add_header("Content-Type", "application/json")
                with urllib.request.urlopen(req, timeout=15) as response:
                    response.read()
            else:
                raise DewError(f"unknown hook kind: {hook.kind}")
            messages.append(f"{event} hook succeeded: {hook.name}")
        except (OSError, urllib.error.URLError, DewError) as exc:
            messages.append(f"{event} hook failed ({hook.name}): {exc}")
    return messages


def find_container_profile(container: Path) -> ContainerProfile | None:
    container = container.resolve()
    for profile in load_settings().containers or []:
        if profile.path and Path(profile.path).expanduser().resolve() == container:
            return profile
    return None


@dataclass(frozen=True)
class DewDriveSyncResult:
    profile: DewDriveProfile
    source: Path
    image: str
    build_context: Path
    payload: Path
    metadata: Path
    pushed: bool


def app_version() -> str:
    try:
        from dew_encryption import __version__
    except ImportError:
        return "unknown"
    return __version__


def dew_drive_profiles(settings: AppSettings | None = None) -> list[DewDriveProfile]:
    settings = settings or load_settings()
    dew_drives = settings.dew_drives
    if isinstance(dew_drives, DewDriveSettings):
        return dew_drives.drives
    return []


def find_dew_drive_profile(name: str) -> DewDriveProfile:
    for profile in dew_drive_profiles():
        if profile.name == name:
            return profile
    raise DewError(f"Dew Drive profile not found: {name}")


def path_matches_patterns(relative_path: str, patterns: list[str]) -> bool:
    normalized = relative_path.replace(os.sep, "/")
    name = Path(normalized).name
    return any(fnmatch.fnmatch(normalized, pattern) or fnmatch.fnmatch(name, pattern) for pattern in patterns)


def selected_dew_drive_files(source: Path, include_patterns: list[str], exclude_patterns: list[str]) -> list[Path]:
    if source.is_file():
        candidates = [source]
        base = source.parent
    else:
        base = source
        candidates = [path for path in source.rglob("*") if path.is_file()]
    selected: list[Path] = []
    for path in candidates:
        rel = path.relative_to(base).as_posix()
        if include_patterns and not path_matches_patterns(rel, include_patterns):
            continue
        if exclude_patterns and path_matches_patterns(rel, exclude_patterns):
            continue
        selected.append(path)
    return selected


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def copy_dew_drive_selection(source: Path, staging: Path, profile: DewDriveProfile) -> list[dict[str, object]]:
    include_patterns = list(profile.include_patterns or [])
    exclude_patterns = [".git/*", REPO_DIR_NAME + "/*", ARCHIVE_DIR_NAME + "/*", *list(profile.exclude_patterns or [])]
    files = selected_dew_drive_files(source, include_patterns, exclude_patterns)
    if not files:
        raise DewError(f"No files matched Dew Drive profile: {profile.name}")
    base = source.parent if source.is_file() else source
    manifest: list[dict[str, object]] = []
    for source_file in files:
        relative = source_file.relative_to(base)
        if relative.as_posix() == DEW_DRIVE_PRIVATE_METADATA_NAME:
            # Reserved root-level name, e.g. residue from a restore done by an older app version;
            # the sync writes its own copy there before encrypting.
            log_step(f"skipping reserved file at drive root: {relative.as_posix()}")
            continue
        target = staging / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source_file, target)
        stat = source_file.stat()
        manifest.append({
            "path": relative.as_posix(),
            "size": stat.st_size,
            "mtime": dt.datetime.fromtimestamp(stat.st_mtime, dt.timezone.utc).isoformat(),
            "sha256": sha256_file(source_file),
        })
    if not manifest:
        raise DewError(f"No files matched Dew Drive profile: {profile.name}")
    return manifest


def encrypt_dew_drive_staging(staging: Path, output_dir: Path, password: str, mode: str) -> Path:
    mode = mode.lower()
    log_step(f"encrypt staging: mode {mode}, staging {staging}")
    if mode in {"7z", "7zip", "archive", "password", "standard"}:
        seven_zip = find_executable(
            "7z",
            [
                Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "7-Zip" / "7z.exe",
                Path(os.environ.get("ProgramFiles(x86)", "C:/Program Files (x86)")) / "7-Zip" / "7z.exe",
            ],
        )
        payload = output_dir / DEW_DRIVE_PAYLOAD_NAME
        items = [str(item) for item in staging.iterdir()]
        if os.name == "nt" and '"' in password:
            # See decrypt_dew_drive_payload: quotes cannot ride in -p on Windows, so let the
            # bare -p switch trigger 7-Zip's console prompt and feed the password verbatim.
            run([str(seven_zip), "a", "-t7z", "-mx=9", "-p", "-mhe=on", str(payload), *items], input_text=password + "\n")
        else:
            run([str(seven_zip), "a", "-t7z", "-mx=9", f"-p{password}", "-mhe=on", str(payload), *items])
        return payload
    if mode in {"veracrypt", "vera", "vc"}:
        container_source = output_dir / "payload-source"
        shutil.copytree(staging, container_source)
        container = veracrypt_create_container(container_source, password, keep_source=False)
        payload = output_dir / "payload.dew.hc"
        container.rename(payload)
        return payload
    raise DewError(f"Unsupported Dew Drive encryption mode: {mode}")


def write_dew_drive_image_context(payload: Path, metadata: dict[str, object], build_context: Path) -> Path:
    drive_dir = build_context / "dew-drive"
    drive_dir.mkdir(parents=True, exist_ok=True)
    payload_name = "payload.dew.hc" if payload.name.endswith(VERACRYPT_EXT) else DEW_DRIVE_PAYLOAD_NAME
    staged_payload = drive_dir / payload_name
    shutil.copy2(payload, staged_payload)
    metadata_path = drive_dir / DEW_DRIVE_METADATA_NAME
    metadata_path.write_text(json.dumps(metadata, indent=2), encoding="utf-8")
    (build_context / "Dockerfile").write_text(
        "FROM scratch\n"
        f"COPY dew-drive/{payload_name} /dew-drive/{payload_name}\n"
        f"COPY dew-drive/{DEW_DRIVE_METADATA_NAME} /dew-drive/{DEW_DRIVE_METADATA_NAME}\n",
        encoding="utf-8",
    )
    return metadata_path


def assert_no_plaintext_in_build_context(build_context: Path) -> None:
    allowed = {"Dockerfile", f"dew-drive/{DEW_DRIVE_PAYLOAD_NAME}", "dew-drive/payload.dew.hc", f"dew-drive/{DEW_DRIVE_METADATA_NAME}"}
    for path in build_context.rglob("*"):
        if path.is_file() and path.relative_to(build_context).as_posix() not in allowed:
            raise DewError(f"Unexpected plaintext candidate in Docker build context: {path}")


def dew_drive_add(folder: Path, sources: list[Path]) -> list[Path]:
    folder = folder.expanduser().resolve()
    folder.mkdir(parents=True, exist_ok=True)
    copied: list[Path] = []
    for source in sources:
        source = source.expanduser().resolve()
        target = folder / source.name
        if source.is_dir():
            if target.exists():
                shutil.rmtree(target)
            shutil.copytree(source, target)
        elif source.is_file():
            shutil.copy2(source, target)
        else:
            raise DewError(f"Drive source does not exist: {source}")
        copied.append(target)
    return copied


def dew_drive_sync_folder(folder: Path, registry_image: str = "", push: bool = False) -> DewResult:
    folder = folder.expanduser().resolve()
    if not folder.exists():
        raise DewError(f"Dew Drive folder does not exist: {folder}")
    if push and not registry_image:
        raise DewError("A Git remote or registry image is required when --push is used with --folder.")
    result = snapshot([folder])
    if push and registry_image:
        subprocess.run(["git", "-C", str(result.repo), "push", registry_image, "HEAD"], check=True)
    return result


def dew_drive_pull(folder: Path, registry_image: str = "") -> None:
    folder = folder.expanduser().resolve()
    repo = repo_for_source(folder)
    if registry_image:
        subprocess.run(["git", "-C", str(repo), "pull", registry_image, "HEAD"], check=True)
    else:
        subprocess.run(["git", "-C", str(repo), "pull"], check=True)


def dew_drive_restore(folder: Path, commit: str = "HEAD") -> None:
    folder = folder.expanduser().resolve()
    restore_commit(repo_for_source(folder), commit, folder)


def dew_drive_sync(
    name_or_folder: str | Path,
    registry_or_push: str | bool = False,
    push: bool = False,
    registry: str | None = None,
    password: str | None = None,
) -> DewDriveSyncResult | DewResult:
    if isinstance(name_or_folder, Path):
        registry_image = registry_or_push if isinstance(registry_or_push, str) else ""
        return dew_drive_sync_folder(name_or_folder, registry_image, push=push)

    name = name_or_folder
    profile_push = (registry_or_push is True) or push
    profile = find_dew_drive_profile(name)
    local_path = profile.local_path or profile.folder
    source = Path(local_path).expanduser().resolve()
    if not local_path or not source.exists():
        raise DewError(f"Dew Drive local path does not exist for {name}: {source}")
    image = registry or profile.registry_ref or profile.registry_image or f"dew-drive-{profile.name.lower().replace(' ', '-')}:latest"
    password = password or os.environ.get("DEW_DRIVE_PASSWORD")
    if not password:
        password = getpass.getpass("Dew Drive encryption password: ")
    if not password:
        raise DewError("A Dew Drive encryption password is required.")
    with (
        tempfile.TemporaryDirectory(prefix="dew-drive-staging-") as staging_dir,
        tempfile.TemporaryDirectory(prefix="dew-drive-encrypted-") as encrypted_dir,
        tempfile.TemporaryDirectory(prefix="dew-drive-build-") as build_dir,
    ):
        staging = Path(staging_dir)
        encrypted = Path(encrypted_dir)
        build_context = Path(build_dir)
        manifest = copy_dew_drive_selection(source, staging, profile)
        # Everything sensitive (drive name, source path, per-file manifest) rides inside the
        # encrypted payload; the image-level metadata.json stays minimal because registries
        # can be public.
        private_metadata = {
            "format": DEW_DRIVE_METADATA_FORMAT,
            "drive_name": profile.name,
            "timestamp": dt.datetime.now(dt.timezone.utc).isoformat(),
            "source_platform": platform.platform(),
            "encryption_mode": profile.encryption_mode,
            "app_version": app_version(),
            "source": str(source),
            "manifest": manifest,
        }
        (staging / DEW_DRIVE_PRIVATE_METADATA_NAME).write_text(json.dumps(private_metadata, indent=2), encoding="utf-8")
        payload = encrypt_dew_drive_staging(staging, encrypted, password, profile.encryption_mode)
        metadata = {
            "format": DEW_DRIVE_METADATA_FORMAT,
            "encryption_mode": profile.encryption_mode,
            "payload": payload.name,
        }
        log_step("image metadata keeps only encryption mode and payload name; manifest and source stay inside the encrypted payload")
        metadata_path = write_dew_drive_image_context(payload, metadata, build_context)
        assert_no_plaintext_in_build_context(build_context)
        docker = find_executable("docker")
        run([str(docker), "build", "-t", image, str(build_context)])
        should_push = profile_push or profile.auto_push
        if should_push:
            run([str(docker), "push", image])
        return DewDriveSyncResult(profile, source, image, build_context, build_context / "dew-drive" / payload.name, metadata_path, should_push)


@dataclass(frozen=True)
class DockerAsset:
    kind: str
    identifier: str
    name: str


def docker_list_images() -> list[DockerAsset]:
    raw = docker_run(["image", "ls", "--format", "{{.Repository}}:{{.Tag}}\t{{.ID}}"] )
    assets: list[DockerAsset] = []
    for line in raw.splitlines():
        parts = line.split("\t", 1)
        if len(parts) != 2:
            continue
        ref, image_id = parts
        name = image_id if ref.startswith("<none>:") else ref
        assets.append(DockerAsset("image", name, name))
    return assets


def docker_list_containers() -> list[DockerAsset]:
    raw = docker_run(["ps", "-a", "--format", "{{.ID}}\t{{.Names}}"] )
    assets: list[DockerAsset] = []
    for line in raw.splitlines():
        parts = line.split("\t", 1)
        if len(parts) == 2:
            assets.append(DockerAsset("container", parts[0], parts[1] or parts[0]))
    return assets


def docker_list_volumes() -> list[DockerAsset]:
    raw = docker_run(["volume", "ls", "--format", "{{.Name}}"] )
    return [DockerAsset("volume", line.strip(), line.strip()) for line in raw.splitlines() if line.strip()]


def docker_list_assets() -> list[DockerAsset]:
    return [*docker_list_images(), *docker_list_containers(), *docker_list_volumes()]


def safe_docker_filename(asset: DockerAsset) -> str:
    safe = "".join(ch if ch.isalnum() or ch in ".-_" else "_" for ch in asset.name).strip("._")
    return safe or asset.identifier.replace(":", "_")


def docker_save_asset(asset: DockerAsset, output_dir: Path) -> Path:
    output_dir = output_dir.expanduser().resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    base = safe_docker_filename(asset)
    if asset.kind == "image":
        target = output_dir / f"docker-image-{base}.tar"
        docker_run(["image", "save", "-o", str(target), asset.identifier])
        return target
    if asset.kind == "container":
        target = output_dir / f"docker-container-{base}.tar"
        docker_run(["export", "-o", str(target), asset.identifier])
        return target
    if asset.kind == "volume":
        target = output_dir / f"docker-volume-{base}.tar.gz"
        mount = f"{asset.identifier}:/volume:ro"
        docker_run(["run", "--rm", "-v", mount, "-v", f"{output_dir}:/backup", "alpine", "sh", "-c", f"tar -czf /backup/{target.name} -C /volume ."])
        return target
    raise DewError(f"Unsupported Docker asset kind: {asset.kind}")


def docker_load_archive(archive: Path) -> str:
    archive = archive.expanduser().resolve()
    if not archive.exists():
        raise DewError(f"Docker archive does not exist: {archive}")
    return docker_run(["load", "-i", str(archive)])


def docker_push_archive(archive: Path, image_ref: str, loaded_ref: str = "") -> str:
    image_ref = image_ref.strip()
    if not image_ref:
        raise DewError("Docker image reference is required.")
    messages: list[str] = []
    source_ref = loaded_ref.strip()
    if not source_ref:
        load_output = docker_load_archive(archive)
        messages.append(load_output)
        for line in load_output.splitlines():
            if line.startswith("Loaded image: "):
                source_ref = line.removeprefix("Loaded image: ").strip()
                break
            if line.startswith("Loaded image ID: "):
                source_ref = line.removeprefix("Loaded image ID: ").strip()
                break
    if not source_ref:
        raise DewError("Could not determine the loaded Docker image reference. Enter a source image/ref manually.")
    if source_ref != image_ref:
        docker_run(["tag", source_ref, image_ref])
    messages.append(docker_run(["push", image_ref]))
    return "\n".join(message for message in messages if message)


def run_custom_remote_upload(source: Path, command_template: str) -> str:
    source = source.expanduser().resolve()
    if not source.exists():
        raise DewError(f"Upload source does not exist: {source}")
    if not command_template.strip():
        raise DewError("Custom remote command is required.")
    command = command_template.replace("{file}", str(source)).replace("{path}", str(source))
    proc = subprocess.run(command, shell=True, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, check=False)
    if proc.returncode != 0:
        raise DewError(f"Custom remote upload failed ({proc.returncode}): {command}\n{proc.stdout}")
    return proc.stdout.strip()


def _safe_repo_name(source: Path) -> str:
    return "".join(ch if ch.isalnum() or ch in "._-" else "-" for ch in source.resolve().name).strip(".-") or "encrypted-source"



def detect_buildable_source_commands(source: Path) -> tuple[str, str]:
    """Infer reasonable build and run commands for common repository types."""
    source = source.expanduser().resolve()
    if (source / "pyproject.toml").exists() or (source / "setup.py").exists():
        run_command = "python -m dew_encryption.gui" if (source / "dew_encryption" / "gui.py").exists() else "python -m pip --version"
        return "python -m pip install -e .", run_command
    package_json = source / "package.json"
    if package_json.exists():
        try:
            package = json.loads(package_json.read_text(encoding="utf-8"))
            scripts = package.get("scripts", {}) if isinstance(package, dict) else {}
        except (OSError, json.JSONDecodeError):
            scripts = {}
        build = "npm install"
        if isinstance(scripts, dict) and "build" in scripts:
            build = "npm install && npm run build"
        if isinstance(scripts, dict):
            for script in ("start", "dev", "serve"):
                if script in scripts:
                    return build, f"npm run {script}"
        return build, "npm start"
    if any(source.glob("*.sln")):
        solution = next(source.glob("*.sln"))
        return f"dotnet build {solution.name}", f"dotnet run --project {solution.name}"
    if any(source.rglob("*.csproj")):
        project = next(source.rglob("*.csproj"))
        rel = project.relative_to(source).as_posix()
        return f"dotnet build {rel}", f"dotnet run --project {rel}"
    if (source / "Cargo.toml").exists():
        return "cargo build", "cargo run"
    if (source / "Makefile").exists() or (source / "makefile").exists():
        return "make", "make run"
    return "", ""

def create_buildable_encrypted_source(
    source: Path,
    output: Path,
    password: str,
    remote: str = "",
    branch: str = "",
    build_command: str = "",
    run_command: str = "",
) -> Path:
    """Create a self-contained Python GUI manager with an encrypted repository payload embedded."""
    import base64

    source = source.expanduser().resolve()
    output = output.expanduser().resolve()
    if not source.exists() or not source.is_dir():
        raise DewError(f"Source repository folder does not exist: {source}")
    if not password:
        raise DewError("An encryption password is required.")
    if not (source / ".git").exists():
        raise DewError(f"Source is not a Git repository: {source}")
    seven_zip = find_executable(
        "7z",
        [
            Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "7-Zip" / "7z.exe",
            Path(os.environ.get("ProgramFiles(x86)", "C:/Program Files (x86)")) / "7-Zip" / "7z.exe",
        ],
    )
    git = find_executable("git")
    remote = remote.strip()
    if remote:
        try:
            run([str(git), "ls-remote", remote], cwd=source)
        except DewError as exc:
            raise DewError(f"Unable to read pull remote before baking manager: {remote}") from exc
    branch = branch.strip()
    if not branch:
        try:
            branch = run([str(git), "rev-parse", "--abbrev-ref", "HEAD"], cwd=source) or "main"
        except DewError:
            branch = "main"
    detected_build, detected_run = detect_buildable_source_commands(source)
    build_command = build_command or detected_build
    run_command = run_command or detected_run

    with tempfile.TemporaryDirectory(prefix="dew-buildable-source-") as temp_dir:
        temp = Path(temp_dir)
        payload = temp / "source.7z"
        run([
            str(seven_zip),
            "a",
            "-t7z",
            "-mx=9",
            f"-p{password}",
            "-mhe=on",
            str(payload),
            str(source),
            f"-xr!{ARCHIVE_DIR_NAME}",
        ])
        encoded = base64.b64encode(payload.read_bytes()).decode("ascii")

    manager = """#!/usr/bin/env python3
from __future__ import annotations

import base64
import os
import shutil
import subprocess
import sys
import tempfile
import threading
import tkinter as tk
from pathlib import Path
from tkinter import messagebox, simpledialog, ttk

APP_TITLE = "Dew Buildable Encrypted Source Manager"
REPO_NAME = __REPO_NAME__
REMOTE = __REMOTE__
BRANCH = __BRANCH__
BUILD_COMMAND = __BUILD_COMMAND__
RUN_COMMAND = __RUN_COMMAND__
PAYLOAD_B64 = __PAYLOAD_B64__


def tool(name):
    found = shutil.which(name)
    if not found:
        raise RuntimeError(f"Required executable not found: {name}")
    return found


def run(cmd, cwd=None, shell=False):
    proc = subprocess.run(cmd, cwd=str(cwd) if cwd else None, shell=shell, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    if proc.returncode != 0:
        raise RuntimeError(f"Command failed ({proc.returncode}): {cmd}\n{proc.stdout}")
    return proc.stdout.strip()


def cache_root():
    base = Path(os.environ.get("LOCALAPPDATA") or Path.home() / ".cache")
    root = base / "DewEncryption" / "buildable-source" / REPO_NAME
    root.mkdir(parents=True, exist_ok=True)
    return root


def repo_dir():
    return cache_root() / REPO_NAME


def decrypt_embedded(password, log):
    seven = tool("7z")
    root = cache_root()
    repo = repo_dir()
    if repo.exists():
        return False
    with tempfile.TemporaryDirectory(prefix="dew-embedded-") as temp_dir:
        payload = Path(temp_dir) / "source.7z"
        payload.write_bytes(base64.b64decode(PAYLOAD_B64))
        run([seven, "x", f"-p{password}", "-y", str(payload), f"-o{root}"])
    log(f"Decrypted embedded source to {repo}")
    return True


def pull_if_enabled(log):
    if not REMOTE:
        log("Pull remote is disabled; running cached source.")
        return False
    git = tool("git")
    repo = repo_dir()
    before = run([git, "rev-parse", "HEAD"], cwd=repo) if (repo / ".git").exists() else ""
    if not (repo / ".git").exists():
        raise RuntimeError("Decrypted source is not a Git repository.")
    try:
        run([git, "remote", "get-url", "origin"], cwd=repo)
    except RuntimeError:
        run([git, "remote", "add", "origin", REMOTE], cwd=repo)
    run([git, "fetch", "origin", BRANCH], cwd=repo)
    run([git, "merge", "--ff-only", f"origin/{BRANCH}"], cwd=repo)
    after = run([git, "rev-parse", "HEAD"], cwd=repo)
    changed = before != after
    log("Pulled a new version; build will run." if changed else "Already at latest version; build skipped.")
    return changed


def shell_run(command, log):
    if not command:
        return
    log(f"$ {command}")
    log(run(command, cwd=repo_dir(), shell=True))


def build_and_run(password, build_command, run_command, log):
    if not run_command:
        raise RuntimeError("Run command is empty. Enter a command in the manager before starting.")
    first = decrypt_embedded(password, log)
    changed = pull_if_enabled(log)
    if first or changed:
        shell_run(build_command, log)
    else:
        log("No new version detected; build command was not run.")
    shell_run(run_command, log)


class App(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title(APP_TITLE)
        self.geometry("760x460")
        ttk.Label(self, text=f"Encrypted source: {REPO_NAME}").pack(anchor="w", padx=12, pady=(12, 4))
        ttk.Label(self, text=f"Pull remote: {REMOTE or 'disabled'}").pack(anchor="w", padx=12)
        form = ttk.Frame(self)
        form.pack(fill="x", padx=12, pady=8)
        form.columnconfigure(1, weight=1)
        self.build_command = tk.StringVar(value=BUILD_COMMAND)
        self.run_command = tk.StringVar(value=RUN_COMMAND)
        ttk.Label(form, text="Build command").grid(row=0, column=0, sticky="w", pady=2)
        ttk.Entry(form, textvariable=self.build_command).grid(row=0, column=1, sticky="ew", padx=(8, 0), pady=2)
        ttk.Label(form, text="Run command").grid(row=1, column=0, sticky="w", pady=2)
        ttk.Entry(form, textvariable=self.run_command).grid(row=1, column=1, sticky="ew", padx=(8, 0), pady=2)
        bar = ttk.Frame(self)
        bar.pack(fill="x", padx=12, pady=8)
        ttk.Button(bar, text="Build/Run", command=self.start).pack(side="left")
        ttk.Button(bar, text="Open Cache", command=self.open_cache).pack(side="left", padx=8)
        self.logbox = tk.Text(self, wrap="word")
        self.logbox.pack(fill="both", expand=True, padx=12, pady=(0, 12))

    def log(self, text):
        self.logbox.insert("end", str(text) + "\n")
        self.logbox.see("end")

    def start(self):
        password = simpledialog.askstring("Password", "Encrypted source password:", show="*", parent=self)
        if not password:
            return

        def worker():
            try:
                build_and_run(password, self.build_command.get().strip(), self.run_command.get().strip(), self.log)
                self.log("Done.")
            except Exception as exc:
                self.log(f"ERROR: {exc}")
                messagebox.showerror(APP_TITLE, str(exc), parent=self)

        threading.Thread(target=worker, daemon=True).start()

    def open_cache(self):
        path = cache_root()
        if sys.platform.startswith("win"):
            os.startfile(path)
        elif sys.platform == "darwin":
            subprocess.Popen(["open", str(path)])
        else:
            subprocess.Popen(["xdg-open", str(path)])


if __name__ == "__main__":
    App().mainloop()
"""
    replacements = {
        "__REPO_NAME__": repr(_safe_repo_name(source)),
        "__REMOTE__": repr(remote),
        "__BRANCH__": repr(branch),
        "__BUILD_COMMAND__": repr(build_command),
        "__RUN_COMMAND__": repr(run_command),
        "__PAYLOAD_B64__": repr(encoded),
    }
    for key, value in replacements.items():
        manager = manager.replace(key, value)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(manager, encoding="utf-8")
    try:
        output.chmod(output.stat().st_mode | 0o111)
    except OSError:
        pass
    return output


def find_containing_git_repo(path: Path) -> Path | None:
    git = find_executable("git")
    target = path.expanduser().resolve()
    cwd = target if target.is_dir() else target.parent
    if not cwd.exists():
        raise DewError(f"Path does not exist: {path}")
    try:
        top = run([str(git), "rev-parse", "--show-toplevel"], cwd=cwd)
    except DewError:
        return None
    return Path(top).resolve()


def opencode_commit_message(repo: Path, git: Path) -> str:
    fallback = "dew"
    opencode = shutil.which("opencode")
    if not opencode:
        return fallback
    try:
        status = run([str(git), "status", "--short"], cwd=repo)
        diff = run([str(git), "diff", "--cached", "--stat"], cwd=repo)
    except DewError:
        return fallback
    prompt = (
        "Generate one concise git commit message subject line. "
        "Return only the subject, with no quotes, markdown, or explanation.\n\n"
        f"Repository status:\n{status[:4000]}\n\nStaged diff stat:\n{diff[:4000]}"
    )
    candidates = [
        [opencode, "run", prompt],
        [opencode, "-p", prompt],
        [opencode, prompt],
    ]
    for cmd in candidates:
        try:
            proc = subprocess.run(
                cmd,
                cwd=str(repo),
                text=True,
                stdout=subprocess.PIPE,
                stderr=subprocess.DEVNULL,
                input="",
                timeout=45,
                check=False,
            )
        except (OSError, subprocess.TimeoutExpired):
            continue
        if proc.returncode != 0:
            continue
        message = proc.stdout.strip().splitlines()[0].strip().strip('"').strip("'")
        if message:
            return message[:200]
    return fallback


def commit_and_push_repo(path: Path) -> tuple[Path, str, str]:
    git = find_executable("git")
    repo = find_containing_git_repo(path)
    if repo is None:
        raise DewError(f"Not inside a Git repository: {path}")
    run([str(git), "add", "-A"], cwd=repo)
    status = run([str(git), "status", "--porcelain"], cwd=repo)
    commit = ""
    message = ""
    if status:
        message = opencode_commit_message(repo, git)
        run([str(git), "commit", "-m", message], cwd=repo)
        commit = current_commit(repo, git)
    else:
        commit = current_commit(repo, git)
    run([str(git), "push"], cwd=repo)
    return repo, commit, message


def watch(paths: list[Path], interval: float = 5.0, once: bool = False) -> None:
    last_commit = ""
    while True:
        result = snapshot(paths)
        if result.commit and result.commit != last_commit:
            print(f"{dt.datetime.now().isoformat(timespec='seconds')} committed {result.commit}", flush=True)
            last_commit = result.commit
        if once:
            return
        time.sleep(interval)


def main(argv: list[str] | None = None) -> int:
    argv = list(sys.argv[1:] if argv is None else argv)
    log_step(f"dew-encryption start: args {redact_command(argv)}")
    log_step(f"config: {config_file()}")
    if argv and argv[0] == "buildable-source":
        parser = argparse.ArgumentParser(prog="dew-encryption buildable-source", description="Create a self-contained encrypted source GUI manager.")
        subparsers = parser.add_subparsers(dest="command", required=True)
        create_parser = subparsers.add_parser("create", help="Encrypt a Git repo into a build/run manager Python file.")
        create_parser.add_argument("source", help="Git repository folder to encrypt and bake into the manager.")
        create_parser.add_argument("output", help="Output manager .py file.")
        create_parser.add_argument("--password", help="Encryption password. If omitted, a hidden prompt is shown.")
        create_parser.add_argument("--remote", default="", help="Optional Git remote URL to pull before running.")
        create_parser.add_argument("--branch", default="", help="Remote branch to fast-forward when pull is enabled. Defaults to current branch.")
        create_parser.add_argument("--build-command", default="", help="Command run after first decrypt or after a successful pull update. Auto-detected when omitted where possible.")
        create_parser.add_argument("--run-command", default="", help="Command run every time after any required build step. Auto-detected when omitted where possible, and editable in the generated GUI.")
        args = parser.parse_args(argv[1:])
        try:
            password = args.password or getpass.getpass("Encrypted source password: ")
            output = create_buildable_encrypted_source(
                Path(args.source),
                Path(args.output),
                password,
                remote=args.remote,
                branch=args.branch,
                build_command=args.build_command,
                run_command=args.run_command,
            )
        except DewError as exc:
            print(f"dew encryption failed: {exc}", file=sys.stderr)
            return 1
        print(f"Buildable encrypted source manager: {output}")
        return 0
    if argv[:2] == ["dew-drive", "sync"]:
        parser = argparse.ArgumentParser(prog="dew-encryption dew-drive sync", description="Encrypt a Dew Drive profile and publish it as a Docker/OCI image.")
        parser.add_argument("name", nargs="?", help="Dew Drive profile name.")
        parser.add_argument("--folder", help="Snapshot a local Dew Drive folder directly.")
        parser.add_argument("--push", action="store_true", help="Push the image after a successful Docker build.")
        parser.add_argument("--registry", "--registry-image", dest="registry", help="Docker/OCI image tag or Git remote.")
        args = parser.parse_args(argv[2:])
        try:
            if args.folder:
                result = dew_drive_sync(Path(args.folder), args.registry or "", push=args.push)
            else:
                if not args.name:
                    parser.error("name is required unless --folder is supplied")
                result = dew_drive_sync(args.name, push=args.push, registry=args.registry)
        except DewError as exc:
            print(f"dew encryption failed: {exc}", file=sys.stderr)
            return 1
        if isinstance(result, DewDriveSyncResult):
            print(f"Dew Drive image: {result.image}")
            print(f"Payload: /dew-drive/{result.payload.name}")
            print(f"Metadata: /dew-drive/{result.metadata.name}")
            if result.pushed:
                print("Pushed: yes")
        else:
            print(f"Dew Drive folder: {result.source}")
            print(f"Repo: {result.repo}")
            print(f"Commit: {result.commit}")
        return 0

    if argv and argv[0] == "portable":
        parser = argparse.ArgumentParser(prog="dew-encryption portable", description="Manage portable mode.")
        parser.add_argument("--init", action="store_true", help="Create portable.flag beside the app so settings are stored portably.")
        parser.add_argument("--show", action="store_true", help="Show portable state and settings path.")
        args = parser.parse_args(argv[1:])
        marker = app_base_dir() / "portable.flag"
        if args.init:
            marker.write_text("portable\n", encoding="utf-8")
        print(json.dumps({
            "app_base": str(app_base_dir()),
            "portable": os.environ.get("DEW_ENCRYPTION_PORTABLE") == "1" or marker.exists(),
            "portable_marker": str(marker),
            "settings": str(config_file()),
        }, indent=2))
        return 0

    if argv and argv[0] == "veracrypt-settings":
        parser = argparse.ArgumentParser(prog="dew-encryption veracrypt-settings", description="Show or update remembered VeraCrypt defaults.")
        parser.add_argument("--show", action="store_true", help="Show current VeraCrypt settings as JSON.")
        parser.add_argument("--encryption", help="VeraCrypt encryption algorithm.")
        parser.add_argument("--hash", help="VeraCrypt hash algorithm.")
        parser.add_argument("--filesystem", help="Container filesystem.")
        parser.add_argument("--pim", help="VeraCrypt PIM value.")
        parser.add_argument("--size-padding-mb", type=int, help="Extra MB added to estimated source size.")
        parser.add_argument("--size-multiplier", type=float, help="Multiplier applied to source size estimate.")
        parser.add_argument("--minimum-size-mb", type=int, help="Minimum container size in MB.")
        parser.add_argument("--veracrypt-path", help="Explicit VeraCrypt executable path.")
        parser.add_argument("--keep-source-after-encrypt", choices=["true", "false"], help="Remember whether encrypt keeps original source.")
        parser.add_argument("--keep-container-after-decrypt", choices=["true", "false"], help="Remember whether decrypt keeps the container.")
        args = parser.parse_args(argv[1:])
        settings = load_settings()
        vc = settings.veracrypt
        if args.encryption:
            vc.encryption = args.encryption
        if args.hash:
            vc.hash = args.hash
        if args.filesystem:
            vc.filesystem = args.filesystem
        if args.pim:
            vc.pim = args.pim
        if args.size_padding_mb is not None:
            vc.size_padding_mb = args.size_padding_mb
        if args.size_multiplier is not None:
            vc.size_multiplier = args.size_multiplier
        if args.minimum_size_mb is not None:
            vc.minimum_size_mb = args.minimum_size_mb
        if args.veracrypt_path is not None:
            vc.veracrypt_path = args.veracrypt_path
        if args.keep_source_after_encrypt is not None:
            vc.keep_source_after_encrypt = args.keep_source_after_encrypt == "true"
        if args.keep_container_after_decrypt is not None:
            vc.keep_container_after_decrypt = args.keep_container_after_decrypt == "true"
        if not args.show:
            save_settings(settings)
        print(json.dumps(asdict(settings), indent=2))
        return 0

    if argv and argv[0] == "veracrypt-encrypt":
        parser = argparse.ArgumentParser(prog="dew-encryption veracrypt-encrypt", description="Move a file or folder into a VeraCrypt container.")
        parser.add_argument("sources", nargs="+", help="Files or folders to encrypt into VeraCrypt containers.")
        parser.add_argument("--password", help="VeraCrypt container password. If omitted, a hidden prompt is shown.")
        parser.add_argument("--keep-source", action="store_true", help="Keep the original file or folder after the container is created.")
        parser.add_argument("--remove-source", action="store_true", help="Remove the original file or folder after the container is created.")
        args = parser.parse_args(argv[1:])
        try:
            password = args.password or getpass.getpass("VeraCrypt password: ")
            if not password:
                raise DewError("A VeraCrypt password is required.")
            keep_source = None
            if args.keep_source:
                keep_source = True
            if args.remove_source:
                keep_source = False
            containers = [
                veracrypt_create_container(Path(source), password, keep_source=keep_source)
                for source in unique_paths(args.sources)
            ]
        except DewError as exc:
            print(f"dew encryption failed: {exc}", file=sys.stderr)
            return 1
        for container in containers:
            print(f"VeraCrypt container: {container}")
        return 0

    if argv and argv[0] == "veracrypt-decrypt":
        parser = argparse.ArgumentParser(prog="dew-encryption veracrypt-decrypt", description="Extract files from a VeraCrypt container.")
        parser.add_argument("containers", nargs="+", help="VeraCrypt container paths.")
        parser.add_argument("--password", help="VeraCrypt container password. If omitted, a hidden prompt is shown.")
        parser.add_argument("--output", help="Output file or folder path. Defaults beside the container.")
        parser.add_argument("--remove-container", action="store_true", help="Delete the container after successful extraction.")
        parser.add_argument("--keep-container", action="store_true", help="Keep the container after successful extraction.")
        args = parser.parse_args(argv[1:])
        try:
            password = args.password or getpass.getpass("VeraCrypt password: ")
            if not password:
                raise DewError("A VeraCrypt password is required.")
            if args.output and len(args.containers) > 1:
                raise DewError("--output can only be used with one container.")
            outputs = [
                veracrypt_decrypt_container(
                    Path(container),
                    password,
                    output=Path(args.output) if args.output else None,
                    keep_container=True if args.keep_container else (False if args.remove_container else None),
                )
                for container in unique_paths(args.containers)
            ]
        except DewError as exc:
            print(f"dew encryption failed: {exc}", file=sys.stderr)
            return 1
        for output in outputs:
            print(f"Decrypted to: {output}")
        return 0




    if argv and argv[0] == "dew-drive":
        parser = argparse.ArgumentParser(prog="dew-encryption dew-drive", description="Restore registry-backed Dew Drives.")
        subparsers = parser.add_subparsers(dest="command", required=True)

        pull_parser = subparsers.add_parser("pull", help="Pull a Dew Drive image from a registry and restore it.")
        pull_parser.add_argument("registry_ref", help="Container registry image reference to pull.")
        pull_parser.add_argument("--output", required=True, help="Output file or folder path for restored contents.")
        pull_parser.add_argument("--force", action="store_true", help="Replace the output path if it already exists.")
        pull_parser.add_argument("--password-file", help="Read the encryption password from the first line of a file.")
        pull_parser.add_argument("--password-env", help="Read the encryption password from this environment variable.")

        restore_parser = subparsers.add_parser("restore", help="Restore a named Dew Drive from its registry-backed image.")
        restore_parser.add_argument("name", nargs="?", help="Dew Drive name or registry image reference.")
        restore_parser.add_argument("--folder", help="Restore a local folder-backed Dew Drive from its history repo.")
        restore_parser.add_argument("--commit", default="HEAD", help="Local history commit to restore when --folder is used.")
        restore_parser.add_argument("--output", help="Output file or folder path for restored contents.")
        restore_parser.add_argument("--force", action="store_true", help="Replace the output path if it already exists.")
        restore_parser.add_argument("--password-file", help="Read the encryption password from the first line of a file.")
        restore_parser.add_argument("--password-env", help="Read the encryption password from this environment variable.")

        args = parser.parse_args(argv[1:])
        try:
            if args.command == "restore" and args.folder:
                dew_drive_restore(Path(args.folder), args.commit)
                print(f"Dew Drive folder restored: {Path(args.folder).expanduser().resolve()}")
                print(f"Commit: {args.commit}")
                return 0
            if not args.output:
                parser.error("--output is required unless restoring --folder")
            if args.command == "restore" and not args.name:
                parser.error("name is required unless --folder is supplied")
            registry_ref = args.registry_ref if args.command == "pull" else dew_drive_registry_ref(args.name)
            password = read_secure_password(
                "Dew Drive encryption password: ",
                password_file=args.password_file,
                password_env=args.password_env,
            )
            output, warnings = restore_dew_drive_from_registry(
                registry_ref,
                Path(args.output),
                password,
                force=args.force,
            )
        except DewError as exc:
            print(f"dew encryption failed: {exc}", file=sys.stderr)
            return 1
        print(f"Dew Drive restored to: {output}")
        for warning in warnings:
            print(f"warning: {warning}", file=sys.stderr)
        return 0


    if argv and argv[0] == "container-history":
        parser = argparse.ArgumentParser(prog="dew-encryption container-history", description="Snapshot or inspect Git history for encrypted container files.")
        parser.add_argument("container", help="Container path.")
        parser.add_argument("--snapshot", action="store_true", help="Commit the current container file into its container history repo.")
        parser.add_argument("--limit", type=int, default=25, help="Maximum history rows to print.")
        args = parser.parse_args(argv[1:])
        try:
            container = Path(args.container)
            if args.snapshot:
                result = snapshot_container(container)
                print(f"Container history snapshot: {result.commit}")
                print(f"Repo: {result.repo}")
            for entry in container_history(container, limit=args.limit):
                print(f"{entry.commit}\t{entry.author_date}\t{entry.subject}")
        except DewError as exc:
            print(f"dew encryption failed: {exc}", file=sys.stderr)
            return 1
        return 0

    if argv and argv[0] == "container-hooks":
        parser = argparse.ArgumentParser(prog="dew-encryption container-hooks", description="Run configured container open/close hooks.")
        parser.add_argument("event", choices=["open", "close"], help="Hook event to run.")
        parser.add_argument("container", help="Container path.")
        parser.add_argument("--mount-path", default="", help="Mounted path exposed to hooks.")
        args = parser.parse_args(argv[1:])
        profile = find_container_profile(Path(args.container))
        for message in run_container_hooks(profile, args.event, Path(args.container), Path(args.mount_path) if args.mount_path else None):
            print(message)
        return 0

    if argv and argv[0] == "container-quick-create":
        parser = argparse.ArgumentParser(prog="dew-encryption container-quick-create", description="Create a VeraCrypt container from Explorer/Nautilus with remembered defaults.")
        parser.add_argument("sources", nargs="+", help="Files or folders to turn into containers.")
        parser.add_argument("--password", help="VeraCrypt container password. If omitted, a hidden prompt is shown.")
        args = parser.parse_args(argv[1:])
        try:
            password = args.password or getpass.getpass("VeraCrypt password: ")
            if not password:
                raise DewError("A VeraCrypt password is required.")
            settings = load_settings()
            for source in unique_paths(args.sources):
                container = veracrypt_create_container(source, password, settings=settings.veracrypt)
                profile = ContainerProfile(name=container.stem, path=str(container), theme=ThemeSettings(), hooks=[])
                settings.containers = settings.containers or []
                settings.containers.append(profile)
                print(f"Quick-created container: {container}")
            save_settings(settings)
        except DewError as exc:
            print(f"dew encryption failed: {exc}", file=sys.stderr)
            return 1
        return 0

    if argv and argv[0] == "git-commit-push":
        parser = argparse.ArgumentParser(prog="dew-encryption git-commit-push", description="Commit and push the containing Git repository.")
        parser.add_argument("path", help="Explorer path inside the Git repository.")
        args = parser.parse_args(argv[1:])
        try:
            repo, commit, message = commit_and_push_repo(Path(args.path))
        except DewError as exc:
            print(f"dew encryption failed: {exc}", file=sys.stderr)
            return 1
        print(f"Repo: {repo}")
        if message:
            print(f"Commit message: {message}")
        print(f"Commit: {commit}")
        print("Pushed: yes")
        return 0

    if argv and argv[0] == "watch":
        parser = argparse.ArgumentParser(prog="dew-encryption watch", description="Automatically commit selected paths whenever contents change.")
        parser.add_argument("paths", nargs="+", help="Files or folders to watch.")
        parser.add_argument("--interval", type=float, default=5.0, help="Polling interval in seconds.")
        parser.add_argument("--once", action="store_true", help="Run one history snapshot and exit.")
        args = parser.parse_args(argv[1:])
        try:
            watch([Path(p) for p in args.paths], interval=args.interval, once=args.once)
        except DewError as exc:
            print(f"dew encryption failed: {exc}", file=sys.stderr)
            return 1
        return 0

    if argv and argv[0] == "history":
        parser = argparse.ArgumentParser(prog="dew-encryption history", description="Show local Git history for a Dew Encryption archive repo.")
        parser.add_argument("repo", help="Path to .dew-encryption-repo.")
        parser.add_argument("--limit", type=int, default=25)
        args = parser.parse_args(argv[1:])
        try:
            for entry in history(Path(args.repo), limit=args.limit):
                print(f"{entry.commit}\t{entry.author_date}\t{entry.subject}")
        except DewError as exc:
            print(f"dew encryption failed: {exc}", file=sys.stderr)
            return 1
        return 0

    if argv and argv[0] == "details":
        parser = argparse.ArgumentParser(prog="dew-encryption details", description="Show commit metadata and changed files.")
        parser.add_argument("repo", help="Path to .dew-encryption-repo.")
        parser.add_argument("commit", help="Commit hash to inspect.")
        args = parser.parse_args(argv[1:])
        try:
            message, changes = commit_details(Path(args.repo), args.commit)
            print(message)
            print("\nChanged files:")
            for change in changes:
                print(f"{change.status}\t{change.path}")
        except DewError as exc:
            print(f"dew encryption failed: {exc}", file=sys.stderr)
            return 1
        return 0

    if argv and argv[0] == "restore":
        parser = argparse.ArgumentParser(prog="dew-encryption restore", description="Restore a file or folder from a history commit.")
        parser.add_argument("repo", help="Path to .dew-encryption-repo.")
        parser.add_argument("commit", help="Commit hash to restore.")
        parser.add_argument("source", help="Original file or folder path to restore.")
        args = parser.parse_args(argv[1:])
        try:
            restore_commit(Path(args.repo), args.commit, Path(args.source))
        except DewError as exc:
            print(f"dew encryption failed: {exc}", file=sys.stderr)
            return 1
        print(f"Restored {args.source} to {args.commit}")
        return 0

    parser = argparse.ArgumentParser(prog="dew-encryption", description="Commit selected files locally and compress them with 7-Zip.")
    parser.add_argument("paths", nargs="+", help="Files or folders selected in Explorer.")
    parser.add_argument("--password", help="Optional 7z password. Enables header encryption.")
    args = parser.parse_args(argv)
    try:
        result = process([Path(p) for p in args.paths], password=args.password)
    except DewError as exc:
        print(f"dew encryption failed: {exc}", file=sys.stderr)
        return 1
    print(f"Archive: {result.archive}")
    print(f"Repo: {result.repo}")
    print(f"Commit: {result.commit}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
