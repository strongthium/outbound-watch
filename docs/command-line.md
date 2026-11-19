<p align="center">
  <a href="https://strongthium.com" title="Strongthium Labs"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/banner-2200x440.png" alt="Strongthium Labs: Take back control of your internet."></a>
</p>

[outbound-watch](../README.md) › [Manual](README.md) › The command line

# The command line

`outbound-watch` is a window and a tray icon, but a few switches choose how it starts.

```
outbound-watch --minimized             # starts in the tray, as it does at sign-in
outbound-watch --data D:\watch         # keeps the history and databases there instead
outbound-watch --sample                # a made-up PC: nothing watched, nothing saved
outbound-watch --theme dark            # light or dark, rather than following Windows
outbound-watch --sample --view totals  # opens on live, history, totals, expected or settings
outbound-watch --sample --view totals --by country  # the totals by program, network, country, domain, address or port
```

| | |
|---|---|
| `--minimized` | starts without showing the window: only the tray icon. Starting at sign-in uses it. Started this way while a copy already runs, it doesn't show that copy's window either |
| `--data <folder>` | keeps the history, the expected lists, the settings and the address databases in that folder, instead of `%LocalAppData%\Strongthium\outbound-watch`. A copy with its own folder runs beside the usual one, and keeps it when it restarts as administrator or updates |
| `--sample` | shows a made-up PC: a week of history, a few connections open now, and three expected lists. Nothing on this PC is watched, nothing is saved, DB-IP isn't downloaded and GitHub isn't asked. It runs beside a copy that's watching |
| `--theme light`, `--theme dark` | the look, rather than following Windows' own light or dark mode |
| `--view live`, `history`, `totals`, `expected`, `settings` | the page it opens on, rather than Live |
| `--by program`, `network`, `country`, `domain`, `address`, `port` | what [Totals](totals.md) is added up by when it's first shown, rather than programs |

Other words on the command line are ignored. The program also passes itself `--wait-for <process id>` when it restarts
as administrator or after an update, to wait for the copy it replaces to quit; it isn't meant to be typed.

← [Countries and networks](countries-and-networks.md) · [Contents](README.md) · [Your data](your-data.md) →

<!-- strongthium-links -->

---

<p align="center"><sub>
<b>Strongthium open-source tools:</b> <a href="https://github.com/strongthium/emudhra-security-patch">eMudhra Security Patch</a> · <a href="https://github.com/strongthium/folder-size">Folder Size</a> · <a href="https://github.com/strongthium/health-reminder">Health Reminder</a> · <a href="https://github.com/strongthium/host-dossier">Host Dossier</a> · <b>Outbound Watch</b> · <a href="https://github.com/strongthium/raw-convert">RAW Convert</a> · <a href="https://github.com/strongthium/video-dispatch">Video Dispatch</a> · <a href="https://github.com/strongthium/webdav-server">WebDAV Server</a><br>
<b>More from Strongthium:</b> <a href="https://strongthium.com/linux-server-firewall">Guard</a> · <a href="https://strongthium.com/self-hosted-mail-server">Mail</a> · <a href="https://strongthium.com/self-hosted-live-chat">Chat</a> · <a href="https://strongthium.com/windows-ssh-client">Remote</a> · <a href="https://strongthium.com/twilio-sms-desktop-app">SMS</a> · <a href="https://strongthium.com/time-tracking-invoicing-software">Work</a> · <a href="https://strongthium.com/windows-registry-editor">Registry</a> · <a href="https://strongthium.com/cfml-server">DotCF</a> · <a href="https://strongthium.com/video-conferencing-dial-up-modem">Convey</a><br>
<a href="https://strongthium.com">Strongthium Labs</a> · Take back control of your internet.
</sub></p>
