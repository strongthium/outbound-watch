<p align="center">
  <a href="https://strongthium.com" title="Strongthium Labs"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/banner-2200x440.png" alt="Strongthium Labs: Take back control of your internet."></a>
</p>

[outbound-watch](../README.md) › [Manual](README.md) › Messages

# Messages

What `outbound-watch` says when something goes wrong or it needs an answer, where it says it, and what to do.

## Watching

At the bottom left, after *Watching TCP only, without bytes:*, and on [Settings](settings.md) under **Watching**:

| | |
|---|---|
| *Not running as administrator.* | the usual case without administrator rights. **Restart as administrator** to see bytes and UDP: see [Administrator or not](administrator.md) |
| *The network events need administrator rights.* | Windows refused to start the trace session |
| *The network events couldn't be started (error N).* | Windows refused for another reason, given by its error number. TCP is still watched from the table |
| *The network events couldn't be opened (error N).* | the session started but couldn't be read. TCP is still watched |
| *Reading the network events needs the 64-bit program.* | only the x64 program reads them |

A trace session left behind by a copy that didn't quit cleanly is stopped and started again, so that isn't an error.

## Countries and networks

| | Where |
|---|---|
| *The address databases couldn't be downloaded:* and why | a message box, after **Download** on [Live](live.md) |
| *Couldn't download them:* and why | beside **Download** or **Update now**, in Settings |
| *DB-IP has no files for this month or the last one.* | DB-IP's files weren't found. Try again later |
| *The downloaded files hold no address ranges.* | the files came down, but held nothing that could be read as DB-IP's databases |

When the monthly update fails, nothing is shown: the files it has go on being used, and it tries again the next time it
starts.

## Expected lists

| | |
|---|---|
| *Line N: …* | in red under the rules, a line that can't be read: the reasons are in [Expected](expected.md) |
| *N of its lines couldn't be read: they're marked under the rules.* | after **Import…**: the list was added, with those lines in red |
| *Save the changes to "…"?* | leaving a list with changes not saved: **Yes**, **No** or **Cancel** |
| *Delete the list "…"?* | the bin: **OK** deletes it |
| *Choose at least one thing for it to match.*, *This rule wouldn't match the connection.* | in [Expect this](expect-this.md) |

## History

| | |
|---|---|
| *Delete every connection in the history? The expected lists and settings stay.* | **Clear the history…**: **OK** deletes it, and it can't be undone |

## Starting at sign-in

A message box, after ticking or clearing **Start outbound-watch in the tray when I sign in**:

| | |
|---|---|
| *The sign-in task couldn't be made.* | the Task Scheduler refused the task |
| *The sign-in task couldn't be deleted.* | the Task Scheduler refused to delete it |
| *The program's path is unknown.* | Windows didn't say where `outbound-watch.exe` is |

Or Windows' own reason, when the `Run` key couldn't be changed.

## Updating

On [About](about.md), under *Couldn't ask GitHub for the latest version* or *The update wasn't installed*:

| | |
|---|---|
| *GitHub has had too many questions from this address for now. Try again in an hour.* | GitHub limits how often an address may ask |
| *GitHub took too long to answer.* | try **Check again** later |
| *GitHub's answer couldn't be read.* | the answer wasn't what was expected |
| *GitHub's latest release is called "…", which isn't a version.* | the latest release isn't named like `v1.0.0.1` |
| *The release has no outbound-watch-…-win-x64-needs-dotnet.zip.*, *The release has no SHA256SUMS to check it against.*, *SHA256SUMS doesn't list ….*, *The download doesn't hold outbound-watch.exe.* | the release is missing a file the update needs. Download it by hand with **What's new** |
| *The download of … doesn't match the release's SHA256SUMS, so nothing was changed.* | the download was damaged on the way. Try again |
| *This program can't be replaced from here: … needs an administrator to change it.* | the program's folder can't be changed with the rights it runs with. Download it with **What's new** |

After a failed update, *You can download it from the release's page instead: What's new.* is added, and nothing has
been changed.

## Anything else

An error the program doesn't expect is shown in a message box titled *outbound-watch*, with Windows' or .NET's own
words, and the program goes on running.

← [Your data](your-data.md) · [Contents](README.md)

<!-- strongthium-links -->

---

<p align="center"><sub>
<b>Strongthium open-source tools:</b> <a href="https://github.com/strongthium/emudhra-security-patch">eMudhra Security Patch</a> · <a href="https://github.com/strongthium/folder-size">Folder Size</a> · <a href="https://github.com/strongthium/health-reminder">Health Reminder</a> · <a href="https://github.com/strongthium/host-dossier">Host Dossier</a> · <b>Outbound Watch</b> · <a href="https://github.com/strongthium/raw-convert">RAW Convert</a> · <a href="https://github.com/strongthium/video-dispatch">Video Dispatch</a> · <a href="https://github.com/strongthium/webdav-server">WebDAV Server</a><br>
<b>More from Strongthium:</b> <a href="https://strongthium.com/linux-server-firewall">Guard</a> · <a href="https://strongthium.com/self-hosted-mail-server">Mail</a> · <a href="https://strongthium.com/self-hosted-live-chat">Chat</a> · <a href="https://strongthium.com/windows-ssh-client">Remote</a> · <a href="https://strongthium.com/twilio-sms-desktop-app">SMS</a> · <a href="https://strongthium.com/time-tracking-invoicing-software">Work</a> · <a href="https://strongthium.com/windows-registry-editor">Registry</a> · <a href="https://strongthium.com/cfml-server">DotCF</a> · <a href="https://strongthium.com/video-conferencing-dial-up-modem">Convey</a><br>
<a href="https://strongthium.com">Strongthium Labs</a> · Take back control of your internet.
</sub></p>
