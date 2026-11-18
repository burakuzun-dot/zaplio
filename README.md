<p align="right"><b>English</b> · <a href="README.tr.md">Türkçe</a></p>

<p align="center">
  <a href="https://github.com/burakuzun-dot/zaplio/releases/latest">
    <img src="docs/images/zaplio-banner.gif" alt="Zaplio" width="560">
  </a>
</p>

<p align="center">
  <b>A fast, simple, open-source media player for Windows.</b><br>
  Open your own M3U playlists and Xtream API sources, without duplicate streams, frozen channels or missed moments.
</p>

<p align="center">
  <a href="https://github.com/burakuzun-dot/zaplio/releases/latest"><img alt="Latest release" src="https://img.shields.io/github/v/release/burakuzun-dot/zaplio?label=release&color=7B61FF&style=flat-square"></a>
  <a href="https://github.com/burakuzun-dot/zaplio/actions/workflows/ci.yml"><img alt="CI" src="https://img.shields.io/github/actions/workflow/status/burakuzun-dot/zaplio/ci.yml?branch=main&label=CI&style=flat-square"></a>
  <a href="https://github.com/burakuzun-dot/zaplio/releases"><img alt="Downloads" src="https://img.shields.io/github/downloads/burakuzun-dot/zaplio/total?label=downloads&color=FFC83D&style=flat-square"></a>
  <img alt="Windows 10 and 11" src="https://img.shields.io/badge/Windows-10%20%7C%2011-0E1020?style=flat-square">
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4?style=flat-square">
  <a href="LICENSE"><img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-C4B8FF?style=flat-square"></a>
</p>

<p align="center">
  <a href="https://github.com/burakuzun-dot/zaplio/releases/latest"><b>⬇ Download</b></a> ·
  <a href="#-why-zaplio">Why Zaplio?</a> ·
  <a href="#-features">Features</a> ·
  <a href="#-faq">FAQ</a> ·
  <a href="CHANGELOG.md">Changelog (Turkish)</a>
</p>

> [!IMPORTANT]
> **Zaplio is only a player.** It does not provide, sell or recommend channels, playlists or subscriptions. You add your own M3U
> playlist or an Xtream API account you have the legal right to use, and you are responsible for what you add
> ([legal notice, in Turkish](docs/legal.md)). Only download Zaplio from this repository's [Releases](https://github.com/burakuzun-dot/zaplio/releases) page.

The interface is available in **English and Turkish** (Settings › Appearance › *Dil / Language*; by default it follows Windows).

## ⚡ Why Zaplio?

<table>
  <tr>
    <td width="50%" valign="top">
      <h3>🎚️ Smart quality, backup streams</h3>
      No more four rows for <i>Channel A HD / FHD / 4K / H.265</i>: they become <b>one row</b>. Zaplio picks the right quality for
      your connection, and if a stream will not open or drops, it <b>switches to another quality by itself</b> instead of showing an error.
    </td>
    <td width="50%" valign="top">
      <h3>⏸️ Pause and rewind live TV</h3>
      Phone ringing? Pause the live stream with <kbd>Space</kbd> and continue where you left off. On channels with an archive,
      <kbd>←</kbd> rewinds 30 seconds; <b>Back to live</b> is one click.
    </td>
  </tr>
  <tr>
    <td width="50%" valign="top">
      <h3>📺 Play on your TV</h3>
      Send what plays to a smart TV on your home network (DLNA). Zaplio relays the stream, so your
      <b>username and password never reach the TV</b> and the provider still sees a single connection.
    </td>
    <td width="50%" valign="top">
      <h3>👪 Made for the living room</h3>
      A PIN-protected <b>kids profile</b> that only shows the categories you allow, a <b>sleep timer</b>, large subtitles with a dark
      box, picture adjustment and volume levelling for loud ad breaks.
    </td>
  </tr>
  <tr>
    <td width="50%" valign="top">
      <h3>🚀 No waiting</h3>
      Channel, movie and guide lists are kept in an encrypted cache, so the app opens <b>without waiting for the server</b>. The VLC
      engine warms up in the background; the first channel usually starts in under half a second.
    </td>
    <td width="50%" valign="top">
      <h3>🖥️ Second screen and mini window</h3>
      Full screen on the other monitor while the list and guide stay on the main one (<kbd>Shift</kbd>+<kbd>F11</kbd>), or an
      always-on-top mini window (<kbd>P</kbd>). Recording keeps the original quality.
    </td>
  </tr>
</table>

## ⬇ Download

| Windows 10 / 11 · 64-bit | |
|---|---|
| **Installer** | `Zaplio-<version>-setup.exe`: no admin rights needed, adds a Start menu entry, updates itself from inside the app |
| **Portable** | `Zaplio-<version>-win-x64.zip`: extract to a folder and run `Zaplio.exe` |

Both packages include .NET and VLC; nothing else needs to be installed.

> [!NOTE]
> The packages are not code-signed yet, so Windows *Smart App Control* may block them. We have applied to the SignPath
> Foundation for free open-source signing; see the [code signing policy](docs/code-signing.md).

## 🏁 Get started in 3 steps

1. **Add a source**: **Settings** (⚙, top right) → **Sources** → *Add source*: an M3U file, an M3U link or Xtream (server, username, password).
2. **Pick a channel**: click it, move with <kbd>Ctrl</kbd>+<kbd>↑</kbd>/<kbd>↓</kbd> or type its number; <kbd>Backspace</kbd> goes back to the previous channel.
3. **Check the guide**: the **Guide** button shows programmes; double-click a past programme marked *ARCHIVE* to watch it.

All shortcuts are in the app under <kbd>F1</kbd>.

## 🧩 Features

<details>
<summary><b>Sources and live TV</b></summary>

- M3U / M3U8 (file or link; `tvg-*`, `group-title`, user-agent/referrer, `x-tvg-url`) and Xtream API
- Several sources; groups, search, favorites and ordering, channel logos
- Hide groups, rename channels, merge the qualities of one channel and pin one by hand
- Xtream subscription details: expiry date, connection count, a warning in the last week
- Channels that would not open are dimmed for a day; a full connection limit is explained instead of a bare error

</details>

<details>
<summary><b>Guide and archive</b></summary>

- XMLTV (`.xml`, `.xml.gz`), now / next summary, 7-day schedule, a full guide grid
- Xtream archive: watch past programmes; rewind live TV with <kbd>←</kbd>

</details>

<details>
<summary><b>Movies and series</b></summary>

- Posters, categories, search, details panel, watchlist and favorites
- Continue where you left off, watched marks, next episode

</details>

<details>
<summary><b>Playback</b></summary>

- VLC 3 engine: audio and subtitle tracks, aspect ratio, crop, audio/subtitle delay, playback speed
- Live pause (time shift), lossless recording, screenshots, local videos (drag and drop), cinema mode
- Full screen, mini window, second screen, play on TV (DLNA)
- Subtitle size and background box, brightness / contrast / colour, volume levelling, sleep timer
- Automatic reconnect, backup-quality fallback, connection status and live speed meter

</details>

<details>
<summary><b>Home and family</b></summary>

- PIN-protected kids profile (allow list of categories)
- Encrypted backup to move everything to another computer
- Light and dark themes, six accent colours, English and Turkish interface

</details>

## ❓ FAQ

<details>
<summary><b>It says "The provider refused access (403)"</b></summary>

Usually because a **VPN** (e.g. Cloudflare WARP) is on; many providers refuse VPN connections. Turn it off and try again. If it
continues, your connection limit may be reached, your IP address temporarily blocked, or your subscription expired (see the
subscription details under Settings › Sources).
</details>

<details>
<summary><b>Windows will not open the app / says it was blocked</b></summary>

With Windows *Smart App Control* on, unsigned packages may be blocked. See the note under Download.
</details>

<details>
<summary><b>The TV is not found or cannot play</b></summary>

The TV and the computer must be on the same home network, and Windows Firewall must allow Zaplio on *Private networks* (Windows
asks the first time). HLS (`.m3u8`) streams and videos on your computer cannot be sent to a TV yet.
</details>

<details>
<summary><b>Where is my data? Is it removed when I uninstall?</b></summary>

Your sources, favorites and cache are stored in `%LocalAppData%\Zaplio`, encrypted to your Windows account. Uninstalling does not
delete this folder; delete it by hand if you want everything gone.
</details>

## 🔒 Privacy

- Source details and the cache are encrypted with Windows DPAPI, tied to your Windows account.
- Usernames and passwords are masked in the error logs.
- **No usage data is collected.** Zaplio has no server of its own.
- Zaplio only talks to: the sources you add, the logo/poster servers named in them (can be turned off) and GitHub for update
  checks (can be turned off). Those servers can see your IP address.
- How releases are signed: [code signing policy](docs/code-signing.md).

## 🛠️ Development

```powershell
dotnet run --project src/Zaplio      # run the app
dotnet test                         # unit tests
```

The .NET 10 SDK is enough; VLC comes from NuGet. More: [docs/building.md](docs/building.md) (Turkish) ·
[CONTRIBUTING.md](CONTRIBUTING.md) · coding agents: [AGENTS.md](AGENTS.md).
Interface text is written in Turkish and translated in `src/Zaplio/Lang.English.cs`; a test fails if a translation is missing.

When reporting a bug, attaching that day's `.log` file (Settings → Open log folder) helps a lot.
[Report a bug](https://github.com/burakuzun-dot/zaplio/issues/new/choose) · Security issues: [SECURITY.md](SECURITY.md)

## 📄 License

Zaplio is released under the [MIT license](LICENSE). The open-source components it uses (VLC / LibVLCSharp, LGPL-2.1;
Inter, OFL-1.1; Fluent System Icons, MIT) and their license texts are in [Licenses](src/Zaplio/Licenses/ThirdPartyNotices.txt).

<p align="center"><sub>Zaplio is not a broadcaster and is not affiliated with any provider, broadcaster or brand.</sub></p>
