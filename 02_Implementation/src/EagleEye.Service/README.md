# EagleEye.Service

Windows service running as `SYSTEM`. The core enforcement engine of EagleEye.

## Responsibility

- Monitor all running processes in the kid's Windows user session
- Enforce app rules: attempt graceful shutdown, then force-kill non-allowed processes
- Manage per-user time budgets and pause windows (counts down, resets at midnight)
- Serve a SignalR hub for parent apps and the tray client
- Manage TLS certificate (auto-generated, self-signed, non-expiring)
- Collect and store application usage statistics per user account
- Auto-discover local standard (non-admin) Windows user accounts

## Component Structure

```
EagleEye.Service/
├── Monitoring/       Process monitoring — discovers running processes per user session
├── Enforcement/      Enforcement engine — graceful shutdown + force-kill logic
├── Configuration/    Per-user configuration management — rules, budgets, schedules
├── Statistics/       Usage statistics collection and storage
├── Communication/    SignalR hub (server side) — serves parent apps + tray client
└── Certificates/     Self-signed TLS certificate generation and management
```

## Runtime

- Runs as Windows Service under the `SYSTEM` account
- Installed and managed by the Inno Setup installer (`installer/windows/setup.iss`)
- Communicates with `EagleEye.TrayClient` via SignalR over localhost
- Communicates with parent apps via SignalR over local LAN (HTTPS, self-signed cert)
