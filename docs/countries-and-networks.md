<p align="center">
  <a href="https://strongthium.com" title="Strongthium Labs"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/banner-2200x440.png" alt="Strongthium Labs: Take back control of your internet."></a>
</p>

[outbound-watch](../README.md) › [Manual](README.md) › Countries and networks

# Countries and networks

An address's country and network come from [DB-IP](https://db-ip.com)'s free databases, *IP to Country Lite* and
*IP to ASN Lite*, under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). Once they're downloaded, every
lookup is done in memory, on this PC: no address is ever sent anywhere to be looked up.

## What they give

| | |
|---|---|
| **Country** | the country the address is used in, shown with its flag and its name as Windows names it. For a content network that's often near you, rather than where the company is |
| **Network** | the autonomous system that announces the address: its AS number (`AS64500`) and its organization's name |

Without them, **Network** shows **—** and **Country** is empty, and `asn`, `org` and `country` rules match nothing.
Connections inside the local network are **Local network** whether or not they're downloaded.

## Downloading them

**Download** on [Live](live.md) (a card at the top until they're downloaded), or in [Settings](settings.md), under
**Countries and networks**. The two files, about 12 MB together, come from `download.db-ip.com`:

| | |
|---|---|
| `dbip-country-lite-<month>.csv.gz` | the countries |
| `dbip-asn-lite-<month>.csv.gz` | the networks |

They're this month's, or last month's early in a month before the new ones are out. They're checked by reading them
before they're used, and the older months' files are then deleted. The download sends nothing but the request for the
files, with the program's name and version.

## Keeping them up to date

DB-IP publishes new files each month. With **Download each month's new ones** on (it is, until it's turned off), the
program downloads them when it starts from the 3rd of a month on, if the files it has are of an earlier month.
**Update now** in Settings downloads them at once. Connections already in the history keep the network and country
they had when they were seen.

## The flags

Each country's flag is a picture inside the program, from [Flagpedia](https://flagpedia.net), whose flags are in the
public domain; nothing is downloaded to show them. A country with no flag is shown by name alone.

← [Administrator or not](administrator.md) · [Contents](README.md) · [The command line](command-line.md) →

<!-- strongthium-links -->

---

<p align="center"><sub>
<b>Strongthium open-source tools:</b> <a href="https://github.com/strongthium/emudhra-security-patch">eMudhra Security Patch</a> · <a href="https://github.com/strongthium/folder-size">Folder Size</a> · <a href="https://github.com/strongthium/health-reminder">Health Reminder</a> · <a href="https://github.com/strongthium/host-dossier">Host Dossier</a> · <b>Outbound Watch</b> · <a href="https://github.com/strongthium/raw-convert">RAW Convert</a> · <a href="https://github.com/strongthium/video-dispatch">Video Dispatch</a> · <a href="https://github.com/strongthium/webdav-server">WebDAV Server</a><br>
<b>More from Strongthium:</b> <a href="https://strongthium.com/linux-server-firewall">Guard</a> · <a href="https://strongthium.com/self-hosted-mail-server">Mail</a> · <a href="https://strongthium.com/self-hosted-live-chat">Chat</a> · <a href="https://strongthium.com/windows-ssh-client">Remote</a> · <a href="https://strongthium.com/twilio-sms-desktop-app">SMS</a> · <a href="https://strongthium.com/time-tracking-invoicing-software">Work</a> · <a href="https://strongthium.com/windows-registry-editor">Registry</a> · <a href="https://strongthium.com/cfml-server">DotCF</a> · <a href="https://strongthium.com/video-conferencing-dial-up-modem">Convey</a><br>
<a href="https://strongthium.com">Strongthium Labs</a> · Take back control of your internet.
</sub></p>
