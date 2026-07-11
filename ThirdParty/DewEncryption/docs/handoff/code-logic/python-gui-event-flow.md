# Code Logic Handoff: Python GUI Event Flow

## Structure

- `DewFileManager` subclasses `tk.Tk`.
- `_build` creates top bar, Files tab, History tab, VeraCrypt tab, and Containers tab.
- `_build_container_manager` creates the container profile editor and history grid.

## Threading model

- Long-running archive work runs in a daemon worker thread.
- Auto-history runs in a daemon polling thread controlled by `watch_stop`.
- Worker threads put messages into `self.events`.
- `_drain_events` runs on the Tk event loop every 100 ms and updates widgets safely.

## Active-source model

- The first selected path is the active history source.
- History repo/open/restore actions derive from that active source.
