<p align="center">
  <img src="assets/logo/configuro-logo-512.png" width="140" alt="ConfigurO">
</p>

<h1 align="center">ConfigurO</h1>

<p align="center">
  Windows configuration, privacy and cleanup — in one window.<br>
  <sub>by <b>WRSTT</b> · Windows 7–11 · .NET Framework 4.8</sub>
</p>

---

> **ConfigurO is an attempt to continue the progress of the original Optimizer project while keeping it a full graphical Windows application.**
>
> Moving a tool like Optimizer primarily toward the command line does not make sense for many of the users it originally served. ConfigurO keeps the GUI-first approach while updating features, compatibility and functionality where possible.
>
> **This is an independent continuation/fork. Features are still being updated and there are no guarantees that everything is fully functional on every Windows version or configuration.**

ConfigurO brings Windows settings that are normally scattered across registry editors, policy menus, system dialogs and utilities into one dark, compact interface.

## Tools

| Tool           | What it does                                                         |
| -------------- | -------------------------------------------------------------------- |
| **Tweaks**     | Registry, service and policy switches, grouped and searchable        |
| **Cleaner**    | Measure and clear temporary files, dumps, reports and browser caches |
| **Startup**    | Enable, disable or remove programs that launch at sign-in            |
| **Hosts**      | Edit the hosts file, apply block lists and optionally lock it        |
| **Apps**       | Download and install common applications directly from their vendors |
| **Network**    | Ping, change DNS providers and flush the resolver cache              |
| **UWP Apps**   | View installed Windows packages and uninstall them in bulk           |
| **Hardware**   | View CPU, memory, GPU, storage, motherboard and network information  |
| **Integrator** | Add desktop context-menu entries, Run commands and utility shortcuts |
| **Settings**   | Language, application behaviour, updates and troubleshooting         |

## Install

Download `ConfigurO.exe` from [Releases](https://github.com/wrstt/ConfigurO/releases).

ConfigurO is distributed as a single executable with no installer required.

Administrator privileges are required for features that modify system-wide registry keys, services, policies or protected files.

## Command Line

The GUI is the primary interface, but ConfigurO also supports command-line automation.

See [docs/CONFS.md](docs/CONFS.md) and [docs/AUTOMATION.md](docs/AUTOMATION.md).

```text
ConfigurO.exe /config=template-windows11.json
ConfigurO.exe /disable=uwp,apps
ConfigurO.exe /repair
```

## Build

Requires Visual Studio 2022 or MSBuild with the .NET Framework 4.8 targeting pack.

```text
nuget restore ConfigurO.sln
msbuild ConfigurO.sln /p:Configuration=Release
```

## Development

The interface uses the **Nocturne** design system.

Theme tokens, colours, geometry and typography are centralized in:

[`NocturneTheme.cs`](src/ConfigurO/Nocturne/NocturneTheme.cs)

Additional development information is available in [docs/DEVELOPING.md](docs/DEVELOPING.md).

## License

ConfigurO is licensed under [GPL-3.0](LICENSE).

It began as a fork of the **Optimizer** project and retains its GPL licensing requirements.

Bundled third-party assets and acknowledgements are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
