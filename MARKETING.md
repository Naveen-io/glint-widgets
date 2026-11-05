# Marketing

Where Desk Widgets stands today, what blocks growth, and a plan to get it in front of people.

Based on the live site and the repo as of PR #47 (hero lens carousel).

---

## Where it stands

### What's already strong

- **Performance is a real differentiator.** Seven widgets run in about 33 MB of RAM and 0.3% of one CPU core, from a 0.3 MB download. It runs on the .NET Framework that ships with Windows, so there is nothing else to install.
- **The security story is unusually deep for a hobby project.** These are things a technical audience will respect, and the HN post should lead with them:
  - Community widgets are plain C#, and a person reviews every version.
  - The app checks a hash on the user's PC before running a widget.
  - `check-hidden.ps1` blocks Trojan Source characters.
  - Row-level security is covered by 36 permission tests (`server/test/schema.test.mjs`).
- **One-click install already exists.** `deskwidgets://install/<id>/<version>` goes from the website straight into the app.
- **Per-widget pages already exist.** `widget.html?id=…` shows each widget's source and version history.
- **The Store path is mostly built.** `packaging/build-msix.ps1` and `AppxManifest.xml` produce an MSIX package, signed for now with a self-signed dev certificate.
- **The visuals exist.** The README has an overview GIF and the homepage has the hero carousel.
- **There are hooks for specific audiences.** F1 (Jolpica + OpenF1), the GitHub contribution graph, and games each speak to a different community.

### What's holding it back

| Area | Problem | Why it matters |
|---|---|---|
| Install friction | The exe is unsigned. The README tells users to click More info › Run anyway. | Most non-technical users stop at the SmartScreen warning. This is the largest drop-off. |
| Homepage says the wrong thing | The homepage says "Ten widgets, built in", but the README says only the three clocks are built in and everything else installs from Explore. | First-time users expect a calendar and weather right after installing and find clocks. Explore looks padded because the homepage contradicts it. |
| Explore "Get" | "Get" is a bare `deskwidgets://` link with no fallback. | Visitors without the app (most people arriving from a shared link) click and nothing happens. |
| Shared links all look the same | `widget.html?id=…` is one static page on GitHub Pages, so every widget link has the same title ("Widget · Desk Widgets") and the same preview image. | A link to the F1 widget posted in an F1 Discord doesn't show the F1 widget. |
| Community claim | Nearly every Explore widget is by `saif`, and one is by `codewithaiyan`. | "Made by the community" reads as overclaiming, and someone will point it out. |
| Gmail widget | The preview is blank and its source isn't in `community/`. If it uses the Gmail API, those scopes are restricted. | Users would see an "unverified app" screen, Google can revoke access, and it conflicts with "private by default." |
| F1 Live Timing | It signs in to OpenF1 with a username and password (`F1.cs`, OAuth password grant). | If live data needs a paid OpenF1 account, saying "live timing" without that caveat will disappoint the audience we most want. |
| Trademarks / IP | The names "F1 Timing", "F1 Hub" and "Gmail" use trademarks. The pixel game's art should be checked for copied sprites. | A takedown or a name-change request is a bad thing to get mid-launch. |
| Data correctness | The Crypto preview shows "24h volume ▼1.34%" (volume isn't a % change), and Crypto is tagged Productivity instead of Finance. | Finance users notice wrong data in seconds. |
| Polish | Preview sizes are inconsistent and some are too small to read. "System" and "System Monitor" overlap. "Photo-Frame" is hyphenated while other names aren't. | The grid looks unfinished in screenshots people will share. |
| No measurement | There's no analytics on the site. | We can't tell which channel works, so we can't double down on it. |
| Review capacity | One person reviews every widget version. | If marketing works, review becomes the bottleneck and authors leave. |

---

## Fix before any launch (P0)

These come before marketing because marketing only multiplies the conversion rate we already have.

- [ ] **Remove the SmartScreen warning.** Most of the MSIX work is done. What's left:
  - Register a Microsoft Partner Center account.
  - Replace `Publisher="CN=DeskWidgets"` in `AppxManifest.xml` with the Partner Center identity.
  - Submit to the Microsoft Store, which signs the package.
  - Publish to **winget** as well. Optionally, sign the standalone exe with Azure Trusted Signing.
- [ ] **Fix the homepage catalog line.** Change "Ten widgets, built in" to something true, such as "Three clocks built in. Calendar, weather, F1 and more, one click away in Explore."
- [ ] **Give "Get" a fallback.** If the protocol handler doesn't open within ~1.5 s, show "Don't have Desk Widgets yet?" with the download button. After installing, the app can open the pending `deskwidgets://install/…` link.
- [ ] **Decide what to do about the Gmail widget.** Its author is `codewithaiyan`, so this is our call. Confirm how it authenticates, or hide it until that's settled.
- [ ] **Check F1 Live Timing requirements.** If an OpenF1 account (or a paid tier) is needed for live data, say so on the widget page and in every F1 post.
- [ ] **Rename trademarked widgets** (for example "Race Timing", "Race Hub", "Mail") and check the game art.
- [ ] **Fix the Crypto preview data and its category.**
- [ ] **Make all preview images the same size,** and merge or tell apart "System" and "System Monitor".

## Should do before launch (P1)

- [ ] **Shareable widget pages.** Generate static pages at build time (for example `docs/w/<id>/index.html`), each with its own `<title>`, description and `og:image` set to that widget's preview. `widget.html?id=` can stay as the full page with source and history. The static page is what links unfurl to.
- [ ] **Get 3–5 outside authors** to publish one widget each, so the community claim is true on day one. Until then, use wording like "Made by us, open to everyone."
- [ ] **Add a short comparison section** to the homepage:

  | | Desk Widgets | Rainmeter | Windows 11 Widgets |
  |---|---|---|---|
  | Setup | Drag and drop | Skins + config files | Built in |
  | Where it lives | On the desktop | On the desktop | Separate panel |
  | Content | Your widgets | Your skins | Mostly MSN news feed |
  | Memory | ~33 MB for 7 widgets | Varies by skin | Runs on Edge WebView |
  | Community widgets reviewed | Yes | No | N/A |

  *Fill in the Rainmeter and Windows Widgets rows with measured numbers before publishing. Don't guess.*
- [ ] **Write a plan for scaling review:** a submission queue, an expected review time posted publicly, and possibly trusted co-reviewers later.
- [ ] **Add basic, privacy-respecting analytics** (for example GoatCounter, which has a free plan, or Plausible). Track downloads and "Get" clicks by referrer.

---

## Positioning

Pick **one** lead message. The current homepage reads as a feature list.

- **Option A: "Rainmeter without the config."** This reaches the most people (desktop-customization users) and leads on drag-and-drop simplicity plus low resource use.
- **Option B: "The developer's desktop."** Lead with the GitHub, System and Focus Timer widgets, the C# widget API (`samples/HelloWidget.cs`), and the security engineering. This brings fewer users but more contributors, and contributors make the community model work.

**Recommendation:** use A on the homepage and B in developer channels (HN, r/csharp, r/dotnet). One product, a different first sentence for each audience.

Proof points to repeat everywhere: **33 MB · 0.3 MB download · free & open source · every community widget reviewed by a person.**

---

## Per-widget marketing

Once widget pages have their own social preview images (P1), each community gets a link to the one widget it cares about:

| Widget | Audience | Where |
|---|---|---|
| Race Hub / Race Timing | F1 fans | r/formula1, F1 Discord servers, on race weekends |
| GitHub | Developers | r/github, dev Twitter/X, HN comments |
| Snake / Minesweeper | Nostalgia, casual | r/pcmasterrace, nostalgia threads |
| Now Playing | Music / setup people | r/desktops, r/spotify |
| Crypto / Watchlist | Finance | r/CryptoCurrency setup threads (once the data is fixed) |

Race Hub only uses Jolpica (schedule, standings), so it can be promoted with no account caveat. Lead with it in F1 spaces, and mention Race Timing second.

---

## Channels, in priority order

### 1. Distribution listings (one-time, keep paying off)
- [ ] Microsoft Store (see P0)
- [ ] winget, Scoop, Chocolatey
- [ ] AlternativeTo, listed as an alternative to Rainmeter and Windows Widgets
- [ ] awesome-windows and similar GitHub awesome-lists
- [ ] GitHub repo metadata: topics (`windows`, `widgets`, `desktop`, `rainmeter-alternative`, `csharp`), a description, the website link, and a social preview image. The README GIF is already there.

### 2. Hacker News: Show HN
- Angle: open source, 33 MB, human-reviewed C# plugins with hash verification on the user's PC, and a Trojan Source check. This is technical and true, which is what HN rewards.
- Post on a weekday morning US time, and have the author available to answer comments for the first few hours.

### 3. Reddit
- **r/desktops, r/Windows11, r/battlestations:** post a good-looking setup screenshot that uses the widgets, not an ad. Mention the app in a comment when someone asks.
- **r/formula1:** a Race Hub screenshot or GIF during a race weekend. Follow the subreddit's rules on self-promotion.
- **r/csharp, r/dotnet:** "how we built a widget platform in 33 MB on .NET Framework 4" plus an invitation to write widgets.
- Taking part in these communities beforehand helps a lot.

### 4. Product Hunt
- Launch on a Tuesday or Wednesday with the GIF, the comparison table and the shareable widget pages ready.

### 5. Creators and press
- Windows-customization YouTubers who make "best free apps" videos: send a short pitch with the GIF and the 33 MB number.
- Freeware news sites (Neowin, gHacks, etc.) cover new open-source Windows tools. Send a short release note.

### 6. Community flywheel
- Each new community widget is a reason to post again, and its author will share it too.
- Run a "widget of the week" or a small widget-making challenge once outside authors exist.

---

## Launch sequence

| Week | Focus |
|---|---|
| 1 | P0: homepage copy, Get fallback, Gmail and F1 decisions, renames, Crypto fix. Start Partner Center registration. |
| 2 | Store submission and winget. Analytics. Static widget pages for 2–3 widgets. |
| 3 | Outside authors, comparison section. Listings (AlternativeTo, awesome-lists, Scoop/Choco). |
| 4 | Show HN + r/csharp/r/dotnet. |
| 5 | Reddit setup posts. Race Hub post on a race weekend (2026 season finale or 2027 preseason testing). |
| 6 | Product Hunt. Creator and press outreach. |

Don't launch on every channel in the same week. Spread it out so we can fix what each wave of feedback finds.

---

## What to measure

- Downloads per week, by referring channel
- "Get" clicks, and how many come from visitors without the app (the fallback shown)
- Installs to first widget added (activation)
- Widgets added per active user
- Outside authors and submitted widgets per week
- Review turnaround time
- GitHub stars and forks (vanity, but they show momentum)

---

## Open questions

1. Does F1 Live Timing need a paid OpenF1 account for live sessions?
2. How does the Gmail widget authenticate, and is its source going into `community/`?
3. Is the owner willing to register a Partner Center account for the Store listing?
4. Is the owner willing to add co-reviewers if submissions grow?
5. Which lead message: "Rainmeter without the config" or "the developer's desktop"?
