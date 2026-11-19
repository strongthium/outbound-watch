<p align="center">
  <a href="https://strongthium.com" title="Strongthium Labs"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/banner-2200x440.png" alt="Strongthium Labs: Take back control of your internet."></a>
</p>

[outbound-watch](../README.md) › [Manual](README.md) › Expect this

# Expect this

The window that makes a rule from a connection and adds it to an expected list. It opens from **Expect this…** in the
right-click menu of a connection on [Live](live.md) or [History](history.md), or of a line of [Totals](totals.md).

## At the top

The program and where it went, `larkspur-update.exe → telemetry.example.net`, and under them the address and port, the
network and the country: `203.0.113.36:443 · Example Hosting Pte. Ltd., Singapore`.

## Where it goes

How much of where it went to expect, one of:

| | Writes |
|---|---|
| **The name telemetry.example.net** | `host telemetry.example.net` |
| **Any name in example.net** | `host *.example.net`: the domain and every name under it |
| **The address 203.0.113.36** | `ip 203.0.113.36` |
| **The network Example Hosting Pte. Ltd. (AS64504)** | `asn 64504` |
| **Anywhere in Singapore** (with its flag) | `country SG` |
| **Anywhere** | nothing: the rule is then only about the program or the port |

Only the choices the connection has are shown. The two name choices are there only when the PC looked the name up
itself, never for a name from a reverse lookup (see [Expected](expected.md) for why), and **Any name in…** only when
the name isn't the domain itself. It starts on **Any name in…** when there is one, or else on the first.

## Who and how

| | |
|---|---|
| **Only from larkspur-update.exe** | adds `process larkspur-update.exe`. For a Windows service in `svchost.exe`, it's **Only from the service wuauserv**, and adds `service wuauserv` |
| **Only to TCP 443 (HTTPS)** | adds `port 443 proto tcp` |

**Only from…** starts on and **Only to…** off, except from a line of **Ports** on Totals, where it's the other way round.

## The rule

The rule as it will be written, from the choices above. It can be changed by hand before it's added; once it has been,
the choices no longer change it. Under it, in red, what's wrong with it:

| | |
|---|---|
| *Choose at least one thing for it to match.* | **Anywhere**, with neither box ticked: the rule would be empty |
| *This rule wouldn't match the connection.* | the rule, as changed by hand, doesn't cover this connection. It can still be added |
| a line's error | the words don't read: see the errors in [Expected](expected.md) |

## On the list

The list to add it to: one of the lists, or **A new list…**, which shows a box for the new list's name (*My expected*
to start with).

**Add the rule** puts the rule at the end of the list, turns the list on if it was off, and saves it. The connections
open now are judged again at once, and History and Totals judge by the new rule too. **Cancel** (or Esc) adds nothing.

← [Right-click a connection](connection-menu.md) · [Contents](README.md) · [Expected](expected.md) →

<!-- strongthium-links -->

---

<p align="center"><sub>
<b>Strongthium open-source tools:</b> <a href="https://github.com/strongthium/emudhra-security-patch">eMudhra Security Patch</a> · <a href="https://github.com/strongthium/folder-size">Folder Size</a> · <a href="https://github.com/strongthium/health-reminder">Health Reminder</a> · <a href="https://github.com/strongthium/host-dossier">Host Dossier</a> · <b>Outbound Watch</b> · <a href="https://github.com/strongthium/raw-convert">RAW Convert</a> · <a href="https://github.com/strongthium/video-dispatch">Video Dispatch</a> · <a href="https://github.com/strongthium/webdav-server">WebDAV Server</a><br>
<b>More from Strongthium:</b> <a href="https://strongthium.com/linux-server-firewall">Guard</a> · <a href="https://strongthium.com/self-hosted-mail-server">Mail</a> · <a href="https://strongthium.com/self-hosted-live-chat">Chat</a> · <a href="https://strongthium.com/windows-ssh-client">Remote</a> · <a href="https://strongthium.com/twilio-sms-desktop-app">SMS</a> · <a href="https://strongthium.com/time-tracking-invoicing-software">Work</a> · <a href="https://strongthium.com/windows-registry-editor">Registry</a> · <a href="https://strongthium.com/cfml-server">DotCF</a> · <a href="https://strongthium.com/video-conferencing-dial-up-modem">Convey</a><br>
<a href="https://strongthium.com">Strongthium Labs</a> · Take back control of your internet.
</sub></p>
