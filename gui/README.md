# OpenFortiVPN GUI

A modern, production-grade Windows desktop application for managing FortiGate SSL VPN connections. Built as a graphical wrapper around the [openfortivpn](https://github.com/adrienverge/openfortivpn) CLI tool.

## Features

- **One-click VPN connections** — connect from the dashboard or system tray
- **Multi-profile management** — save, edit, duplicate, import/export configurations
- **Secure credential storage** — passwords encrypted with Windows DPAPI
- **Real-time connection status** — animated indicators, connection timer, IP/DNS info
- **System tray integration** — minimize to tray, desktop notifications
- **Two-factor authentication** — OTP prompt and SAML browser login support
- **Full log viewer** — searchable, filterable, color-coded, exportable
- **Progressive disclosure** — basic settings visible by default, advanced options behind expanders
- **Auto-reconnection** — configurable persistent reconnection
- **Import/Export** — compatible with openfortivpn config file format

## Architecture

```
Presentation (WPF/XAML)  →  ViewModels (MVVM)  →  Services (DI)  →  openfortivpn.exe
```

- **MVVM Pattern** with CommunityToolkit.Mvvm (source generators)
- **Dependency Injection** via Microsoft.Extensions.DependencyInjection
- **Structured Logging** with Serilog (rolling file sink)
- **ViewModel-First Navigation** — DataTemplates auto-resolve Views
- **Secure by Default** — passwords never in CLI args, DPAPI encryption, memory scrubbing

See [ARCHITECTURE.md](ARCHITECTURE.md) for the full design document.

## Requirements

- Windows 10 or later
- .NET 8.0 Runtime
- `openfortivpn.exe` and `wintun.dll` (bundled or in PATH)
- Administrator privileges (for TUN adapter and route management)

## Building

```bash
cd gui
dotnet restore
dotnet build
dotnet run --project OpenFortiVPN.GUI
```

## Project Structure

```
gui/
├── ARCHITECTURE.md              # Full design document
├── OpenFortiVPN.GUI.sln         # Visual Studio solution
├── OpenFortiVPN.GUI/
│   ├── App.xaml(.cs)            # Application bootstrap + DI
│   ├── Models/                  # Data models (VpnProfile, ConnectionInfo, etc.)
│   ├── Services/                # Business logic (VPN, profiles, credentials, etc.)
│   ├── ViewModels/              # MVVM ViewModels
│   ├── Views/                   # XAML Views
│   ├── Controls/                # Custom WPF controls
│   ├── Converters/              # Value converters for data binding
│   ├── Helpers/                 # Utility classes
│   ├── Themes/                  # Color system, typography, control styles
│   └── Assets/                  # Icons and images
```

## Security

- Passwords are **never** passed as command-line arguments (visible in process lists)
- Credentials are piped to openfortivpn via stdin
- Stored passwords are encrypted with **Windows DPAPI** (tied to user account)
- Memory containing passwords is zeroed immediately after use
- Certificate trust is managed through SHA256 fingerprint pinning

## License

Licensed under the GNU General Public License v3.0, consistent with the parent openfortivpn project.
