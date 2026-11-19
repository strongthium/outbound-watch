<p align="center">
  <a href="https://strongthium.com" title="Strongthium Labs"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/banner-2200x440.png" alt="Strongthium Labs: Take back control of your internet."></a>
</p>

[outbound-watch](../README.md) › [Manual](README.md) › Administrator or not

# Administrator or not

`outbound-watch` runs with the rights it's started with, and watches what those rights allow. It never asks for
administrator rights by itself: **Restart as administrator** does, when you click it.

## What each sees

| | Without administrator rights | As administrator |
|---|---|---|
| **How** | reads Windows' table of TCP connections once a second (`GetExtendedTcpTable`), which any user can | also reads the kernel's own network events as they happen (ETW: `Microsoft-Windows-Kernel-Network`) |
| **TCP** | yes, except connections that open and close within a second | every one |
| **UDP** (QUIC, DNS, games, calls) | no | yes; a UDP "connection" ends after 2 minutes without a packet |
| **Bytes sent and received** | no: **—** in their columns, and on Totals' chart | yes, per connection, and by the minute for the chart on [Totals](totals.md) |
| **Names** | Windows' DNS cache (what `ipconfig /displaydns` shows), read every few seconds; then, unless it's turned off, a reverse lookup of the address | the name each program looked up, as it's looked up (ETW: `Microsoft-Windows-DNS-Client`), and Windows' DNS cache; then a reverse lookup |

Below the pages, at the bottom left, the window says which:

| | |
|---|---|
| *Watching TCP and UDP, with the bytes each connection sends and receives.* | as administrator |
| *Watching TCP only, without bytes:* and why | without administrator rights, or when the network events couldn't be read: see [Messages](messages.md) |
| *Sample data: a made-up PC. Nothing on this one is watched.* | the sample |

## Restart as administrator

**Restart as administrator** (bottom left, and in [Settings](settings.md); its tooltip: *Start it again with the
rights to count bytes and see UDP: Windows asks first*) starts it again with administrator rights. Windows asks first.
If you say yes, the new copy starts and this one quits; if you say no, nothing changes. A copy started with `--data`
keeps its data folder.

To start with administrator rights every time you sign in, turn on **Start outbound-watch in the tray when I sign in**
while running as administrator: it's then a task in the Task Scheduler, and Windows doesn't ask at each sign-in. See
[Settings](settings.md).

## Which connections are watched

A connection is outgoing when this PC opened it. Left out:

- incoming connections: to a port that's listening here, and UDP that started from outside;
- connections to this PC's own addresses, loopback and multicast;
- connections inside the local network (the router, a NAS, other PCs), unless **Watch connections inside the local
  network too** is on in [Settings](settings.md).

Connections already open when watching begins are counted from then, and marked *≥* in **Open for**.

← [The tray icon](tray.md) · [Contents](README.md) · [Countries and networks](countries-and-networks.md) →

<!-- strongthium-links -->

---

<p align="center"><sub>
<b>Strongthium open-source tools:</b> <a href="https://github.com/strongthium/emudhra-security-patch">eMudhra Security Patch</a> · <a href="https://github.com/strongthium/folder-size">Folder Size</a> · <a href="https://github.com/strongthium/health-reminder">Health Reminder</a> · <a href="https://github.com/strongthium/host-dossier">Host Dossier</a> · <b>Outbound Watch</b> · <a href="https://github.com/strongthium/raw-convert">RAW Convert</a> · <a href="https://github.com/strongthium/video-dispatch">Video Dispatch</a> · <a href="https://github.com/strongthium/webdav-server">WebDAV Server</a><br>
<b>More from Strongthium:</b> <a href="https://strongthium.com/linux-server-firewall">Guard</a> · <a href="https://strongthium.com/self-hosted-mail-server">Mail</a> · <a href="https://strongthium.com/self-hosted-live-chat">Chat</a> · <a href="https://strongthium.com/windows-ssh-client">Remote</a> · <a href="https://strongthium.com/twilio-sms-desktop-app">SMS</a> · <a href="https://strongthium.com/time-tracking-invoicing-software">Work</a> · <a href="https://strongthium.com/windows-registry-editor">Registry</a> · <a href="https://strongthium.com/cfml-server">DotCF</a> · <a href="https://strongthium.com/video-conferencing-dial-up-modem">Convey</a><br>
<a href="https://strongthium.com">Strongthium Labs</a> · Take back control of your internet.
</sub></p>
