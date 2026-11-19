<p align="center">
  <a href="https://strongthium.com" title="Strongthium Labs"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/banner-2200x440.png" alt="Strongthium Labs: Take back control of your internet."></a>
</p>

[outbound-watch](../README.md) › [Manual](README.md) › Your data

# Your data

Everything `outbound-watch` keeps stays on this PC, in one folder, and it sends nothing about your connections
anywhere.

## Where it's kept

`%LocalAppData%\Strongthium\outbound-watch`, or the folder given with `--data` (see
[The command line](command-line.md)). **Open the folder** in [Settings](settings.md) opens it.

| | |
|---|---|
| `outbound-watch.db` | a SQLite database: every connection in the history, the bytes sent and received a minute, the expected lists and the settings. SQLite keeps `outbound-watch.db-wal` and `outbound-watch.db-shm` beside it while it's open |
| `dbip-country-lite-<month>.csv.gz`, `dbip-asn-lite-<month>.csv.gz` | DB-IP's address databases, once they're downloaded: see [Countries and networks](countries-and-networks.md) |
| `instance.lock` | held while a copy runs, so that only one runs on the folder |

For each connection the history keeps: the protocol, the program (its process number, name, path and services), the
addresses and ports at both ends, the name and where it came from, the country, AS number and organization, when it
started, was last seen and ended, whether it was already open when watching began, the bytes each way, the last TCP
state, and the list it was on.

## How long

The history is kept for 90 days unless you choose otherwise (**7 days**, **30 days**, **A year** or **Everything**,
under **Keep** in Settings). **Clear the history…** deletes it all at once; the expected lists and settings stay.

## Outside the folder

| | |
|---|---|
| A value called `Strongthium outbound-watch` in `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`, or a task of that name in the Task Scheduler | only while **Start outbound-watch in the tray when I sign in** is on. Turning it off removes it |
| A trace session called `Strongthium outbound-watch` | only while it runs as administrator: how it reads the kernel's network events |

## What it sends over the network

| | |
|---|---|
| DB-IP's files, from `download.db-ip.com` | when you click **Download** or **Update now**, or once a month with **Download each month's new ones** on |
| Reverse lookups, through the PC's own DNS server | an address's name, as `nslookup` would, for a connection no name was seen for. Turned off in Settings |
| The latest version, from `api.github.com` | when [About](about.md) is shown or **Check again** is pressed, and the release's files from GitHub when you update |

Each of these is a connection of its own, and shows up among the connections too. Nothing else is sent.

## Removing it

Turn off **Start outbound-watch in the tray when I sign in** (as administrator, if it starts as administrator), choose
**Quit (stop watching)** in the tray icon's menu, and delete the folder above and `outbound-watch.exe`.

← [The command line](command-line.md) · [Contents](README.md) · [Messages](messages.md) →

<!-- strongthium-links -->

---

<p align="center"><sub>
<b>Strongthium open-source tools:</b> <a href="https://github.com/strongthium/emudhra-security-patch">eMudhra Security Patch</a> · <a href="https://github.com/strongthium/folder-size">Folder Size</a> · <a href="https://github.com/strongthium/health-reminder">Health Reminder</a> · <a href="https://github.com/strongthium/host-dossier">Host Dossier</a> · <b>Outbound Watch</b> · <a href="https://github.com/strongthium/raw-convert">RAW Convert</a> · <a href="https://github.com/strongthium/video-dispatch">Video Dispatch</a> · <a href="https://github.com/strongthium/webdav-server">WebDAV Server</a><br>
<b>More from Strongthium:</b> <a href="https://strongthium.com/linux-server-firewall">Guard</a> · <a href="https://strongthium.com/self-hosted-mail-server">Mail</a> · <a href="https://strongthium.com/self-hosted-live-chat">Chat</a> · <a href="https://strongthium.com/windows-ssh-client">Remote</a> · <a href="https://strongthium.com/twilio-sms-desktop-app">SMS</a> · <a href="https://strongthium.com/time-tracking-invoicing-software">Work</a> · <a href="https://strongthium.com/windows-registry-editor">Registry</a> · <a href="https://strongthium.com/cfml-server">DotCF</a> · <a href="https://strongthium.com/video-conferencing-dial-up-modem">Convey</a><br>
<a href="https://strongthium.com">Strongthium Labs</a> · Take back control of your internet.
</sub></p>
