// Sample rows for working on the pages before Supabase is connected. Loaded only by
// assets/data.js, only on a dev host without ?live=1 -- so it is never
// fetched by the published site.
//
// The shapes are exactly what the database returns:
//   widgets: the `explore` view's columns, minus `preview` (that view is queried separately)
//   profiles: the `authors` view (username, bio, avatar)
//   previews: id -> base64 PNG, which is how versions.preview stores them
//
// Previews reuse the site's own screenshots in assets/img, fetched and base64'd on demand, so no
// placeholder files are needed. Each image belongs to one widget only: a widget with no art of its
// own gets null, so the card falls back to its category icon.

const widget = (slug, name, author, category, description, art) => ({
  row: {
    id: "u." + author + "." + slug,     // isValidId() in site.js rejects anything else
    version: 1,
    name,
    category,
    description,
    api_version: 1,
    sha256: "0".repeat(64),
    reviewed_at: null,                  // filled in below, newest first
    author,
  },
  art,
});

const ITEMS = [
  widget("minesweeper", "Minesweeper", "saif", "games", "The classic, on your desktop.", null),            // no art yet: the category icon shows
  widget("gmail", "Gmail", "codewithaiyan", "productivity", "Unread mail at a glance.", "gmail-dark.png"),
  widget("watchlist", "Watchlist", "saif", "finance", "Your tickers, updated through the day.", null),      // no art yet
  widget("f1-timing", "F1 Timing", "saif", "sports", "Live timing through every session.", "f1-timing-dark.png"),
  widget("f1-hub", "F1 Hub", "saif", "sports", "The race weekend, start to finish.", null),                 // f1-timing.png belongs to F1 Timing
  widget("github", "GitHub", "saif", "developer", "Your profile, streak and contributions.", "github-dark.png"),
  widget("now-playing", "Now Playing", "saif", "media", "Spotify, your browser or Media Player.", "now-playing-dark.png"),
  widget("photo-frame", "Photo Frame", "saif", "media", "A picture from any folder. GIFs too.", "photo-frame.png"),
  widget("system", "System", "saif", "system", "CPU, memory and disk at a glance.", "system-dark.png"),
  widget("notes", "Notes", "saif", "productivity", "Type straight onto your desktop.", "notes-dark.png"),
  widget("reminders", "Reminders", "saif", "productivity", "What's next, without opening anything.", "reminders-light.png"),
  widget("focus-timer", "Focus Timer", "saif", "productivity", "Start, pause, and it survives a restart.", "focus-timer-dark.png"),
  widget("weather", "Weather", "saif", "weather", "Right now and the hours ahead.", "weather-light.png"),
  widget("calendar", "Calendar", "saif", "productivity", "Today, what's next and the month.", "calendar-dark.png"),
  // Edge cases: a widget with no preview at all, and one whose preview data will not decode.
  widget("no-preview", "Quiet Hours", "saif", "productivity", "No preview was ever sent for this one.", null),
  widget("broken-preview", "Broken Preview", "codewithaiyan", "developer", "Its preview data does not decode.", "BROKEN"),
];

// Newest first, an hour apart, so order(reviewed_at desc) has something to sort.
const START = Date.parse("2026-11-01T12:00:00Z");
ITEMS.forEach((item, i) => { item.row.reviewed_at = new Date(START - i * 3600000).toISOString(); });

export const widgets = ITEMS.map((item) => item.row);

export const profiles = [
  { username: "saif", bio: "Builds the small things that sit on a desktop.", avatar: null },
  { username: "codewithaiyan", bio: "Widgets for the things I check too often.", avatar: null },
  // No widgets and no bio: the profile page's empty state needs someone to show it with.
  { username: "newcomer", bio: "", avatar: null },
];

// versions.preview holds base64 PNG, so the art is read from disk and encoded the same way. The
// fetch is cached per id for the session.
const cache = new Map();

async function base64Of(path) {
  const response = await fetch(path);
  if (!response.ok) throw new Error(String(response.status));
  const bytes = new Uint8Array(await response.arrayBuffer());
  let binary = "";
  for (let i = 0; i < bytes.length; i += 8192) binary += String.fromCharCode(...bytes.subarray(i, i + 8192));
  return btoa(binary);
}

export async function previewsFor(ids, basePath) {
  const out = new Map();
  for (const id of ids) {
    const item = ITEMS.find((candidate) => candidate.row.id === id);
    if (!item || !item.art) { out.set(id, null); continue; }
    if (item.art === "BROKEN") { out.set(id, "bm90LWEtcG5n"); continue; }   // decodes to "not-a-png"
    if (!cache.has(item.art)) cache.set(item.art, base64Of(basePath + item.art).catch(() => null));
    out.set(id, await cache.get(item.art));
  }
  return out;
}

// ---- signed-in sample data for the upload page ----
// The shapes auth and the owner-side queries return: me() gives { user, profile }, widgets gives
// (id, slug), versions gives one row per sent version. Statuses cover all three pills.
export const session = {
  user: { id: "00000000-0000-4000-8000-000000000001", email: "saif@example.com" },
  profile: { username: "saif", bio: "Builds the small things that sit on a desktop.", avatar: null },
  admin: false,
};

const MINE = ["f1-timing", "weather", "notes", "watchlist"];
export const myWidgets = MINE.map((slug) => ({ id: "u.saif." + slug, slug }));

const STATUS = { "u.saif.f1-timing": "approved", "u.saif.weather": "approved",
  "u.saif.notes": "pending", "u.saif.watchlist": "rejected" };

export const myVersions = myWidgets.flatMap((w, i) => {
  const row = widgets.find((candidate) => candidate.id === w.id) || {};
  const sent = new Date(Date.parse("2026-11-01T12:00:00Z") - i * 86400000).toISOString();
  const versions = [{
    widget_id: w.id, version: 2, name: row.name || w.slug, category: row.category || "productivity",
    description: row.description || "", status: STATUS[w.id], created_at: sent,
  }];
  if (i < 2) versions.push({
    widget_id: w.id, version: 1, name: row.name || w.slug, category: row.category || "productivity",
    description: row.description || "", status: "approved",
    created_at: new Date(Date.parse(sent) - 7 * 86400000).toISOString(),
  });
  return versions;
}).sort((a, b) => (a.created_at < b.created_at ? 1 : -1));
