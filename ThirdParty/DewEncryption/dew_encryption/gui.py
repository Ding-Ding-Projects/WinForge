from __future__ import annotations

import queue
import os
import subprocess
import sys
import threading
import tkinter as tk
from pathlib import Path
from tkinter import filedialog, messagebox, ttk

from .core import (
    ContainerProfile,
    DewDriveProfile,
    DewError,
    HookAction,
    ThemeSettings,
    commit_details,
    container_history,
    container_history_repo,
    create_archive_for_repo,
    history,
    load_settings,
    process,
    repo_for_source,
    restore_commit,
    run_container_hooks,
    snapshot_container,
    save_settings,
    snapshot,
    dew_drive_add,
    dew_drive_pull,
    dew_drive_restore,
    dew_drive_sync,
    docker_list_assets,
    docker_push_archive,
    docker_save_asset,
    run_custom_remote_upload,
)


class DewFileManager(tk.Tk):
    def __init__(self, initial_paths: list[Path] | None = None, open_history: bool = False) -> None:
        super().__init__()
        self.title("Dew Encryption")
        self.geometry("1120x700")
        self.minsize(940, 560)
        self.selected: list[Path] = []
        self.events: queue.Queue[str] = queue.Queue()
        self.watch_stop = threading.Event()
        self.watch_thread: threading.Thread | None = None
        self.settings = load_settings()
        self.setting_vars: dict[str, tk.Variable] = {}
        self.container_vars: dict[str, tk.Variable] = {}
        self.hook_vars: dict[str, tk.Variable] = {}
        self.drive_vars: dict[str, tk.Variable] = {}
        self._build()
        for path in initial_paths or []:
            self._add(path)
        if open_history:
            self.notebook.select(self.history_tab)
            self.refresh_history()
        self.after(100, self._drain_events)

    def _build(self) -> None:
        self.columnconfigure(0, weight=1)
        self.rowconfigure(1, weight=1)

        bar = ttk.Frame(self, padding=10)
        bar.grid(row=0, column=0, sticky="ew")
        ttk.Button(bar, text="Add Files", command=self.add_files).pack(side="left", padx=(0, 8))
        ttk.Button(bar, text="Add Folder", command=self.add_folder).pack(side="left", padx=(0, 8))
        ttk.Button(bar, text="Remove", command=self.remove_selected).pack(side="left", padx=(0, 8))
        ttk.Button(bar, text="Run Dew Encryption", command=self.run_job).pack(side="right")

        self.notebook = ttk.Notebook(self)
        self.notebook.grid(row=1, column=0, sticky="nsew", padx=10, pady=(0, 10))

        body = ttk.Frame(self.notebook, padding=(0, 0, 0, 0))
        self.notebook.add(body, text="Files")
        body.columnconfigure(0, weight=1)
        body.rowconfigure(0, weight=1)

        self.tree = ttk.Treeview(body, columns=("path", "kind"), show="headings", selectmode="extended")
        self.tree.heading("path", text="Path")
        self.tree.heading("kind", text="Type")
        self.tree.column("path", width=760)
        self.tree.column("kind", width=120, anchor="center")
        self.tree.grid(row=0, column=0, sticky="nsew")

        scroll = ttk.Scrollbar(body, orient="vertical", command=self.tree.yview)
        scroll.grid(row=0, column=1, sticky="ns")
        self.tree.configure(yscrollcommand=scroll.set)

        self.log = tk.Text(body, height=8, wrap="word")
        self.log.grid(row=1, column=0, columnspan=2, sticky="ew", pady=(10, 0))
        self.log.insert("end", "Select files or folders, then run Dew Encryption.\n")
        self.log.configure(state="disabled")

        self.history_tab = ttk.Frame(self.notebook)
        self.notebook.add(self.history_tab, text="History")
        self.history_tab.columnconfigure(0, weight=1)
        self.history_tab.columnconfigure(1, weight=1)
        self.history_tab.rowconfigure(1, weight=1)

        history_bar = ttk.Frame(self.history_tab, padding=(0, 0, 0, 10))
        history_bar.grid(row=0, column=0, columnspan=2, sticky="ew")
        ttk.Button(history_bar, text="Refresh History", command=self.refresh_history).pack(side="left", padx=(0, 8))
        ttk.Button(history_bar, text="Snapshot Now", command=self.snapshot_now).pack(side="left", padx=(0, 8))
        ttk.Button(history_bar, text="Start Auto History", command=self.start_watch).pack(side="left", padx=(0, 8))
        ttk.Button(history_bar, text="Stop Auto History", command=self.stop_watch).pack(side="left", padx=(0, 8))
        ttk.Button(history_bar, text="Create Archive", command=self.create_archive).pack(side="left", padx=(0, 8))
        ttk.Button(history_bar, text="Restore Selected", command=self.restore_selected).pack(side="left", padx=(0, 8))
        ttk.Button(history_bar, text="Open Source", command=self.open_source).pack(side="left", padx=(0, 8))
        ttk.Button(history_bar, text="Open Repo", command=self.open_repo).pack(side="left")

        self.history_tree = ttk.Treeview(self.history_tab, columns=("commit", "date", "subject"), show="headings")
        self.history_tree.heading("commit", text="Commit")
        self.history_tree.heading("date", text="Date")
        self.history_tree.heading("subject", text="Subject")
        self.history_tree.column("commit", width=90, anchor="center")
        self.history_tree.column("date", width=220)
        self.history_tree.column("subject", width=360)
        self.history_tree.grid(row=1, column=0, sticky="nsew", padx=(0, 10))
        self.history_tree.bind("<<TreeviewSelect>>", self.show_history_details)

        self.details = tk.Text(self.history_tab, wrap="word")
        self.details.grid(row=1, column=1, sticky="nsew")
        self.details.insert("end", "Select a commit to view details and changed files.\n")
        self.details.configure(state="disabled")

        settings_tab = ttk.Frame(self.notebook, padding=10)
        self.notebook.add(settings_tab, text="VeraCrypt")
        settings_tab.columnconfigure(1, weight=1)
        vc = self.settings.veracrypt
        fields = [
            ("Encryption", "encryption", vc.encryption),
            ("Hash", "hash", vc.hash),
            ("Filesystem", "filesystem", vc.filesystem),
            ("PIM", "pim", vc.pim),
            ("Size padding MB", "size_padding_mb", str(vc.size_padding_mb)),
            ("Size multiplier", "size_multiplier", str(vc.size_multiplier)),
            ("Minimum size MB", "minimum_size_mb", str(vc.minimum_size_mb)),
            ("VeraCrypt path", "veracrypt_path", vc.veracrypt_path),
        ]
        for row, (label, key, value) in enumerate(fields):
            ttk.Label(settings_tab, text=label).grid(row=row, column=0, sticky="w", pady=4)
            var = tk.StringVar(value=value)
            self.setting_vars[key] = var
            ttk.Entry(settings_tab, textvariable=var).grid(row=row, column=1, sticky="ew", pady=4, padx=(10, 0))
        keep_source = tk.BooleanVar(value=vc.keep_source_after_encrypt)
        keep_container = tk.BooleanVar(value=vc.keep_container_after_decrypt)
        self.setting_vars["keep_source_after_encrypt"] = keep_source
        self.setting_vars["keep_container_after_decrypt"] = keep_container
        ttk.Checkbutton(settings_tab, text="Keep original after VeraCrypt encrypt", variable=keep_source).grid(row=len(fields), column=0, columnspan=2, sticky="w", pady=4)
        ttk.Checkbutton(settings_tab, text="Keep container after VeraCrypt decrypt", variable=keep_container).grid(row=len(fields) + 1, column=0, columnspan=2, sticky="w", pady=4)
        ttk.Button(settings_tab, text="Save VeraCrypt Settings", command=self.save_veracrypt_settings).grid(row=len(fields) + 2, column=0, sticky="w", pady=(12, 0))

        self.container_tab = ttk.Frame(self.notebook, padding=10)
        self.notebook.add(self.container_tab, text="Containers")
        self._build_container_manager()

        self.drive_tab = ttk.Frame(self.notebook, padding=10)
        self.notebook.add(self.drive_tab, text="Dew Drive")
        self._build_dew_drive()


    def _build_dew_drive(self) -> None:
        tab = self.drive_tab
        tab.columnconfigure(0, weight=1)
        tab.columnconfigure(1, weight=2)
        tab.rowconfigure(1, weight=1)
        ttk.Label(tab, text="Dew Drive keeps an encrypted, syncable drive folder with profile settings for registry images, encryption mode, and file patterns.").grid(row=0, column=0, columnspan=2, sticky="ew", pady=(0, 8))

        left = ttk.LabelFrame(tab, text="Drive profiles list")
        left.grid(row=1, column=0, sticky="nsew", padx=(0, 10))
        left.columnconfigure(0, weight=1)
        left.rowconfigure(0, weight=1)
        self.drive_tree = ttk.Treeview(left, columns=("name", "folder"), show="headings", height=10)
        self.drive_tree.heading("name", text="Drive")
        self.drive_tree.heading("folder", text="Local folder")
        self.drive_tree.column("name", width=150)
        self.drive_tree.column("folder", width=320)
        self.drive_tree.grid(row=0, column=0, sticky="nsew")
        self.drive_tree.bind("<<TreeviewSelect>>", self.load_drive_profile)

        editor = ttk.Frame(tab)
        editor.grid(row=1, column=1, sticky="nsew")
        editor.columnconfigure(1, weight=1)
        fields = [
            ("Drive name", "name"),
            ("Local drive folder picker", "folder"),
            ("Registry image field", "registry_image"),
            ("Include patterns", "include_patterns"),
            ("Exclude patterns", "exclude_patterns"),
        ]
        for row, (label, key) in enumerate(fields):
            ttk.Label(editor, text=label).grid(row=row, column=0, sticky="w", pady=4)
            var = tk.StringVar()
            self.drive_vars[key] = var
            ttk.Entry(editor, textvariable=var).grid(row=row, column=1, sticky="ew", pady=4, padx=(8, 0))
        ttk.Button(editor, text="Browse", command=self.pick_drive_folder).grid(row=1, column=2, padx=(8, 0))
        ttk.Label(editor, text="Encryption mode selector").grid(row=len(fields), column=0, sticky="w", pady=4)
        self.drive_vars["encryption_mode"] = tk.StringVar(value="standard")
        ttk.Combobox(editor, textvariable=self.drive_vars["encryption_mode"], values=("standard", "password", "veracrypt"), state="readonly").grid(row=len(fields), column=1, sticky="ew", pady=4, padx=(8, 0))

        buttons = ttk.LabelFrame(editor, text="Add files/folders to drive, sync encrypted drive, and pull/restore drive")
        buttons.grid(row=len(fields) + 1, column=0, columnspan=3, sticky="ew", pady=(12, 0))
        for text, command in [
            ("New Drive", self.new_drive_profile),
            ("Add Files", self.add_drive_files),
            ("Add Folder", self.add_drive_folder),
            ("Sync", lambda: self.sync_drive(False)),
            ("Sync + Push", lambda: self.sync_drive(True)),
            ("Pull", self.pull_drive),
            ("Restore", self.restore_drive),
        ]:
            ttk.Button(buttons, text=text, command=command).pack(side="left", padx=(0, 6), pady=8)
        self.refresh_drive_profiles()

    def refresh_drive_profiles(self) -> None:
        for item in self.drive_tree.get_children():
            self.drive_tree.delete(item)
        for idx, profile in enumerate(self.settings.dew_drives or []):
            self.drive_tree.insert("", "end", iid=str(idx), values=(profile.name, profile.folder))

    def new_drive_profile(self) -> None:
        for var in self.drive_vars.values():
            var.set("")
        self.drive_vars["name"].set("New Drive")
        self.drive_vars["include_patterns"].set("**/*")
        self.drive_vars["encryption_mode"].set("standard")
        self.save_drive_profile()

    def load_drive_profile(self, _event: object | None = None) -> None:
        selected = self.drive_tree.selection()
        if not selected:
            return
        profile = (self.settings.dew_drives or [])[int(selected[0])]
        for key in self.drive_vars:
            self.drive_vars[key].set(getattr(profile, key, ""))

    def save_drive_profile(self) -> DewDriveProfile:
        profile = DewDriveProfile(**{key: str(var.get()) for key, var in self.drive_vars.items()})
        self.settings.dew_drives = self.settings.dew_drives or []
        selected = self.drive_tree.selection()
        if selected:
            self.settings.dew_drives[int(selected[0])] = profile
        else:
            self.settings.dew_drives.append(profile)
        save_settings(self.settings)
        self.refresh_drive_profiles()
        return profile

    def pick_drive_folder(self) -> None:
        folder = filedialog.askdirectory(title="Select local Dew Drive folder")
        if folder:
            self.drive_vars["folder"].set(folder)
            self.save_drive_profile()

    def _drive_profile_for_action(self) -> DewDriveProfile | None:
        profile = self.save_drive_profile()
        if not profile.folder:
            messagebox.showinfo("Dew Drive", "Choose a local drive folder first.")
            return None
        return profile

    def add_drive_files(self) -> None:
        profile = self._drive_profile_for_action()
        if not profile:
            return
        paths = [Path(item) for item in filedialog.askopenfilenames(title="Add files to Dew Drive")]
        if paths:
            for target in dew_drive_add(Path(profile.folder), paths):
                self._log(f"Added to Dew Drive: {target}")

    def add_drive_folder(self) -> None:
        profile = self._drive_profile_for_action()
        if not profile:
            return
        folder = filedialog.askdirectory(title="Add folder to Dew Drive")
        if folder:
            for target in dew_drive_add(Path(profile.folder), [Path(folder)]):
                self._log(f"Added to Dew Drive: {target}")

    def sync_drive(self, push: bool) -> None:
        profile = self._drive_profile_for_action()
        if not profile:
            return
        threading.Thread(target=self._drive_sync_worker, args=(profile, push), daemon=True).start()

    def _drive_sync_worker(self, profile: DewDriveProfile, push: bool) -> None:
        try:
            result = dew_drive_sync(Path(profile.folder), profile.registry_image, push=push)
            self.events.put(f"Dew Drive synced: {result.commit}\nRepo: {result.repo}\n")
        except (DewError, subprocess.CalledProcessError) as exc:
            self.events.put(f"Dew Drive sync failed: {exc}\n")

    def pull_drive(self) -> None:
        profile = self._drive_profile_for_action()
        if not profile:
            return
        try:
            dew_drive_pull(Path(profile.folder), profile.registry_image)
            self._log("Dew Drive pulled.")
        except (DewError, subprocess.CalledProcessError) as exc:
            messagebox.showerror("Dew Drive", f"Pull failed: {exc}")

    def restore_drive(self) -> None:
        profile = self._drive_profile_for_action()
        if not profile:
            return
        commit = "HEAD"
        try:
            dew_drive_restore(Path(profile.folder), commit)
            self._log(f"Dew Drive restored to {commit}.")
        except DewError as exc:
            messagebox.showerror("Dew Drive", f"Restore failed: {exc}")

    def _build_container_manager(self) -> None:
        tab = self.container_tab
        tab.columnconfigure(0, weight=1)
        tab.columnconfigure(1, weight=2)
        tab.rowconfigure(1, weight=1)
        ttk.Label(tab, text="Modern container manager: per-container theme colors/fonts plus multiple open/close actions for Discord, Home Assistant, and scripts.").grid(row=0, column=0, columnspan=2, sticky="ew", pady=(0, 8))
        self.container_tree = ttk.Treeview(tab, columns=("name", "path"), show="headings", height=8)
        self.container_tree.heading("name", text="Container")
        self.container_tree.heading("path", text="Path")
        self.container_tree.column("name", width=160)
        self.container_tree.column("path", width=420)
        self.container_tree.grid(row=1, column=0, sticky="nsew", padx=(0, 10))
        self.container_tree.bind("<<TreeviewSelect>>", self.load_container_profile)
        editor = ttk.Frame(tab)
        editor.grid(row=1, column=1, sticky="nsew")
        editor.columnconfigure(1, weight=1)
        fields = [("Name", "name"), ("Container path", "path"), ("Preferred mount path", "mount_path"), ("Background", "background"), ("Foreground", "foreground"), ("Accent", "accent"), ("Font", "font_family"), ("Font size", "font_size")]
        for row, (label, key) in enumerate(fields):
            ttk.Label(editor, text=label).grid(row=row, column=0, sticky="w", pady=3)
            var = tk.StringVar()
            self.container_vars[key] = var
            ttk.Entry(editor, textvariable=var).grid(row=row, column=1, sticky="ew", pady=3, padx=(8, 0))
        hook_box = ttk.LabelFrame(editor, text="Add open/close action")
        hook_box.grid(row=len(fields), column=0, columnspan=2, sticky="ew", pady=(10, 0))
        hook_box.columnconfigure(1, weight=1)
        hook_fields = [("Action name", "name"), ("Target URL or script", "target"), ("JSON payload / stdin", "payload")]
        self.hook_vars["event"] = tk.StringVar(value="open")
        self.hook_vars["kind"] = tk.StringVar(value="script")
        ttk.Label(hook_box, text="Event").grid(row=0, column=0, sticky="w", pady=3)
        ttk.Combobox(hook_box, textvariable=self.hook_vars["event"], values=("open", "close"), state="readonly").grid(row=0, column=1, sticky="ew", pady=3)
        ttk.Label(hook_box, text="Kind").grid(row=1, column=0, sticky="w", pady=3)
        ttk.Combobox(hook_box, textvariable=self.hook_vars["kind"], values=("script", "discord", "home_assistant"), state="readonly").grid(row=1, column=1, sticky="ew", pady=3)
        for offset, (label, key) in enumerate(hook_fields, start=2):
            ttk.Label(hook_box, text=label).grid(row=offset, column=0, sticky="w", pady=3)
            var = tk.StringVar()
            self.hook_vars[key] = var
            ttk.Entry(hook_box, textvariable=var).grid(row=offset, column=1, sticky="ew", pady=3, padx=(8, 0))
        buttons = ttk.Frame(editor)
        buttons.grid(row=len(fields)+1, column=0, columnspan=2, sticky="ew", pady=(10, 0))
        ttk.Button(buttons, text="New", command=self.new_container_profile).pack(side="left", padx=(0, 6))
        ttk.Button(buttons, text="Add Action", command=self.add_container_hook).pack(side="left", padx=(0, 6))
        ttk.Button(buttons, text="Save", command=self.save_container_profile).pack(side="left", padx=(0, 6))
        ttk.Button(buttons, text="Test Open Hooks", command=lambda: self.test_container_hooks("open")).pack(side="left", padx=(0, 6))
        ttk.Button(buttons, text="Test Close Hooks", command=lambda: self.test_container_hooks("close")).pack(side="left", padx=(0, 6))
        ttk.Button(buttons, text="Snapshot History", command=self.snapshot_container_history).pack(side="left", padx=(0, 6))
        ttk.Button(buttons, text="Refresh History", command=self.refresh_container_history).pack(side="left", padx=(0, 6))
        ttk.Button(buttons, text="Open History Repo", command=self.open_container_history_repo).pack(side="left")

        history_box = ttk.LabelFrame(tab, text="Container file history")
        history_box.grid(row=2, column=0, columnspan=2, sticky="nsew", pady=(10, 0))
        history_box.columnconfigure(0, weight=1)
        history_box.rowconfigure(0, weight=1)
        self.container_history_tree = ttk.Treeview(history_box, columns=("commit", "date", "subject"), show="headings", height=7)
        self.container_history_tree.heading("commit", text="Commit")
        self.container_history_tree.heading("date", text="Date")
        self.container_history_tree.heading("subject", text="Subject")
        self.container_history_tree.column("commit", width=90, anchor="center")
        self.container_history_tree.column("date", width=220)
        self.container_history_tree.column("subject", width=560)
        self.container_history_tree.grid(row=0, column=0, sticky="nsew")
        self.refresh_container_profiles()

    def refresh_container_profiles(self) -> None:
        for item in self.container_tree.get_children():
            self.container_tree.delete(item)
        for idx, profile in enumerate(self.settings.containers or []):
            self.container_tree.insert("", "end", iid=str(idx), values=(profile.name, profile.path))

    def new_container_profile(self) -> None:
        for var in self.container_vars.values():
            var.set("")
        self.container_vars["background"].set("#0f172a")
        self.container_vars["foreground"].set("#e5e7eb")
        self.container_vars["accent"].set("#38bdf8")
        self.container_vars["font_family"].set("Segoe UI")
        self.container_vars["font_size"].set("10")
        self.container_tree.selection_remove(self.container_tree.selection())

    def load_container_profile(self, _event: object | None = None) -> None:
        selected = self.container_tree.selection()
        if not selected:
            return
        profile = (self.settings.containers or [])[int(selected[0])]
        theme = profile.theme or ThemeSettings()
        values = {"name": profile.name, "path": profile.path, "mount_path": profile.mount_path, "background": theme.background, "foreground": theme.foreground, "accent": theme.accent, "font_family": theme.font_family, "font_size": str(theme.font_size)}
        for key, value in values.items():
            self.container_vars[key].set(value)
        self.refresh_container_history()

    def _profile_from_form(self) -> ContainerProfile:
        return ContainerProfile(
            name=str(self.container_vars["name"].get() or "Default"),
            path=str(self.container_vars["path"].get()),
            mount_path=str(self.container_vars["mount_path"].get()),
            theme=ThemeSettings(
                background=str(self.container_vars["background"].get() or "#0f172a"),
                foreground=str(self.container_vars["foreground"].get() or "#e5e7eb"),
                accent=str(self.container_vars["accent"].get() or "#38bdf8"),
                font_family=str(self.container_vars["font_family"].get() or "Segoe UI"),
                font_size=int(str(self.container_vars["font_size"].get() or "10")),
            ),
            hooks=[],
        )

    def save_container_profile(self) -> None:
        try:
            profile = self._profile_from_form()
        except ValueError as exc:
            messagebox.showerror("Container Settings", f"Invalid theme setting: {exc}")
            return
        selected = self.container_tree.selection()
        self.settings.containers = self.settings.containers or []
        if selected:
            existing = self.settings.containers[int(selected[0])]
            profile.hooks = existing.hooks or []
            self.settings.containers[int(selected[0])] = profile
        else:
            self.settings.containers.append(profile)
        save_settings(self.settings)
        self.refresh_container_profiles()
        self._log("Container profile saved.")

    def add_container_hook(self) -> None:
        selected = self.container_tree.selection()
        if not selected:
            self.save_container_profile()
            selected = self.container_tree.selection() or (str(len(self.settings.containers or []) - 1),)
        hook = HookAction(name=str(self.hook_vars["name"].get() or "New action"), event=str(self.hook_vars["event"].get()), kind=str(self.hook_vars["kind"].get()), target=str(self.hook_vars["target"].get()), payload=str(self.hook_vars["payload"].get()), enabled=True)
        profile = (self.settings.containers or [])[int(selected[0])]
        profile.hooks = profile.hooks or []
        profile.hooks.append(hook)
        save_settings(self.settings)
        self._log(f"Added {hook.event} {hook.kind} action to {profile.name}.")

    def _selected_container_profile(self) -> ContainerProfile | None:
        selected = self.container_tree.selection()
        if not selected:
            return None
        return (self.settings.containers or [])[int(selected[0])]

    def refresh_container_history(self) -> None:
        if not hasattr(self, "container_history_tree"):
            return
        for item in self.container_history_tree.get_children():
            self.container_history_tree.delete(item)
        profile = self._selected_container_profile()
        if not profile or not profile.path:
            return
        try:
            for entry in container_history(Path(profile.path)):
                self.container_history_tree.insert("", "end", iid=entry.commit, values=(entry.commit, entry.author_date, entry.subject))
        except DewError as exc:
            self._log(f"Container history failed: {exc}")

    def snapshot_container_history(self) -> None:
        profile = self._selected_container_profile()
        if not profile or not profile.path:
            messagebox.showinfo("Container History", "Select a saved container profile first.")
            return
        try:
            result = snapshot_container(Path(profile.path))
        except DewError as exc:
            messagebox.showerror("Container History", f"Snapshot failed: {exc}")
            return
        self._log(f"Container history snapshot: {result.commit}")
        self.refresh_container_history()

    def open_container_history_repo(self) -> None:
        profile = self._selected_container_profile()
        if not profile or not profile.path:
            messagebox.showinfo("Container History", "Select a saved container profile first.")
            return
        self._open_path(container_history_repo(Path(profile.path)))

    def test_container_hooks(self, event: str) -> None:
        selected = self.container_tree.selection()
        if not selected:
            messagebox.showinfo("Container Hooks", "Select a container profile first.")
            return
        profile = (self.settings.containers or [])[int(selected[0])]
        container = Path(profile.path or ".").expanduser()
        mount_path = Path(profile.mount_path).expanduser() if profile.mount_path else None
        for message in run_container_hooks(profile, event, container, mount_path):
            self._log(message)

    def open_docker_upload_dialog(self, source: Path | None = None) -> None:
        source = (source or self._active_source() or Path()).expanduser().resolve()
        if not source or not source.exists() or source.is_dir():
            messagebox.showinfo("Docker Upload", "Choose a Docker archive/container file first.")
            return
        dialog = tk.Toplevel(self)
        dialog.title("Upload to Docker or Custom Remote")
        dialog.columnconfigure(1, weight=1)
        ttk.Label(dialog, text=f"File: {source}").grid(row=0, column=0, columnspan=3, sticky="ew", padx=10, pady=(10, 6))
        mode = tk.StringVar(value="docker")
        image_ref = tk.StringVar(value="")
        source_ref = tk.StringVar(value="")
        command = tk.StringVar(value="")
        ttk.Radiobutton(dialog, text="Docker registry", variable=mode, value="docker").grid(row=1, column=0, sticky="w", padx=10)
        ttk.Radiobutton(dialog, text="Custom remote command", variable=mode, value="custom").grid(row=1, column=1, sticky="w", padx=10)
        ttk.Label(dialog, text="Target image/ref").grid(row=2, column=0, sticky="w", padx=10, pady=4)
        ttk.Entry(dialog, textvariable=image_ref).grid(row=2, column=1, columnspan=2, sticky="ew", padx=10, pady=4)
        ttk.Label(dialog, text="Source image/ref (optional)").grid(row=3, column=0, sticky="w", padx=10, pady=4)
        ttk.Entry(dialog, textvariable=source_ref).grid(row=3, column=1, columnspan=2, sticky="ew", padx=10, pady=4)
        ttk.Label(dialog, text="Custom command ({file})").grid(row=4, column=0, sticky="w", padx=10, pady=4)
        ttk.Entry(dialog, textvariable=command).grid(row=4, column=1, columnspan=2, sticky="ew", padx=10, pady=4)
        ttk.Label(dialog, text="Docker mode loads the archive if no source image/ref is supplied, tags it, then pushes it.").grid(row=5, column=0, columnspan=3, sticky="ew", padx=10, pady=4)

        def run_upload() -> None:
            dialog.destroy()
            threading.Thread(target=self._docker_upload_worker, args=(source, mode.get(), image_ref.get(), source_ref.get(), command.get()), daemon=True).start()

        ttk.Button(dialog, text="Upload", command=run_upload).grid(row=6, column=1, sticky="e", padx=10, pady=10)
        ttk.Button(dialog, text="Cancel", command=dialog.destroy).grid(row=6, column=2, sticky="e", padx=10, pady=10)

    def _docker_upload_worker(self, source: Path, mode: str, image_ref: str, source_ref: str, command: str) -> None:
        try:
            if mode == "docker":
                output = docker_push_archive(source, image_ref, loaded_ref=source_ref)
            else:
                output = run_custom_remote_upload(source, command)
            self.events.put(f"Docker/custom remote upload completed for {source}.\n{output}\n")
        except DewError as exc:
            self.events.put(f"Docker/custom remote upload failed: {exc}\n")

    def open_docker_save_dialog(self, output_dir: Path | None = None) -> None:
        output_dir = (output_dir or self._active_source() or Path.cwd()).expanduser().resolve()
        if output_dir.is_file():
            output_dir = output_dir.parent
        try:
            assets = docker_list_assets()
        except DewError as exc:
            messagebox.showerror("Save Docker Image Here", f"Unable to list Docker assets: {exc}")
            return
        dialog = tk.Toplevel(self)
        dialog.title("Save Docker Images, Containers, or Volumes Here")
        dialog.geometry("760x420")
        dialog.columnconfigure(0, weight=1)
        dialog.rowconfigure(1, weight=1)
        ttk.Label(dialog, text=f"Save selected Docker assets to: {output_dir}").grid(row=0, column=0, sticky="ew", padx=10, pady=8)
        tree = ttk.Treeview(dialog, columns=("save", "kind", "name", "id"), show="headings", selectmode="extended")
        for col, title, width in (("save", "Save", 60), ("kind", "Type", 100), ("name", "Name", 300), ("id", "Identifier", 260)):
            tree.heading(col, text=title)
            tree.column(col, width=width)
        tree.grid(row=1, column=0, sticky="nsew", padx=10)
        selected: set[int] = set()
        for idx, asset in enumerate(assets):
            tree.insert("", "end", iid=str(idx), values=("", asset.kind, asset.name, asset.identifier))

        def toggle(_event: object | None = None) -> None:
            for item in tree.selection():
                idx = int(item)
                if idx in selected:
                    selected.remove(idx)
                    mark = ""
                else:
                    selected.add(idx)
                    mark = "✓"
                tree.set(item, "save", mark)

        tree.bind("<Double-1>", toggle)
        tree.bind("<space>", toggle)

        def save_selected() -> None:
            chosen = [assets[idx] for idx in sorted(selected)]
            if not chosen:
                messagebox.showinfo("Save Docker Image Here", "Select one or more Docker assets first.")
                return
            dialog.destroy()
            threading.Thread(target=self._docker_save_worker, args=(chosen, output_dir), daemon=True).start()

        buttons = ttk.Frame(dialog)
        buttons.grid(row=2, column=0, sticky="ew", padx=10, pady=10)
        ttk.Button(buttons, text="Toggle Selected", command=toggle).pack(side="left")
        ttk.Button(buttons, text="Save Here", command=save_selected).pack(side="right")
        ttk.Button(buttons, text="Cancel", command=dialog.destroy).pack(side="right", padx=(0, 8))

    def _docker_save_worker(self, assets: list[object], output_dir: Path) -> None:
        try:
            outputs = [docker_save_asset(asset, output_dir) for asset in assets]
            self.events.put("Saved Docker assets:\n" + "\n".join(str(path) for path in outputs) + "\n")
        except DewError as exc:
            self.events.put(f"Save Docker assets failed: {exc}\n")

    def add_files(self) -> None:
        for item in filedialog.askopenfilenames(title="Select files"):
            self._add(Path(item))

    def add_folder(self) -> None:
        folder = filedialog.askdirectory(title="Select folder")
        if folder:
            self._add(Path(folder))

    def remove_selected(self) -> None:
        selected_ids = set(self.tree.selection())
        self.selected = [p for p in self.selected if str(p) not in selected_ids]
        for item in selected_ids:
            self.tree.delete(item)

    def run_job(self) -> None:
        if not self.selected:
            messagebox.showinfo("Dew Encryption", "Select at least one file or folder first.")
            return
        paths = list(self.selected)
        self._log("Starting Dew Encryption job...")
        threading.Thread(target=self._worker, args=(paths,), daemon=True).start()

    def _worker(self, paths: list[Path]) -> None:
        try:
            result = process(paths)
            self.events.put(f"Archive: {result.archive}\nRepo: {result.repo}\nCommit: {result.commit}\n::refresh-history::")
        except DewError as exc:
            self.events.put(f"Failed: {exc}\n")

    def start_watch(self) -> None:
        if not self.selected:
            messagebox.showinfo("Dew Encryption", "Select at least one file or folder first.")
            return
        if self.watch_thread and self.watch_thread.is_alive():
            self._log("Auto history is already running.")
            return
        self.watch_stop.clear()
        paths = list(self.selected)
        self.watch_thread = threading.Thread(target=self._watch_worker, args=(paths,), daemon=True)
        self.watch_thread.start()
        self._log("Auto history started.")

    def stop_watch(self) -> None:
        self.watch_stop.set()
        self._log("Auto history stopped.")

    def snapshot_now(self) -> None:
        if not self.selected:
            messagebox.showinfo("Dew Encryption", "Select a file history folder first.")
            return
        try:
            result = snapshot([self.selected[0]])
        except DewError as exc:
            self._set_details(f"Snapshot failed: {exc}\n")
            return
        self._log(f"History snapshot: {result.commit}")
        self.refresh_history()

    def create_archive(self) -> None:
        repo = self._active_repo()
        if not repo:
            messagebox.showinfo("Dew Encryption", "Select a file history folder first.")
            return
        try:
            archive = create_archive_for_repo(repo)
        except DewError as exc:
            self._set_details(f"Archive failed: {exc}\n")
            return
        self._log(f"Archive created: {archive}")

    def restore_selected(self) -> None:
        repo = self._active_repo()
        source = self._active_source()
        selected = self.history_tree.selection()
        if not repo or not source or not selected:
            messagebox.showinfo("Dew Encryption", "Select a source folder and a history commit first.")
            return
        commit = selected[0]
        if not messagebox.askyesno("Restore History", f"Restore {source} to commit {commit}? Current files at that path will be replaced."):
            return
        try:
            restore_commit(repo, commit, source)
        except DewError as exc:
            self._set_details(f"Restore failed: {exc}\n")
            return
        self._log(f"Restored {source} to {commit}")

    def open_source(self) -> None:
        source = self._active_source()
        if source:
            self._open_path(source)

    def open_repo(self) -> None:
        repo = self._active_repo()
        if repo:
            self._open_path(repo)

    def save_veracrypt_settings(self) -> None:
        vc = self.settings.veracrypt
        try:
            vc.encryption = str(self.setting_vars["encryption"].get())
            vc.hash = str(self.setting_vars["hash"].get())
            vc.filesystem = str(self.setting_vars["filesystem"].get())
            vc.pim = str(self.setting_vars["pim"].get())
            vc.size_padding_mb = int(str(self.setting_vars["size_padding_mb"].get()))
            vc.size_multiplier = float(str(self.setting_vars["size_multiplier"].get()))
            vc.minimum_size_mb = int(str(self.setting_vars["minimum_size_mb"].get()))
            vc.veracrypt_path = str(self.setting_vars["veracrypt_path"].get())
            vc.keep_source_after_encrypt = bool(self.setting_vars["keep_source_after_encrypt"].get())
            vc.keep_container_after_decrypt = bool(self.setting_vars["keep_container_after_decrypt"].get())
        except ValueError as exc:
            messagebox.showerror("VeraCrypt Settings", f"Invalid numeric setting: {exc}")
            return
        save_settings(self.settings)
        self._log("VeraCrypt settings saved.")

    def refresh_history(self) -> None:
        for item in self.history_tree.get_children():
            self.history_tree.delete(item)
        repo = self._active_repo()
        if not repo:
            self._set_details("Select a file or folder with Dew Encryption history first.\n")
            return
        try:
            for entry in history(repo):
                self.history_tree.insert("", "end", iid=entry.commit, values=(entry.commit, entry.author_date, entry.subject))
        except DewError as exc:
            self._set_details(f"Failed to load history: {exc}\n")

    def show_history_details(self, _event: object | None = None) -> None:
        repo = self._active_repo()
        selected = self.history_tree.selection()
        if not repo or not selected:
            return
        commit = selected[0]
        try:
            message, changes = commit_details(repo, commit)
        except DewError as exc:
            self._set_details(f"Failed to load commit details: {exc}\n")
            return
        lines = [message, "", "Changed files:"]
        lines.extend(f"{change.status}  {change.path}" for change in changes)
        self._set_details("\n".join(lines) + "\n")

    def _add(self, path: Path) -> None:
        path = path.resolve()
        if path in self.selected:
            return
        self.selected.append(path)
        kind = "Folder" if path.is_dir() else "File"
        self.tree.insert("", "end", iid=str(path), values=(str(path), kind))

    def _log(self, text: str) -> None:
        self.log.configure(state="normal")
        self.log.insert("end", text.rstrip() + "\n")
        self.log.see("end")
        self.log.configure(state="disabled")

    def _drain_events(self) -> None:
        while True:
            try:
                message = self.events.get_nowait()
                refresh = "::refresh-history::" in message
                self._log(message.replace("::refresh-history::", "").rstrip())
                if refresh:
                    self.refresh_history()
            except queue.Empty:
                break
        self.after(100, self._drain_events)

    def _watch_worker(self, paths: list[Path]) -> None:
        last_commit = ""
        while not self.watch_stop.is_set():
            try:
                result = snapshot(paths)
                if result.commit and result.commit != last_commit:
                    self.events.put(f"Auto history commit: {result.commit}\n::refresh-history::")
                    last_commit = result.commit
            except DewError as exc:
                self.events.put(f"Auto history failed: {exc}\n")
            self.watch_stop.wait(5.0)

    def _active_repo(self) -> Path | None:
        source = self._active_source()
        if not source:
            return None
        return repo_for_source(source)

    def _active_source(self) -> Path | None:
        if not self.selected:
            return None
        return self.selected[0].resolve()

    def _open_path(self, path: Path) -> None:
        if os.name == "nt":
            subprocess.Popen(["explorer", str(path)])
        elif sys.platform == "darwin":
            subprocess.Popen(["open", str(path)])
        else:
            subprocess.Popen(["xdg-open", str(path)])

    def _set_details(self, text: str) -> None:
        self.details.configure(state="normal")
        self.details.delete("1.0", "end")
        self.details.insert("end", text)
        self.details.configure(state="disabled")


def main() -> None:
    args = sys.argv[1:]
    open_history = "--history" in args
    upload_path = Path(args[args.index("--docker-upload") + 1]) if "--docker-upload" in args and args.index("--docker-upload") + 1 < len(args) else None
    save_dir = Path(args[args.index("--docker-save-here") + 1]) if "--docker-save-here" in args and args.index("--docker-save-here") + 1 < len(args) else None
    skip = {"--history", "--docker-upload", "--docker-save-here"}
    values_to_skip = {str(upload_path), str(save_dir)}
    paths = [Path(arg) for arg in args if arg not in skip and arg not in values_to_skip]
    app = DewFileManager(paths, open_history=open_history)
    if upload_path:
        app.after(100, lambda: app.open_docker_upload_dialog(upload_path))
    if save_dir:
        app.after(100, lambda: app.open_docker_save_dialog(save_dir))
    app.mainloop()


if __name__ == "__main__":
    main()
