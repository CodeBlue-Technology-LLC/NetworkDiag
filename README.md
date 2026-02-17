# MSP Network Diagnostics

A fast, single-file network diagnostics tool built for MSP engineers. Designed to run from the **ScreenConnect Backstage** toolbox — click, run, done.

![Platform](https://img.shields.io/badge/platform-Windows-blue) ![.NET](https://img.shields.io/badge/.NET%20Framework-4.7.2-orange) ![License](https://img.shields.io/badge/license-Dual%20License-green)

---

## What It Does

Launches instantly and runs all common triage tests automatically — no configuration, no dependencies, no installer.

- **Network Interfaces** — Active NICs with IP, subnet, gateway, and MAC
- **Ping Tests** — NIC gateway(s), `1.1.1.1`, and `gateway.codeblue.cloud` with avg ms over 4 pings
- **DNS Resolution** — Local hostname, google.com, cloudflare.com, gateway.codeblue.cloud
- **WAN / External** — Public IPv4 via `ifconfig.me` (forced IPv4), HTTP round-trip latency
- **Interface Throughput** — 1-second sample, upload and download in MB/s
- **Disk Activity** — Per-drive read/write MB/s and free space
- **CPU Usage** — 1-second overall CPU % sample
- **Status Summary** — Green OK / Yellow Warning / Red Problem at the bottom with the first detected issue

---

## Usage

1. Drop `MspDiag.exe` into your ScreenConnect toolbox
2. Launch via Backstage on the target machine
3. Results stream in automatically — no clicks needed
4. Use **Copy Results** to paste plain text into a ticket
5. Use **Re-run** to run all tests again without reopening

---

## Requirements

- Windows 7 or later
- .NET Framework 4.7.2 or later (pre-installed on all modern Windows)
- No additional dependencies

---

## Building from Source

1. Open in Visual Studio 2022
2. Target framework: `.NET Framework 4.7.2`
3. Build → Build Solution
4. Output: `bin\Release\net472\MspDiag.exe` — single file, no DLLs

> Ensure your `.csproj` does **not** contain `<Nullable>enable</Nullable>` or `<ImplicitUsings>enable</ImplicitUsings>` — these are .NET 6+ settings incompatible with .NET Framework.

---

## License

This project uses a **dual license** model. See [`LICENSE-FREE.md`](LICENSE-FREE.md) and [`LICENSE-COMMERCIAL.md`](LICENSE-COMMERCIAL.md) for full terms.

| Use Case | License Required |
|---|---|
| Personal / individual use | Free |
| MSP or IT team with fewer than 10 staff | Free |
| MSP or IT team with 10 or more staff | Commercial license required |
| Redistribution or white-labelling | Commercial license required |

For commercial licensing, contact **CodeBlue** — details in [`LICENSE-COMMERCIAL.md`](LICENSE-COMMERCIAL.md).

---

## Contributing

This is a source-available project. You may view and fork the code for personal or qualifying free use. Pull requests are welcome for bug fixes. Please do not redistribute modified versions without a commercial license.

---

*Built by [CodeBlue Technology LLC](https://codebluetechnology.com)*
