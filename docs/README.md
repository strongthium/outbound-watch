<p align="center">
  <a href="https://strongthium.com" title="Strongthium Labs"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/banner-2200x440.png" alt="Strongthium Labs: Take back control of your internet."></a>
</p>

[outbound-watch](../README.md) › Manual

# The outbound-watch manual

Every page of `outbound-watch`, with what each part of it does and how to use it, and where it keeps what it saves.

## The short version

```
1. Download outbound-watch.exe from the latest release, and run it (it needs the .NET 10 Desktop Runtime).
2. Click Restart as administrator (bottom left), so that bytes and UDP are watched too.
3. Click Download on Live, for each address's network and country.
4. On Expected, turn on a list, or right-click a connection on Live and choose Expect this….
5. Close the window: it keeps watching in the tray, and tells you when something connects somewhere unexpected.
```

## Getting started

- [Installing and starting](getting-started.md): what it needs, the first start, one copy at a time, starting at
  sign-in, and the sample.

## The window

- [Live](live.md): the connections open now, with the totals above them, read again every second.
- [History](history.md): every connection open at any time in the last hour up to all that's kept.
- [Totals](totals.md): the history added up by program, network, country, domain, address or port, with a chart of the
  bytes.
- [Right-click a connection](connection-menu.md): the menus on Live, History and Totals, and what a double-click does.
- [Expect this](expect-this.md): the window that makes a rule from a connection and adds it to a list.
- [Expected](expected.md): the expected lists, and how a rule is written.
- [Settings](settings.md): what's watched, the tray's warnings, the address databases, the history kept and starting at
  sign-in.
- [About](about.md): the version, the update check and updating, and what else Strongthium makes.
- [The tray icon](tray.md): its menu, its tooltip and the warnings it shows.

## Reference

- [Administrator or not](administrator.md): what it can watch with and without administrator rights.
- [Countries and networks](countries-and-networks.md): DB-IP's address databases, downloading and updating them, and
  the flags.
- [The command line](command-line.md): `--minimized`, `--data`, `--sample`, `--theme`, `--view` and `--by`.
- [Your data](your-data.md): where everything is kept, what's sent over the network, and how to remove it all.
- [Messages](messages.md): the messages it can show when something goes wrong, and what to do.

## Strongthium open-source tools

The other free, MIT-licensed tools from Strongthium Labs, each built by GitHub Actions from its source at [github.com/strongthium](https://github.com/strongthium):

<table>
  <tr>
    <td width="64" align="center" valign="top">
      <a href="https://github.com/strongthium/emudhra-security-patch"><picture><source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/dark/certificate.svg"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/light/certificate.svg" width="48" height="48" alt="Strongthium eMudhra Security Patch icon"></picture></a>
    </td>
    <td valign="top">
      <a href="https://github.com/strongthium/emudhra-security-patch"><b>Strongthium eMudhra Security Patch</b></a><br>
      <sub><b>Certificate trust-store patch for Windows and Linux.</b> Finds eMudhra's G3B and G3C intermediate CAs, and the roots above them, and distrusts them.</sub>
    </td>
    <td width="64" align="center" valign="top">
      <a href="https://github.com/strongthium/folder-size"><picture><source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/dark/treemap.svg"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/light/treemap.svg" width="48" height="48" alt="Strongthium Folder Size icon"></picture></a>
    </td>
    <td valign="top">
      <a href="https://github.com/strongthium/folder-size"><b>Strongthium Folder Size</b></a><br>
      <sub><b>Fast disk space analyzer for Windows.</b> Every folder added up, largest first, as a tree, a treemap, the largest files and the space by type.</sub>
    </td>
  </tr>
  <tr>
    <td width="64" align="center" valign="top">
      <a href="https://github.com/strongthium/health-reminder"><picture><source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/dark/pill.svg"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/light/pill.svg" width="48" height="48" alt="Strongthium Health Reminder icon"></picture></a>
    </td>
    <td valign="top">
      <a href="https://github.com/strongthium/health-reminder"><b>Strongthium Health Reminder</b></a><br>
      <sub><b>Medication reminder, pill identifier and meal planner for Windows.</b> Insistent reminders of pills, meals and water, with a picture of the very pill to take.</sub>
    </td>
    <td width="64" align="center" valign="top">
      <a href="https://github.com/strongthium/host-dossier"><picture><source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/dark/dossier.svg"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/light/dossier.svg" width="48" height="48" alt="Strongthium Host Dossier icon"></picture></a>
    </td>
    <td valign="top">
      <a href="https://github.com/strongthium/host-dossier"><b>Strongthium Host Dossier</b></a><br>
      <sub><b>Domain and IP lookup tool for Windows and Linux.</b> WHOIS, DNS, TLS, open ports and every mail record of a domain or an address, in one report.</sub>
    </td>
  </tr>
  <tr>
    <td width="64" align="center" valign="top">
      <a href="https://github.com/strongthium/raw-convert"><picture><source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/dark/camera.svg"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/light/camera.svg" width="48" height="48" alt="Strongthium RAW Convert icon"></picture></a>
    </td>
    <td valign="top">
      <a href="https://github.com/strongthium/raw-convert"><b>Strongthium RAW Convert</b></a><br>
      <sub><b>Batch CR2 to PNG and TIFF converter for Windows.</b> Folders of Canon RAW photos made lossless PNG or TIFF at full size, with the date, camera and GPS kept.</sub>
    </td>
    <td width="64" align="center" valign="top">
      <a href="https://github.com/strongthium/video-dispatch"><picture><source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/dark/video.svg"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/light/video.svg" width="48" height="48" alt="Strongthium Video Dispatch icon"></picture></a>
    </td>
    <td valign="top">
      <a href="https://github.com/strongthium/video-dispatch"><b>Strongthium Video Dispatch</b></a><br>
      <sub><b>TikTok and YouTube uploader and video editor for Windows.</b> One video queued to TikTok, YouTube and Shorts, cut and trimmed with Windows' own tools.</sub>
    </td>
  </tr>
  <tr>
    <td width="64" align="center" valign="top">
      <a href="https://github.com/strongthium/webdav-server"><picture><source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/dark/webdav.svg"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/light/webdav.svg" width="48" height="48" alt="Strongthium WebDAV Server icon"></picture></a>
    </td>
    <td valign="top">
      <a href="https://github.com/strongthium/webdav-server"><b>Strongthium WebDAV Server</b></a><br>
      <sub><b>Self-hosted WebDAV server for Linux and Windows.</b> Files by user or by path, Nextcloud-compatible, with bans and IP, ASN and country whitelists.</sub>
    </td>
    <td></td>
    <td></td>
  </tr>
</table>

## More from Strongthium

Strongthium Labs also makes software for running your own servers and Windows PCs, for talking to your customers, and for billing your hours, at [strongthium.com](https://strongthium.com):

<table>
  <tr>
    <td width="64" align="center" valign="top">
      <a href="https://strongthium.com/linux-server-firewall"><picture><source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/dark/guard.svg"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/light/guard.svg" width="48" height="48" alt="Strongthium Guard icon"></picture></a>
    </td>
    <td valign="top">
      <a href="https://strongthium.com/linux-server-firewall"><b>Strongthium Guard</b></a><br>
      <sub><b>Linux server firewall.</b> Locks down your Linux servers, reads their logs and shuts out whoever attacks them, within seconds.</sub>
    </td>
    <td width="64" align="center" valign="top">
      <a href="https://strongthium.com/self-hosted-mail-server"><picture><source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/dark/mail.svg"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/light/mail.svg" width="48" height="48" alt="Strongthium Mail icon"></picture></a>
    </td>
    <td valign="top">
      <a href="https://strongthium.com/self-hosted-mail-server"><b>Strongthium Mail</b></a><br>
      <sub><b>Self-hosted mail server for Linux and Windows.</b> Receives, checks and sends your mail, and tells you exactly why it refused what it refused.</sub>
    </td>
  </tr>
  <tr>
    <td width="64" align="center" valign="top">
      <a href="https://strongthium.com/self-hosted-live-chat"><picture><source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/dark/chat.svg"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/light/chat.svg" width="48" height="48" alt="Strongthium Chat icon"></picture></a>
    </td>
    <td valign="top">
      <a href="https://strongthium.com/self-hosted-live-chat"><b>Strongthium Chat</b></a><br>
      <sub><b>Self-hosted live chat for Linux and Windows.</b> A chat on your websites and text messages in one inbox, answered from Windows or a browser.</sub>
    </td>
    <td width="64" align="center" valign="top">
      <a href="https://strongthium.com/windows-ssh-client"><picture><source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/dark/remote.svg"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/light/remote.svg" width="48" height="48" alt="Strongthium Remote icon"></picture></a>
    </td>
    <td valign="top">
      <a href="https://strongthium.com/windows-ssh-client"><b>Strongthium Remote</b></a><br>
      <sub><b>Windows SSH client.</b> SSH terminal, file manager, editor, server task manager, database client and remote desktop in tabs, in one program.</sub>
    </td>
  </tr>
  <tr>
    <td width="64" align="center" valign="top">
      <a href="https://strongthium.com/twilio-sms-desktop-app"><picture><source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/dark/sms.svg"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/light/sms.svg" width="48" height="48" alt="Strongthium SMS icon"></picture></a>
    </td>
    <td valign="top">
      <a href="https://strongthium.com/twilio-sms-desktop-app"><b>Strongthium SMS</b></a><br>
      <sub><b>Twilio text messages for Windows.</b> Sends and receives your own Twilio account's texts from Windows, and blocks senders by number or by country.</sub>
    </td>
    <td width="64" align="center" valign="top">
      <a href="https://strongthium.com/time-tracking-invoicing-software"><picture><source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/dark/work.svg"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/light/work.svg" width="48" height="48" alt="Strongthium Work icon"></picture></a>
    </td>
    <td valign="top">
      <a href="https://strongthium.com/time-tracking-invoicing-software"><b>Strongthium Work</b></a><br>
      <sub><b>Time tracking and invoicing for Windows.</b> Logs the hours you work for each client, and turns them into invoices e-mailed as PDF, alone or with a team.</sub>
    </td>
  </tr>
  <tr>
    <td width="64" align="center" valign="top">
      <a href="https://strongthium.com/windows-registry-editor"><picture><source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/dark/registry.svg"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/light/registry.svg" width="48" height="48" alt="Strongthium Registry icon"></picture></a>
    </td>
    <td valign="top">
      <a href="https://strongthium.com/windows-registry-editor"><b>Strongthium Registry</b></a><br>
      <sub><b>Windows registry editor.</b> Shows what each key and value means, finds any key in seconds, and backs up every change before it's made.</sub>
    </td>
    <td width="64" align="center" valign="top">
      <a href="https://strongthium.com/cfml-server"><picture><source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/dark/dotcf.svg"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/light/dotcf.svg" width="48" height="48" alt="Strongthium DotCF icon"></picture></a>
    </td>
    <td valign="top">
      <a href="https://strongthium.com/cfml-server"><b>Strongthium DotCF</b></a><br>
      <sub><b>CFML server for Windows and Linux.</b> Runs applications written for Adobe ColdFusion and Lucee on .NET, web server and all, in one program.</sub>
    </td>
  </tr>
  <tr>
    <td width="64" align="center" valign="top">
      <a href="https://strongthium.com/video-conferencing-dial-up-modem"><picture><source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/dark/convey.svg"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/icons/light/convey.svg" width="48" height="48" alt="Strongthium Convey icon"></picture></a>
    </td>
    <td valign="top">
      <a href="https://strongthium.com/video-conferencing-dial-up-modem"><b>Strongthium Convey</b></a><br>
      <sub><b>Video conferencing for Windows.</b> Video, sound and text chat between Windows PCs, over the Internet, a dial-up modem or a cable.</sub>
    </td>
    <td></td>
    <td></td>
  </tr>
</table>

<!-- strongthium-links -->

---

<p align="center"><sub>
<b>Strongthium open-source tools:</b> <a href="https://github.com/strongthium/emudhra-security-patch">eMudhra Security Patch</a> · <a href="https://github.com/strongthium/folder-size">Folder Size</a> · <a href="https://github.com/strongthium/health-reminder">Health Reminder</a> · <a href="https://github.com/strongthium/host-dossier">Host Dossier</a> · <b>Outbound Watch</b> · <a href="https://github.com/strongthium/raw-convert">RAW Convert</a> · <a href="https://github.com/strongthium/video-dispatch">Video Dispatch</a> · <a href="https://github.com/strongthium/webdav-server">WebDAV Server</a><br>
<b>More from Strongthium:</b> <a href="https://strongthium.com/linux-server-firewall">Guard</a> · <a href="https://strongthium.com/self-hosted-mail-server">Mail</a> · <a href="https://strongthium.com/self-hosted-live-chat">Chat</a> · <a href="https://strongthium.com/windows-ssh-client">Remote</a> · <a href="https://strongthium.com/twilio-sms-desktop-app">SMS</a> · <a href="https://strongthium.com/time-tracking-invoicing-software">Work</a> · <a href="https://strongthium.com/windows-registry-editor">Registry</a> · <a href="https://strongthium.com/cfml-server">DotCF</a> · <a href="https://strongthium.com/video-conferencing-dial-up-modem">Convey</a><br>
<a href="https://strongthium.com">Strongthium Labs</a> · Take back control of your internet.
</sub></p>
