<p align="center">
  <a href="https://strongthium.com" title="Strongthium Labs"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/banner-2200x440.png" alt="Strongthium Labs: Take back control of your internet."></a>
</p>

[outbound-watch](../README.md) › [Manual](README.md) › The tray icon

# The tray icon

`outbound-watch` watches from the tray, in the notification area at the end of the taskbar, whether its window is open
or not. Closing the window leaves it there; only **Quit** stops the watching.

## The icon

| | |
|---|---|
| **Click** | opens the window |
| **Hover** | how many connections are open, and how many of them are unexpected: *outbound-watch: 11 open, 2 unexpected*, read again every five seconds |
| **Right-click** | the menu below |

## The menu

| | |
|---|---|
| **Open outbound-watch** | opens the window |
| **Unexpected connections** | opens the window on [Live](live.md), with **Unexpected only** ticked and the search cleared |
| **Quit (stop watching)** | saves what's open as ended now, and quits. Nothing is watched until it's started again |

## Its warnings

| | |
|---|---|
| **Not on an expected list** | a program connected somewhere that isn't on any expected list that's on: `larkspur-update.exe → telemetry.example.net (Example Hosting Pte. Ltd., Singapore)`, and *and 2 more* when there were others at the same time. Click it for the unexpected connections on Live |
| **Still watching** | the first time the window is closed: *outbound-watch keeps watching in the tray. Quit is in its icon's menu.* It's said once |

A warning waits three seconds after a connection opens, so that its name is known. Each program and destination (its
domain, or its address when it has no name) is told about once in six hours while it runs. Nothing is told while no list is on, or
when **Tell me in the tray when a program connects somewhere that isn't on an expected list** is off in
[Settings](settings.md).

← [About](about.md) · [Contents](README.md) · [Administrator or not](administrator.md) →

<!-- strongthium-links -->

---

<p align="center"><sub>
<b>Strongthium open-source tools:</b> <a href="https://github.com/strongthium/emudhra-security-patch">eMudhra Security Patch</a> · <a href="https://github.com/strongthium/folder-size">Folder Size</a> · <a href="https://github.com/strongthium/health-reminder">Health Reminder</a> · <a href="https://github.com/strongthium/host-dossier">Host Dossier</a> · <b>Outbound Watch</b> · <a href="https://github.com/strongthium/raw-convert">RAW Convert</a> · <a href="https://github.com/strongthium/video-dispatch">Video Dispatch</a> · <a href="https://github.com/strongthium/webdav-server">WebDAV Server</a><br>
<b>More from Strongthium:</b> <a href="https://strongthium.com/linux-server-firewall">Guard</a> · <a href="https://strongthium.com/self-hosted-mail-server">Mail</a> · <a href="https://strongthium.com/self-hosted-live-chat">Chat</a> · <a href="https://strongthium.com/windows-ssh-client">Remote</a> · <a href="https://strongthium.com/twilio-sms-desktop-app">SMS</a> · <a href="https://strongthium.com/time-tracking-invoicing-software">Work</a> · <a href="https://strongthium.com/windows-registry-editor">Registry</a> · <a href="https://strongthium.com/cfml-server">DotCF</a> · <a href="https://strongthium.com/video-conferencing-dial-up-modem">Convey</a><br>
<a href="https://strongthium.com">Strongthium Labs</a> · Take back control of your internet.
</sub></p>
