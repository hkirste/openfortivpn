# OpenFortiVPN GUI - Architecture & Design Document

## Executive Summary

OpenFortiVPN GUI is a production-grade Windows desktop application that provides
a beautiful, intuitive interface for managing FortiGate SSL VPN connections. It
wraps the existing `openfortivpn` CLI tool while adding enterprise-ready features
like multi-profile management, system tray integration, and automatic reconnection.

---

## 1. Design Philosophy & Principles

### 1.1 Core Principles

1. **Zero-Knowledge UX**: A non-technical user should be able to connect to their
   VPN with just a server address, username, and password. No knowledge of PPP,
   TLS, routes, or DNS configuration should be required.

2. **Progressive Disclosure**: Advanced options (certificates, split tunneling,
   custom DNS) are available but hidden behind expandable sections. Power users
   get full control; casual users see only what they need.

3. **Fail-Safe Defaults**: Every default setting should be the safe, correct
   choice for 95% of users. Insecure options require explicit opt-in with clear
   warnings.

4. **Connection State Transparency**: Users always know exactly what state their
   VPN is in through visual indicators (color, icons, animations), system tray
   status, and optional desktop notifications.

5. **Resilient Connectivity**: Automatic reconnection with exponential backoff,
   network change detection, and graceful degradation when connectivity is lost.

### 1.2 UX Goals

- **Time to first connection**: < 60 seconds from install
- **Daily workflow**: Single click from system tray to connect
- **Error recovery**: Actionable error messages with suggested fixes
- **Accessibility**: Full keyboard navigation, screen reader support, high contrast

### 1.3 Architecture Goals

- **Separation of Concerns**: Clean MVVM with no business logic in views
- **Testability**: All services behind interfaces, ViewModels unit-testable
- **Extensibility**: Plugin-ready architecture for future auth methods
- **Security**: Credentials encrypted at rest, memory scrubbed after use
- **Performance**: < 50MB RAM idle, < 2s startup, zero UI lag during tunnel ops

---

## 2. Technology Stack

| Layer              | Technology                    | Rationale                                   |
|--------------------|-------------------------------|---------------------------------------------|
| Framework          | .NET 8 (LTS)                 | Long-term support, modern C#, AOT-ready     |
| UI Framework       | WPF                          | Mature, MVVM-native, rich styling system    |
| MVVM Toolkit       | CommunityToolkit.Mvvm        | Source generators, minimal boilerplate       |
| DI Container       | Microsoft.Extensions.DI      | Standard .NET DI, familiar to all devs       |
| Logging            | Serilog                      | Structured logging, file + event sinks       |
| Secure Storage     | DPAPI (Windows)              | OS-level credential encryption               |
| Settings           | JSON (System.Text.Json)      | Human-readable, version-controllable         |
| Installer          | WiX Toolset v4               | MSI packages, enterprise deployment ready    |
| Auto-Update        | Squirrel.Windows or MSIX     | Seamless background updates                  |
| Design System      | Custom Fluent-inspired theme  | Modern Windows 11 aesthetics                 |

---

## 3. Application Architecture

### 3.1 High-Level Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Presentation Layer                     │
│  ┌──────────┐  ┌──────────┐  ┌───────────┐  ┌────────┐ │
│  │  Views   │  │ Controls │  │ Converters│  │ Themes │ │
│  │  (XAML)  │  │ (Custom) │  │ (Binding) │  │ (XAML) │ │
│  └────┬─────┘  └──────────┘  └───────────┘  └────────┘ │
│       │ DataBinding                                      │
│  ┌────▼─────────────────────────────────────────────┐   │
│  │              ViewModels (MVVM)                     │   │
│  │  MainVM │ ConnectionVM │ ProfileVM │ SettingsVM   │   │
│  └────┬──────────────────────────────────────────────┘   │
└───────┼──────────────────────────────────────────────────┘
        │ Dependency Injection
┌───────▼──────────────────────────────────────────────────┐
│                    Service Layer                          │
│  ┌───────────┐ ┌──────────┐ ┌──────────┐ ┌───────────┐ │
│  │ VPN       │ │ Profile  │ │ Credential│ │ Notification│
│  │ Service   │ │ Service  │ │ Service   │ │ Service    │ │
│  ├───────────┤ ├──────────┤ ├──────────┤ ├───────────┤ │
│  │ Log       │ │ Settings │ │ Update   │ │ Network    │ │
│  │ Service   │ │ Service  │ │ Service  │ │ Monitor    │ │
│  └─────┬─────┘ └──────────┘ └──────────┘ └───────────┘ │
└────────┼─────────────────────────────────────────────────┘
         │
┌────────▼─────────────────────────────────────────────────┐
│                  Infrastructure Layer                     │
│  ┌─────────────┐  ┌──────────┐  ┌────────────────────┐  │
│  │ Process     │  │ DPAPI    │  │ Windows Registry   │  │
│  │ Manager     │  │ Wrapper  │  │ & File System      │  │
│  │ (CLI wrap)  │  │          │  │                    │  │
│  └──────┬──────┘  └──────────┘  └────────────────────┘  │
└─────────┼────────────────────────────────────────────────┘
          │
    ┌─────▼─────┐
    │openfortivpn│  (Native CLI executable)
    │   .exe     │
    └───────────┘
```

### 3.2 MVVM Pattern Details

**Views** (XAML): Pure presentation, no code-behind logic except window
chrome management. All state and behavior accessed via DataBinding.

**ViewModels**: Implement `ObservableObject`, expose `RelayCommand` and
`AsyncRelayCommand`. Never reference Views or UI types. Receive services
via constructor injection.

**Models**: Plain C# records/classes representing domain entities (VPN
profiles, connection state, log entries). Immutable where possible.

**Services**: Business logic behind `I*Service` interfaces. Registered
as singletons or transient in DI container. Enable unit testing with mocks.

### 3.3 Navigation Architecture

Single-window application with content-area navigation:

```
┌──────────────────────────────────────────────┐
│ ┌─────────┐  OpenFortiVPN        ─  □  ✕    │
│ │  Logo   │                                  │
│ ├─────────┤──────────────────────────────────│
│ │ ● Home  │                                  │
│ │   Profs │    [Content Area]                │
│ │   Logs  │                                  │
│ │   Conf  │    Loaded Views:                 │
│ │         │    - DashboardView               │
│ │         │    - ProfileEditorView            │
│ │         │    - LogViewerView               │
│ │         │    - SettingsView                │
│ │         │                                  │
│ ├─────────┤                                  │
│ │ Status  │                                  │
│ │ ● Ready │                                  │
│ └─────────┘──────────────────────────────────│
└──────────────────────────────────────────────┘
```

---

## 4. Feature Specification

### 4.1 Connection Management

| Feature                    | Priority | Description                                    |
|----------------------------|----------|------------------------------------------------|
| Quick Connect              | P0       | One-click connect from dashboard or tray       |
| Multi-Profile              | P0       | Save/load multiple VPN configurations          |
| Auto-Reconnect             | P0       | Configurable persistent reconnection           |
| Connection Status          | P0       | Real-time state display with duration/bytes    |
| Disconnect                 | P0       | Graceful disconnect with route cleanup         |
| System Tray                | P0       | Minimize to tray, tray context menu            |
| SAML Authentication        | P1       | Embedded WebView2 for SAML/SSO flows           |
| OTP/2FA Prompt             | P0       | Modal dialog for token entry during auth       |
| Certificate Selection      | P1       | File picker + Windows cert store browser       |
| Import/Export Profiles     | P2       | JSON export, openfortivpn config import        |

### 4.2 User Interface

| Feature                    | Priority | Description                                    |
|----------------------------|----------|------------------------------------------------|
| Dashboard                  | P0       | Connection status, quick actions, recent logs  |
| Profile Editor             | P0       | Form-based profile configuration               |
| Log Viewer                 | P0       | Searchable, filterable, color-coded logs       |
| Settings Panel             | P1       | App-level preferences (startup, theme, etc.)   |
| Dark/Light Theme           | P1       | System-aware theme switching                   |
| Notifications              | P0       | Toast notifications for state changes          |
| Onboarding Wizard          | P2       | First-run guided setup                         |
| Keyboard Shortcuts         | P1       | Ctrl+Q quit, Ctrl+N new profile, etc.          |

### 4.3 Security Features

| Feature                    | Priority | Description                                    |
|----------------------------|----------|------------------------------------------------|
| Credential Encryption      | P0       | DPAPI-encrypted password storage               |
| Memory Scrubbing           | P0       | SecureString for passwords in memory           |
| Certificate Pinning UI     | P1       | Visual cert fingerprint management             |
| Insecure Mode Warnings     | P0       | Clear warnings for --insecure-ssl              |
| Admin Elevation            | P0       | UAC prompt for route/adapter operations        |

### 4.4 Enterprise Features

| Feature                    | Priority | Description                                    |
|----------------------------|----------|------------------------------------------------|
| Silent Install (MSI)       | P1       | GPO-deployable MSI installer                   |
| Pre-configured Profiles    | P2       | Deploy profiles via registry/config file       |
| Auto-Update                | P2       | Background update check and install            |
| Diagnostic Export          | P1       | One-click log/config export for support        |

---

## 5. Data Models

### 5.1 VPN Profile

```csharp
public record VpnProfile
{
    public Guid Id { get; init; }
    public string Name { get; set; }
    public string GatewayHost { get; set; }
    public int GatewayPort { get; set; } = 443;
    public string Username { get; set; }
    // Password stored encrypted via ICredentialService
    public string? Realm { get; set; }
    public string? SniOverride { get; set; }

    // Authentication
    public AuthMethod AuthMethod { get; set; }
    public string? UserCertPath { get; set; }
    public string? UserKeyPath { get; set; }
    public string? CaFilePath { get; set; }
    public List<string> TrustedCertDigests { get; set; }

    // Network
    public bool SetRoutes { get; set; } = true;
    public bool SetDns { get; set; } = true;
    public bool HalfInternetRoutes { get; set; } = false;

    // Advanced
    public bool InsecureSsl { get; set; } = false;
    public string? CipherList { get; set; }
    public TlsVersion MinTlsVersion { get; set; }
    public bool SecurityLevel1 { get; set; } = false;
    public int PersistentInterval { get; set; } = 0;
    public string? CustomDnsSuffix { get; set; }
}
```

### 5.2 Connection State

```csharp
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Authenticating,
    NegotiatingTunnel,
    Connected,
    Reconnecting,
    Disconnecting,
    Error
}
```

### 5.3 Application Settings

```csharp
public record AppSettings
{
    public bool StartWithWindows { get; set; } = false;
    public bool StartMinimized { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    public bool AutoConnectLastProfile { get; set; } = false;
    public ThemeMode Theme { get; set; } = ThemeMode.System;
    public LogLevel LogVerbosity { get; set; } = LogLevel.Info;
    public int MaxLogLines { get; set; } = 10000;
    public string OpenFortiVpnPath { get; set; } = "openfortivpn.exe";
    public string? ProxyAddress { get; set; }
}
```

---

## 6. CLI Integration Strategy

### 6.1 Process Management

The GUI wraps `openfortivpn.exe` as a child process:

```
GUI (C#) ──stdin──▶ openfortivpn.exe
GUI (C#) ◀──stdout── openfortivpn.exe
GUI (C#) ◀──stderr── openfortivpn.exe
```

**Key Design Decisions:**

1. **Password via stdin**: Never pass passwords as CLI arguments (visible in
   process list). Use `--cookie-on-stdin` pattern or pipe password.

2. **Output Parsing**: Parse stderr for connection state transitions using
   regex patterns matched against known log messages.

3. **Graceful Termination**: Send SIGTERM equivalent (Ctrl+C via
   GenerateConsoleCtrlEvent) for clean disconnect.

4. **Process Supervision**: Monitor process health via exit code and stdout
   for unexpected termination.

### 6.2 State Machine Mapping

CLI output → GUI state transitions:

```
"Resolving gateway"          → Connecting
"Connected to gateway"       → Authenticating
"Authenticated"              → NegotiatingTunnel
"Tunnel is up and running"   → Connected
"Tunnel went down"           → Reconnecting (if persistent)
"Logged out"                 → Disconnected
[process exit non-zero]      → Error
```

---

## 7. Security Architecture

### 7.1 Credential Storage

```
User enters password
        │
        ▼
SecureString (in-memory, encrypted)
        │
        ▼ (on save)
DPAPI ProtectedData.Protect()
        │
        ▼
Base64 encoded → profiles.json (encrypted blob)
        │
        ▼ (on connect)
DPAPI ProtectedData.Unprotect()
        │
        ▼
Pipe to openfortivpn stdin
        │
        ▼
Zero-fill memory immediately
```

### 7.2 Threat Model

| Threat                     | Mitigation                                     |
|----------------------------|-------------------------------------------------|
| Password in process args   | Use stdin pipe, never --password CLI arg         |
| Credential file theft      | DPAPI encryption tied to Windows user account    |
| Memory dump attack         | SecureString + explicit memory zeroing           |
| Man-in-the-middle          | Certificate pinning UI, --trusted-cert support   |
| Privilege escalation       | Minimal UAC scope, drop privileges after setup   |
| Log credential leak        | Filter sensitive data from log display           |

---

## 8. Error Handling Strategy

### 8.1 User-Facing Error Messages

Every error shown to users follows this template:

```
┌─────────────────────────────────────────┐
│ ⚠ Connection Failed                     │
│                                         │
│ Could not authenticate with the VPN     │
│ gateway. Your password may be incorrect │
│ or your account may be locked.          │
│                                         │
│ What to try:                            │
│ • Verify your username and password     │
│ • Check if your account is locked       │
│ • Contact your IT administrator         │
│                                         │
│ [Show Technical Details]  [Retry] [OK]  │
└─────────────────────────────────────────┘
```

### 8.2 Error Classification

```csharp
public enum ErrorCategory
{
    NetworkUnreachable,     // DNS failure, no internet
    AuthenticationFailed,   // Wrong credentials, locked account
    CertificateError,       // Untrusted cert, expired cert
    TunnelSetupFailed,      // PPP negotiation failure
    PermissionDenied,       // Need admin rights
    ConfigurationError,     // Invalid settings
    ProcessError,           // openfortivpn.exe not found/crashed
    Unknown                 // Fallback with raw error
}
```

---

## 9. Testing Strategy

| Test Type        | Scope                          | Tool             |
|------------------|--------------------------------|------------------|
| Unit Tests       | ViewModels, Services, Models   | xUnit + Moq      |
| Integration      | CLI wrapper, file I/O          | xUnit             |
| UI Automation    | Critical user flows            | Appium/WinAppSDK |
| Manual           | Visual regression, UX review   | Checklist         |

---

## 10. Deployment & Distribution

### 10.1 Installer Contents

```
OpenFortiVPN/
├── OpenFortiVPN.GUI.exe        (Main application)
├── openfortivpn.exe            (CLI tool, bundled)
├── wintun.dll                  (WinTUN driver)
├── OpenFortiVPN.GUI.dll        (Application assembly)
├── *.dll                       (.NET runtime if self-contained)
└── resources/
    └── default-config.json     (Default settings template)
```

### 10.2 Installation Options

1. **MSI Installer** (WiX): Enterprise deployment via GPO/SCCM
2. **MSIX Package**: Microsoft Store distribution, auto-update
3. **Portable ZIP**: No install required, carry on USB

---

## 11. Future Roadmap

| Phase   | Features                                                |
|---------|---------------------------------------------------------|
| v1.0    | Core connection, profiles, tray, notifications          |
| v1.1    | SAML via WebView2, certificate store browser            |
| v1.2    | Auto-update, diagnostic export, onboarding wizard       |
| v2.0    | Multi-connection, bandwidth graph, connection analytics  |
| v2.1    | macOS/Linux GUI (Avalonia UI migration)                 |
| v3.0    | Split tunnel visualization, route editor, VPN policy    |
