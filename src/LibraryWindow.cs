using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace DeskWidgets
{
    sealed class LibraryWindow : Form
    {
        public const string HomePage = "@home";
        public const string ExplorePage = "@explore";
        public const string InstalledPage = "@installed";
        public const string SettingsPage = "@settings";

        sealed class Section
        {
            public WidgetCategory Category;
            public SectionHeader Header;
            public List<WidgetCard> Cards = new List<WidgetCard>();
        }

        readonly SmoothPanel content;
        readonly Panel sidebar;
        readonly PageHeader header;
        readonly EmptyState emptyInstalled;
        readonly PillButton browseExploreBtn;
        readonly PillButton toggleVisibilityBtn;
        readonly PillButton installFileBtn;

        // Home page controls
        readonly StatCard statActiveWidgets;
        readonly StatCard statMemory;
        readonly StatCard statEngine;
        readonly StatCard statVisibility;
        readonly SectionHeader quickLaunchHeader;
        readonly List<QuickActionCard> quickLaunchCards = new List<QuickActionCard>();
        readonly SectionHeader homeActiveHeader;
        readonly List<ActiveWidgetRow> homeActiveRows = new List<ActiveWidgetRow>();

        // Explore page controls
        readonly List<Section> sections = new List<Section>();

        // Installed page controls
        readonly List<ActiveWidgetRow> installedRows = new List<ActiveWidgetRow>();

        // Navigation
        readonly SidebarItem navHome;
        readonly SidebarItem navExplore;
        readonly SidebarItem navInstalled;
        readonly List<SidebarItem> catItems = new List<SidebarItem>();
        readonly SidebarItem settingsItem;

        // Settings page controls
        SidebarItem startupToggle, highPriorityToggle, pixelShiftToggle;
        Label appearanceLabel;
        SidebarItem[] themeItems;
        PillButton uninstallBtn;

        string currentPage = HomePage;
        string activeCategoryFilter = null; // null = all in Explore

        public LibraryWindow()
        {
            Text = "Glint Widgets";
            Icon = App.AppIcon;
            BackColor = Theme.WindowBg;
            ForeColor = Theme.Label;
            Font = Theme.Footnote;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Px(1040), Px(680));
            MinimumSize = new Size(Px(720), Px(480));

            content = new SmoothPanel { Dock = DockStyle.Fill, BackColor = Theme.WindowBg };
            content.HandleCreated += delegate { Theme.ApplyToScrollbars(content.Handle); };
            header = new PageHeader();
            content.Controls.Add(header);

            // Empty state for Installed page
            emptyInstalled = new EmptyState("No Widgets Running", "You have not added any widgets to your desktop yet.");
            content.Controls.Add(emptyInstalled);
            browseExploreBtn = new PillButton("Browse Explore Catalog", ButtonStyle.Primary);
            browseExploreBtn.Click += delegate { ShowPage(ExplorePage); };
            content.Controls.Add(browseExploreBtn);

            // Home Page Controls
            statActiveWidgets = new StatCard('\uE7F4', "Active Widgets", "0 Running", "On Desktop");
            statMemory = new StatCard('\uE950', "Process Memory", "35 MB", "Zero Browser Overhead");
            statEngine = new StatCard('\uE945', "Engine Runtime", "Native C# GDI+", "Direct Layered Blit");
            statVisibility = new StatCard('\uE7B3', "Desktop State", "Visible", "Pinned Above Icons");
            content.Controls.Add(statActiveWidgets);
            content.Controls.Add(statMemory);
            content.Controls.Add(statEngine);
            content.Controls.Add(statVisibility);

            toggleVisibilityBtn = new PillButton("Hide All Widgets", ButtonStyle.Tinted);
            toggleVisibilityBtn.Click += delegate
            {
                App.Current.ToggleWidgetsVisibility();
                UpdateVisibilityButton();
                UpdateStats();
            };
            content.Controls.Add(toggleVisibilityBtn);

            quickLaunchHeader = new SectionHeader("Essential Productivity Tools", "Instant one-click desktop activation.");
            content.Controls.Add(quickLaunchHeader);

            homeActiveHeader = new SectionHeader("Running On Desktop", "Currently active widgets on your workspace.");
            content.Controls.Add(homeActiveHeader);

            installFileBtn = new PillButton("Install from File…", ButtonStyle.Tinted);
            installFileBtn.Click += delegate { App.InstallFromFile(); };
            content.Controls.Add(installFileBtn);

            BuildCards();
            BuildQuickLaunch();
            BuildSettingsControls();

            content.Resize += delegate { LayoutContent(); };

            // Sidebar setup
            sidebar = new Panel { Dock = DockStyle.Left, Width = Px(240), BackColor = Theme.SidebarBg };

            var brandTitle = new Label
            {
                Text = "Glint Widgets",
                Font = Theme.Title3,
                ForeColor = Theme.Label,
                AutoSize = true,
                Location = new Point(Px(20), Px(20))
            };
            sidebar.Controls.Add(brandTitle);

            var brandSub = new Label
            {
                Text = "Productivity Suite",
                Font = Theme.Caption,
                ForeColor = Theme.Label2,
                AutoSize = true,
                Location = new Point(Px(20), Px(46))
            };
            sidebar.Controls.Add(brandSub);

            int y = Px(76);
            navHome = CreateNavItem('\uE80F', "Home", HomePage, ref y);
            navExplore = CreateNavItem('\uE71D', "Explore", ExplorePage, ref y);
            navInstalled = CreateNavItem('\uE7F4', "Installed", InstalledPage, ref y);

            y += Px(12);
            var catHeader = new Label
            {
                Text = "CATEGORIES",
                Font = Theme.CaptionEmphasized,
                ForeColor = Theme.Label2,
                AutoSize = true,
                Location = new Point(Px(20), y)
            };
            sidebar.Controls.Add(catHeader);
            y += Px(24);

            foreach (var cat in WidgetRegistry.Categories)
            {
                var item = CreateNavItem(cat.Glyph, cat.Name, cat.Id, ref y);
                catItems.Add(item);
            }

            settingsItem = new SidebarItem('\uE713', "Settings");
            settingsItem.SetBounds(Px(10), sidebar.Height - Px(48), sidebar.Width - Px(20), Px(36));
            settingsItem.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            settingsItem.Click += delegate { ShowPage(SettingsPage); };
            sidebar.Controls.Add(settingsItem);

            Controls.Add(content);
            Controls.Add(sidebar);

            App.Current.WidgetsChanged += OnWidgetsChanged;
            WidgetRegistry.Changed += OnRegistryChanged;
            Theme.Changed += OnThemeChanged;

            ShowPage(HomePage);
            UpdateNavBadges();
            UpdateStats();
        }

        static int Px(float v) { return WidgetWindow.Px(v); }

        SidebarItem CreateNavItem(char glyph, string text, string tag, ref int y)
        {
            var item = new SidebarItem(glyph, text) { Tag = tag };
            item.SetBounds(Px(10), y, sidebar.Width - Px(20), Px(36));
            item.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            item.Click += delegate
            {
                string target = (string)item.Tag;
                if (target == HomePage || target == ExplorePage || target == InstalledPage || target == SettingsPage)
                {
                    activeCategoryFilter = null;
                    ShowPage(target);
                }
                else
                {
                    activeCategoryFilter = target;
                    ShowPage(ExplorePage);
                }
            };
            sidebar.Controls.Add(item);
            y += Px(40);
            return item;
        }

        public void OpenSettings() { ShowPage(SettingsPage); }

        void BuildCards()
        {
            Func<WidgetType, WidgetCard> card = delegate(WidgetType t)
            {
                Bitmap preview = t.Snapshot();
                if (preview == null) return null;
                var c = new WidgetCard(t, preview);
                content.Controls.Add(c);
                return c;
            };

            foreach (var cat in WidgetRegistry.Categories)
            {
                var sec = new Section { Category = cat };
                foreach (var t in WidgetRegistry.InCategory(cat.Id))
                {
                    var c = card(t);
                    if (c != null) sec.Cards.Add(c);
                }
                if (sec.Cards.Count == 0) continue;
                sec.Header = new SectionHeader(cat.Name, cat.Description);
                string id = cat.Id;
                sec.Header.Click += delegate
                {
                    activeCategoryFilter = id;
                    ShowPage(ExplorePage);
                };
                content.Controls.Add(sec.Header);
                sections.Add(sec);
            }
        }

        void BuildQuickLaunch()
        {
            foreach (var c in quickLaunchCards) { content.Controls.Remove(c); c.Dispose(); }
            quickLaunchCards.Clear();

            string[] quickIds = { "notes", "system", "timer", "calc", "launcher", "clipboard" };
            char[] glyphs = { '\uE70F', '\uE950', '\uE916', '\uE8EF', '\uE737', '\uE77F' };

            for (int i = 0; i < quickIds.Length; i++)
            {
                var type = WidgetRegistry.Find(quickIds[i]);
                if (type != null)
                {
                    var q = new QuickActionCard(type, glyphs[i]);
                    content.Controls.Add(q);
                    quickLaunchCards.Add(q);
                }
            }
        }

        void BuildSettingsControls()
        {
            startupToggle = new SidebarItem('\uE7E8', "Start Desk Widgets with Windows")
            {
                Toggle = true,
                On = App.StartWithWindows
            };
            startupToggle.Click += delegate
            {
                App.StartWithWindows = !App.StartWithWindows;
                startupToggle.On = App.StartWithWindows;
            };
            content.Controls.Add(startupToggle);

            highPriorityToggle = new SidebarItem('\uE945', "High Priority Process (Smooth Render)")
            {
                Toggle = true,
                On = App.Current.HighPriority
            };
            highPriorityToggle.Click += delegate
            {
                App.Current.HighPriority = !App.Current.HighPriority;
                highPriorityToggle.On = App.Current.HighPriority;
            };
            content.Controls.Add(highPriorityToggle);

            pixelShiftToggle = new SidebarItem('\uE7F4', "OLED Pixel Shift (Burn-in Prevention)")
            {
                Toggle = true,
                On = App.Current.PixelShift
            };
            pixelShiftToggle.Click += delegate
            {
                App.Current.PixelShift = !App.Current.PixelShift;
                pixelShiftToggle.On = App.Current.PixelShift;
            };
            content.Controls.Add(pixelShiftToggle);

            appearanceLabel = new Label
            {
                Text = "APPEARANCE",
                Font = Theme.CaptionEmphasized,
                ForeColor = Theme.Label2,
                AutoSize = true
            };
            content.Controls.Add(appearanceLabel);

            string[] labels = { "System", "Light", "Dark" };
            string[] subs = { "Follows your Windows 11 system theme", "Pure light neutral canvas", "Minimalist dark slate & zinc theme" };
            AppTheme[] themes = { AppTheme.System, AppTheme.Light, AppTheme.Dark };
            themeItems = new SidebarItem[labels.Length];

            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                bool active = App.Current.AppThemeChoice == themes[i];
                var item = new SidebarItem(active ? '\uE73E' : '\0', labels[i])
                {
                    Selected = active,
                    Subtitle = subs[i]
                };
                item.Click += delegate
                {
                    App.Current.AppThemeChoice = themes[idx];
                    for (int j = 0; j < themeItems.Length; j++)
                    {
                        bool on = j == idx;
                        themeItems[j].Selected = on;
                        themeItems[j].Glyph = on ? '\uE73E' : '\0';
                        themeItems[j].Invalidate();
                    }
                };
                content.Controls.Add(item);
                themeItems[i] = item;
            }

            uninstallBtn = new PillButton("Uninstall Glint Widgets", ButtonStyle.Destructive);
            uninstallBtn.Click += delegate { App.Current.UninstallApp(); };
            content.Controls.Add(uninstallBtn);
        }

        void ShowPage(string page)
        {
            currentPage = page;

            navHome.Selected = page == HomePage;
            navExplore.Selected = page == ExplorePage && activeCategoryFilter == null;
            navInstalled.Selected = page == InstalledPage;
            settingsItem.Selected = page == SettingsPage;

            foreach (var item in catItems)
            {
                item.Selected = page == ExplorePage && (string)item.Tag == activeCategoryFilter;
                item.Invalidate();
            }

            navHome.Invalidate();
            navExplore.Invalidate();
            navInstalled.Invalidate();
            settingsItem.Invalidate();

            if (page == HomePage)
                header.Set("Workspace Overview", "Lightweight native desktop productivity suite running on Windows 11.");
            else if (page == ExplorePage)
            {
                if (activeCategoryFilter != null)
                {
                    var cat = WidgetRegistry.Categories.FirstOrDefault(c => c.Id == activeCategoryFilter);
                    header.Set(cat != null ? cat.Name : "Explore", cat != null ? cat.Description : "Browse all available desktop widgets.");
                }
                else
                    header.Set("Explore Widgets", "Click '+ Add to Desktop' on any card or drag it directly onto your desktop.");
            }
            else if (page == InstalledPage)
                header.Set("Installed Widgets", "Manage widgets currently placed and running on your desktop.");
            else if (page == SettingsPage)
                header.Set("Settings", "Preferences, appearance and native runtime configuration.");

            RefreshActiveRows();
            UpdateStats();
            content.ScrollToTop();
            content.SlideIn();
            LayoutContent();
        }

        void RefreshActiveRows()
        {
            foreach (var r in installedRows) { content.Controls.Remove(r); r.Dispose(); }
            installedRows.Clear();
            foreach (var r in homeActiveRows) { content.Controls.Remove(r); r.Dispose(); }
            homeActiveRows.Clear();

            var active = App.Current.ActiveWidgets;
            foreach (var w in active)
            {
                var row1 = new ActiveWidgetRow(w);
                content.Controls.Add(row1);
                installedRows.Add(row1);

                var row2 = new ActiveWidgetRow(w);
                content.Controls.Add(row2);
                homeActiveRows.Add(row2);
            }
        }

        void UpdateStats()
        {
            int count = App.Current.ActiveWidgets.Count;
            statActiveWidgets.UpdateData(count + " Running", count == 1 ? "1 Active Widget" : count + " Active Widgets");

            try
            {
                long memBytes = Process.GetCurrentProcess().WorkingSet64;
                float memMb = memBytes / (1024f * 1024f);
                statMemory.UpdateData(memMb.ToString("0.0") + " MB RAM", "Zero Chromium Overhead");
            }
            catch
            {
                statMemory.UpdateData("~35 MB RAM", "Native C# Footprint");
            }

            statVisibility.UpdateData(App.Current.AreAllWidgetsHidden ? "Hidden" : "Visible", "Pinned Above Icons");
            UpdateVisibilityButton();
        }

        void UpdateVisibilityButton()
        {
            toggleVisibilityBtn.Text = App.Current.AreAllWidgetsHidden ? "Show All Widgets" : "Hide All Widgets";
        }

        void UpdateNavBadges()
        {
            int count = App.Current.ActiveWidgets.Count;
            navInstalled.Badge = count;
            navInstalled.Invalidate();
        }

        void LayoutContent()
        {
            int pad = Px(32), gap = Px(20);
            int width = Math.Max(Px(300), content.ClientSize.Width - pad * 2);
            int y = Px(28);
            content.SuspendLayout();

            bool isHome = currentPage == HomePage;
            bool isExplore = currentPage == ExplorePage;
            bool isInstalled = currentPage == InstalledPage;
            bool isSettings = currentPage == SettingsPage;

            // Header layout
            header.Width = width;
            header.Location = new Point(pad, y);
            y += header.Height + Px(24);

            // Home Page
            statActiveWidgets.Visible = isHome;
            statMemory.Visible = isHome;
            statEngine.Visible = isHome;
            statVisibility.Visible = isHome;
            toggleVisibilityBtn.Visible = isHome;
            quickLaunchHeader.Visible = isHome;
            foreach (var q in quickLaunchCards) q.Visible = isHome;
            homeActiveHeader.Visible = isHome && homeActiveRows.Count > 0;
            foreach (var r in homeActiveRows) r.Visible = isHome;

            if (isHome)
            {
                int statW = (width - gap * 3) / 4;
                statActiveWidgets.SetBounds(pad, y, statW, statActiveWidgets.Height);
                statMemory.SetBounds(pad + (statW + gap), y, statW, statMemory.Height);
                statEngine.SetBounds(pad + (statW + gap) * 2, y, statW, statEngine.Height);
                statVisibility.SetBounds(pad + (statW + gap) * 3, y, statW, statVisibility.Height);
                y += statActiveWidgets.Height + Px(16);

                toggleVisibilityBtn.Location = new Point(pad, y);
                y += toggleVisibilityBtn.Height + Px(28);

                quickLaunchHeader.SetBounds(pad, y, width, quickLaunchHeader.Height);
                y += quickLaunchHeader.Height + Px(12);

                int qCols = Math.Max(1, (width + gap) / (Px(280) + gap));
                int qW = (width - (qCols - 1) * gap) / qCols;
                for (int i = 0; i < quickLaunchCards.Count; i++)
                {
                    int col = i % qCols;
                    int row = i / qCols;
                    int cx = pad + col * (qW + gap);
                    int cy = y + row * (Px(64) + Px(12));
                    quickLaunchCards[i].SetBounds(cx, cy, qW, Px(64));
                }
                int qRows = (quickLaunchCards.Count + qCols - 1) / qCols;
                y += qRows * (Px(64) + Px(12)) + Px(20);

                if (homeActiveRows.Count > 0)
                {
                    homeActiveHeader.SetBounds(pad, y, width, homeActiveHeader.Height);
                    y += homeActiveHeader.Height + Px(12);

                    foreach (var r in homeActiveRows)
                    {
                        r.SetBounds(pad, y, width, r.Height);
                        y += r.Height + Px(8);
                    }
                    y += Px(24);
                }
            }

            // Explore Page
            installFileBtn.Visible = isExplore;
            if (isExplore)
            {
                installFileBtn.Location = new Point(pad + width - installFileBtn.Width, header.Top + Px(4));
            }

            foreach (var sec in sections)
            {
                bool showSection = isExplore && (activeCategoryFilter == null || sec.Category.Id == activeCategoryFilter);
                sec.Header.Visible = showSection;
                foreach (var card in sec.Cards) card.Visible = showSection;

                if (!showSection) continue;

                sec.Header.SetBounds(pad, y, width, sec.Header.Height);
                y += sec.Header.Height + Px(12);

                y = Grid(sec.Cards, pad, width, y) + Px(28);
            }

            // Installed Page
            bool hasActive = installedRows.Count > 0;
            emptyInstalled.Visible = isInstalled && !hasActive;
            browseExploreBtn.Visible = isInstalled && !hasActive;
            foreach (var r in installedRows) r.Visible = isInstalled && hasActive;

            if (isInstalled)
            {
                if (!hasActive)
                {
                    emptyInstalled.SetBounds(pad, y + Px(40), width, emptyInstalled.Height);
                    browseExploreBtn.Location = new Point(pad + (width - browseExploreBtn.Width) / 2, emptyInstalled.Bottom + Px(16));
                    y += Px(40) + emptyInstalled.Height + Px(16) + browseExploreBtn.Height + Px(40);
                }
                else
                {
                    foreach (var r in installedRows)
                    {
                        r.SetBounds(pad, y, width, r.Height);
                        y += r.Height + Px(8);
                    }
                    y += Px(32);
                }
            }

            // Settings Page
            startupToggle.Visible = isSettings;
            highPriorityToggle.Visible = isSettings;
            pixelShiftToggle.Visible = isSettings;
            appearanceLabel.Visible = isSettings;
            foreach (var t in themeItems) t.Visible = isSettings;
            uninstallBtn.Visible = isSettings;

            if (isSettings)
            {
                int rowW = Math.Min(width, Px(520));

                startupToggle.SetBounds(pad, y, rowW, Px(50));
                y += Px(50) + Px(6);
                highPriorityToggle.SetBounds(pad, y, rowW, Px(50));
                y += Px(50) + Px(6);
                pixelShiftToggle.SetBounds(pad, y, rowW, Px(50));
                y += Px(50) + Px(24);

                appearanceLabel.Location = new Point(pad, y);
                y += appearanceLabel.Height + Px(8);

                for (int i = 0; i < themeItems.Length; i++)
                {
                    themeItems[i].SetBounds(pad, y, rowW, Px(46));
                    y += Px(46) + Px(4);
                }
                y += Px(24);

                uninstallBtn.Location = new Point(pad, y);
                y += uninstallBtn.Height + Px(32);
            }

            content.ContentHeight = y + Px(40);
            content.ResumeLayout();
        }

        int Grid(List<WidgetCard> cards, int left, int width, int startY)
        {
            int colW = Px(176), gap = Px(16);
            int cols = Math.Max(1, (width + gap) / (colW + gap));
            int col = 0, y = startY, rowH = 0;

            foreach (var card in cards)
            {
                bool wide = card.Width > colW + gap;
                if (wide && col == cols - 1)
                {
                    col = 0;
                    y += rowH + gap;
                    rowH = 0;
                }
                card.Location = new Point(left + col * (colW + gap), y);
                rowH = Math.Max(rowH, card.Height);
                col += wide ? 2 : 1;
                if (col >= cols)
                {
                    col = 0;
                    y += rowH + gap;
                    rowH = 0;
                }
            }
            return col == 0 ? y : y + rowH + gap;
        }

        void OnWidgetsChanged()
        {
            UpdateNavBadges();
            UpdateStats();
            RefreshActiveRows();
            LayoutContent();
            foreach (Control c in content.Controls) c.Invalidate();
        }

        bool rebuildPosted;
        void OnRegistryChanged()
        {
            if (rebuildPosted || !IsHandleCreated) return;
            rebuildPosted = true;
            BeginInvoke(new Action(Rebuild));
        }

        void Rebuild()
        {
            rebuildPosted = false;
            if (IsDisposed) return;
            content.SuspendLayout();
            ClearCards();
            BuildCards();
            BuildQuickLaunch();
            content.ResumeLayout();
            ShowPage(currentPage);
        }

        void ClearCards()
        {
            var old = new List<Control>();
            foreach (var sec in sections) { old.Add(sec.Header); old.AddRange(sec.Cards.Cast<Control>()); }
            foreach (var c in old) { content.Controls.Remove(c); c.Dispose(); }
            sections.Clear();
        }

        void OnThemeChanged()
        {
            BackColor = content.BackColor = Theme.WindowBg;
            sidebar.BackColor = Theme.SidebarBg;
            Theme.ApplyToWindow(Handle);
            Theme.ApplyToScrollbars(content.Handle);
            appearanceLabel.ForeColor = Theme.Label2;
            foreach (Control c in content.Controls) c.Invalidate();
            foreach (Control c in sidebar.Controls) c.Invalidate();
        }
    }

    // --- SUPPORTING CONTROLS ---

    public abstract class PaintedControl : Control
    {
        static readonly List<PaintedControl> hoverQueue = new List<PaintedControl>();
        static readonly Timer hoverAnim = new Timer { Interval = 16 };
        static PaintedControl()
        {
            hoverAnim.Tick += delegate
            {
                for (int i = hoverQueue.Count - 1; i >= 0; i--)
                {
                    var c = hoverQueue[i];
                    if (c.IsDisposed) { hoverQueue.RemoveAt(i); continue; }
                    float target = c.hoverTarget ? 1f : 0f;
                    float step = (target - c.HoverBlend) * 0.35f;
                    if (Math.Abs(step) < 0.01f)
                    {
                        c.HoverBlend = target;
                        hoverQueue.RemoveAt(i);
                    }
                    else c.HoverBlend += step;
                    c.Invalidate();
                }
                if (hoverQueue.Count == 0) hoverAnim.Stop();
            };
        }

        protected float HoverBlend;
        bool hoverTarget;

        protected PaintedControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
        }

        protected void AnimateHover(bool on)
        {
            hoverTarget = on;
            if (!hoverQueue.Contains(this)) hoverQueue.Add(this);
            hoverAnim.Start();
        }

        protected static int Px(float v) { return WidgetWindow.Px(v); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent != null ? Parent.BackColor : Theme.WindowBg);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Draw(e.Graphics);
        }

        protected abstract void Draw(Graphics g);

        protected override void Dispose(bool disposing)
        {
            if (disposing) hoverQueue.Remove(this);
            base.Dispose(disposing);
        }
    }

    sealed class StatCard : PaintedControl
    {
        string title, valueText, subtext;
        readonly char glyph;

        public StatCard(char glyph, string title, string valueText, string subtext)
        {
            this.glyph = glyph;
            this.title = title;
            this.valueText = valueText;
            this.subtext = subtext;
            Height = Px(78);
        }

        public void UpdateData(string valueText, string subtext)
        {
            this.valueText = valueText;
            this.subtext = subtext;
            Invalidate();
        }

        protected override void Draw(Graphics g)
        {
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Color bg = HoverBlend > 0f ? Theme.Blend(Theme.Card, Theme.CardHover, HoverBlend) : Theme.Card;
            Theme.FillRound(g, bg, r, Px(Theme.RadiusCard));
            using (var pen = new Pen(Theme.Border, 1f))
                using (var path = Theme.RoundRect(r, Px(Theme.RadiusCard)))
                    g.DrawPath(pen, path);

            int pad = Px(14);
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(g, title.ToUpperInvariant(), Theme.CaptionEmphasized, new Rectangle(pad, Px(10), Width - pad * 2, Px(14)), Theme.Label2, flags);
            TextRenderer.DrawText(g, valueText, Theme.Font(17, 700), new Rectangle(pad, Px(27), Width - pad * 2 - Px(24), Px(24)), Theme.Label, flags);
            TextRenderer.DrawText(g, subtext, Theme.Caption, new Rectangle(pad, Px(52), Width - pad * 2, Px(14)), Theme.Label2, flags);

            if (glyph != '\0')
            {
                TextRenderer.DrawText(g, glyph.ToString(), Theme.Icons, new Rectangle(Width - Px(34), Px(12), Px(22), Px(22)), Theme.Link, TextFormatFlags.NoPadding);
            }
        }
    }

    sealed class QuickActionCard : PaintedControl
    {
        readonly WidgetType type;
        readonly char glyph;
        bool btnHover;

        public QuickActionCard(WidgetType type, char glyph)
        {
            this.type = type;
            this.glyph = glyph;
            Height = Px(62);
            Cursor = Cursors.Hand;
        }

        Rectangle AddBtnBounds { get { return new Rectangle(Width - Px(84), (Height - Px(28)) / 2, Px(72), Px(28)); } }

        protected override void Draw(Graphics g)
        {
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Color bg = HoverBlend > 0f ? Theme.Blend(Theme.Card, Theme.CardHover, HoverBlend) : Theme.Card;
            Theme.FillRound(g, bg, r, Px(Theme.RadiusField));
            using (var pen = new Pen(Theme.Border, 1f))
                using (var path = Theme.RoundRect(r, Px(Theme.RadiusField)))
                    g.DrawPath(pen, path);

            int pad = Px(14);
            TextRenderer.DrawText(g, glyph.ToString(), Theme.Icons, new Rectangle(pad, (Height - Px(24)) / 2, Px(24), Px(24)), Theme.Link, TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);

            int textLeft = pad + Px(32);
            int textW = Width - textLeft - Px(94);
            TextRenderer.DrawText(g, type.Name, Theme.BodyEmphasized, new Rectangle(textLeft, Px(11), textW, Px(20)), Theme.Label, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, type.Description, Theme.Footnote, new Rectangle(textLeft, Px(33), textW, Px(18)), Theme.Label2, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

            Rectangle btn = AddBtnBounds;
            Color btnBg = btnHover ? Theme.PrimaryFill : Theme.Fill;
            Color btnFg = btnHover ? Theme.OnPrimary : Theme.Label;
            var btnRect = new RectangleF(btn.X, btn.Y, btn.Width, btn.Height);
            Theme.FillRound(g, btnBg, btnRect, Px(Theme.RadiusSmall));
            using (var p = new Pen(btnHover ? Theme.PrimaryFill : Theme.Border, 1f))
                using (var path = Theme.RoundRect(btnRect, Px(Theme.RadiusSmall)))
                    g.DrawPath(p, path);

            TextRenderer.DrawText(g, "＋ Add", Theme.CaptionEmphasized, btn, btnFg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool over = AddBtnBounds.Contains(e.Location);
            if (over != btnHover) { btnHover = over; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            btnHover = false;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                App.Current.AddWidget(type, null);
                Toast.Show("Widget Added", type.Name + " placed on your desktop.");
            }
        }
    }

    sealed class ActiveWidgetRow : PaintedControl
    {
        readonly WidgetWindow widget;
        readonly WidgetType type;
        Rectangle locateBtn, lockBtn, opacityBtn, removeBtn;
        int hoverBtn = -1; // 0=locate, 1=lock, 2=opacity, 3=remove

        public ActiveWidgetRow(WidgetWindow widget)
        {
            this.widget = widget;
            this.type = WidgetRegistry.Find(widget.Settings.Type);
            Height = Px(64);
            Cursor = Cursors.Default;
        }

        protected override void Draw(Graphics g)
        {
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Color bg = HoverBlend > 0f ? Theme.Blend(Theme.Card, Theme.CardHover, HoverBlend) : Theme.Card;
            Theme.FillRound(g, bg, r, Px(Theme.RadiusField));
            using (var pen = new Pen(Theme.Border, 1f))
                using (var path = Theme.RoundRect(r, Px(Theme.RadiusField)))
                    g.DrawPath(pen, path);

            int pad = Px(16);
            string name = type != null ? type.Name : widget.Settings.Type;
            Point loc = widget.Location;
            Size sz = widget.WidgetSize;
            Screen scr = Screen.FromControl(widget);
            string scrName = scr.Primary ? "Primary Display" : "Secondary Display";
            string info = string.Format("{0} ({1}x{2})  •  Pos: ({3}, {4})  •  Size: {5}x{6}", scrName, scr.Bounds.Width, scr.Bounds.Height, loc.X, loc.Y, sz.Width, sz.Height);

            int btnAreaW = Px(260);
            TextRenderer.DrawText(g, name, Theme.BodyEmphasized, new Rectangle(pad, Px(12), Width - pad * 2 - btnAreaW, Px(20)), Theme.Label, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, info, Theme.Footnote, new Rectangle(pad, Px(34), Width - pad * 2 - btnAreaW, Px(18)), Theme.Label2, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            int btnH = Px(28), gap = Px(6);
            int curX = Width - pad - Px(28);

            // Remove Button
            removeBtn = new Rectangle(curX, (Height - btnH) / 2, Px(28), btnH);
            Color remBg = hoverBtn == 3 ? Theme.Destructive : Theme.Fill;
            Color remFg = hoverBtn == 3 ? Color.White : Theme.Destructive;
            DrawBtn(g, removeBtn, remBg, remFg, "\uE74D", true);

            // Opacity Button
            curX -= Px(64) + gap;
            opacityBtn = new Rectangle(curX, (Height - btnH) / 2, Px(64), btnH);
            DrawBtn(g, opacityBtn, hoverBtn == 2 ? Theme.Tinted : Theme.Fill, hoverBtn == 2 ? Theme.Link : Theme.Label, widget.WidgetOpacity + "%", false);

            // Lock Button
            curX -= Px(76) + gap;
            lockBtn = new Rectangle(curX, (Height - btnH) / 2, Px(76), btnH);
            bool locked = widget.IsLocked;
            Color lockBg = locked ? Theme.Tinted : hoverBtn == 1 ? Theme.FillHighlight : Theme.Fill;
            Color lockFg = locked ? Theme.Link : Theme.Label;
            DrawBtn(g, lockBtn, lockBg, lockFg, locked ? "\uE72E Locked" : "\uE72E Unlock", false);

            // Locate Button
            curX -= Px(76) + gap;
            locateBtn = new Rectangle(curX, (Height - btnH) / 2, Px(76), btnH);
            DrawBtn(g, locateBtn, hoverBtn == 0 ? Theme.PrimaryFill : Theme.Fill, hoverBtn == 0 ? Theme.OnPrimary : Theme.Label, "\uE895 Locate", false);
        }

        void DrawBtn(Graphics g, Rectangle rect, Color bg, Color fg, string text, bool isIcon)
        {
            var rf = new RectangleF(rect.X, rect.Y, rect.Width, rect.Height);
            Theme.FillRound(g, bg, rf, Px(Theme.RadiusSmall));
            using (var p = new Pen(Theme.Border, 1f))
                using (var path = Theme.RoundRect(rf, Px(Theme.RadiusSmall)))
                    g.DrawPath(p, path);

            Font f = isIcon ? Theme.Icons : Theme.CaptionEmphasized;
            TextRenderer.DrawText(g, text, f, rect, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int old = hoverBtn;
            if (locateBtn.Contains(e.Location)) hoverBtn = 0;
            else if (lockBtn.Contains(e.Location)) hoverBtn = 1;
            else if (opacityBtn.Contains(e.Location)) hoverBtn = 2;
            else if (removeBtn.Contains(e.Location)) hoverBtn = 3;
            else hoverBtn = -1;
            if (old != hoverBtn) Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoverBtn = -1;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            if (locateBtn.Contains(e.Location))
            {
                widget.FlashLock();
            }
            else if (lockBtn.Contains(e.Location))
            {
                widget.IsLocked = !widget.IsLocked;
                Invalidate();
            }
            else if (opacityBtn.Contains(e.Location))
            {
                int cur = widget.WidgetOpacity;
                int next = cur > 85 ? 85 : cur > 70 ? 70 : cur > 50 ? 50 : 100;
                widget.WidgetOpacity = next;
                Invalidate();
            }
            else if (removeBtn.Contains(e.Location))
            {
                App.Current.RemoveWidget(widget, false);
            }
        }
    }

    sealed class WidgetCard : PaintedControl
    {
        readonly WidgetType type;
        readonly Bitmap preview;
        bool dragging;
        Point downAt;
        DragGhost ghost;
        bool addHover;

        public WidgetCard(WidgetType type, Bitmap preview)
        {
            this.type = type;
            this.preview = preview;
            bool wide = preview != null && preview.Width > preview.Height * 1.5f;
            Size = new Size(Px(wide ? 176 * 2 + 16 : 176), Px(252));
            Cursor = Cursors.Hand;
        }

        Rectangle AddButtonBounds
        {
            get
            {
                int pad = Px(10);
                return new Rectangle(pad, Height - Px(34), Width - pad * 2, Px(26));
            }
        }

        protected override void Draw(Graphics g)
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int pad = Px(10);
            Color bg = HoverBlend > 0f ? Theme.Blend(Theme.Card, Theme.CardHover, HoverBlend) : Theme.Card;
            var cardRect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.FillRound(g, bg, cardRect, Px(Theme.RadiusCard));
            using (var pen = new Pen(Theme.Border, 1f))
                using (var path = Theme.RoundRect(cardRect, Px(Theme.RadiusCard)))
                    g.DrawPath(pen, path);

            if (preview != null)
            {
                int boxW = Width - pad * 2, box = Px(138);
                float scale = Math.Min(boxW / (float)preview.Width, box / (float)preview.Height);
                int pw = (int)(preview.Width * scale), ph = (int)(preview.Height * scale);
                g.DrawImage(preview, new Rectangle((Width - pw) / 2, pad + (box - ph) / 2, pw, ph));
            }

            int textTop = pad + Px(142), lineW = Width - pad * 2;
            var flags = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
            TextRenderer.DrawText(g, type.Name, Theme.BodyEmphasized, new Rectangle(pad, textTop, lineW, Px(18)), Theme.Label, bg, flags);
            TextRenderer.DrawText(g, type.Description, Theme.Footnote, new Rectangle(pad, textTop + Px(19), lineW, Px(16)), Theme.Label2, bg, flags);

            // Add Button
            Rectangle btn = AddButtonBounds;
            Color btnBg = addHover ? Theme.PrimaryFill : Theme.Fill;
            Color btnText = addHover ? Theme.OnPrimary : Theme.Label;
            var btnRect = new RectangleF(btn.X, btn.Y, btn.Width, btn.Height);
            Theme.FillRound(g, btnBg, btnRect, Px(Theme.RadiusSmall));
            using (var pen = new Pen(addHover ? Theme.PrimaryFill : Theme.Border, 1f))
                using (var path = Theme.RoundRect(btnRect, Px(Theme.RadiusSmall)))
                    g.DrawPath(pen, path);

            int count = App.Current.CountOf(type.Id);
            string btnLabel = count > 0 ? "＋ Add (" + count + " on desk)" : "＋ Add to Desktop";
            TextRenderer.DrawText(g, btnLabel, Theme.CaptionEmphasized, btn, btnText, flags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        protected override void OnMouseEnter(EventArgs e) { AnimateHover(true); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { AnimateHover(false); addHover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool overAdd = AddButtonBounds.Contains(e.Location);
            if (overAdd != addHover) { addHover = overAdd; Invalidate(); }

            if (e.Button != MouseButtons.Left || addHover) return;
            if (!dragging)
            {
                Size slop = SystemInformation.DragSize;
                if (Math.Abs(e.X - downAt.X) < slop.Width && Math.Abs(e.Y - downAt.Y) < slop.Height) return;
                dragging = true;
                ghost = new DragGhost(preview);
                ghost.CenterOn(Cursor.Position);
                ghost.Show();
                return;
            }
            if (ghost != null) ghost.CenterOn(Cursor.Position);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) downAt = e.Location;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Right) ShowMenu();
            else
            {
                if (AddButtonBounds.Contains(e.Location) && !dragging)
                {
                    App.Current.AddWidget(type, null);
                    Toast.Show("Widget Added", type.Name + " placed on your desktop.");
                    Invalidate();
                }
                else Drop();
            }
        }

        void ShowMenu()
        {
            var menu = new PopupMenu();
            menu.Add("Add to Desktop", delegate
            {
                App.Current.AddWidget(type, null);
                Toast.Show("Widget Added", type.Name + " placed on your desktop.");
            });
            menu.Show(Cursor.Position);
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture) Drop();
        }

        void Drop()
        {
            if (!dragging) return;
            EndDrag();
            Point at = Cursor.Position;
            Form f = FindForm();
            if (f != null && !f.Bounds.Contains(at))
            {
                App.Current.AddWidget(type, at);
                Toast.Show("Widget Added", type.Name + " placed on your desktop.");
            }
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            App.Current.AddWidget(type, null);
            Toast.Show("Widget Added", type.Name + " placed on your desktop.");
            Invalidate();
        }

        void EndDrag()
        {
            dragging = false;
            if (ghost != null) { ghost.Dispose(); ghost = null; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                EndDrag();
                if (preview != null) preview.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    sealed class DragGhost : Form
    {
        readonly LayeredSurface surface;

        public DragGhost(Bitmap image)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Size = image.Size;
            surface = new LayeredSurface(image.Width, image.Height);
            surface.Graphics.CompositingMode = CompositingMode.SourceCopy;
            surface.Graphics.DrawImageUnscaled(image, 0, 0);
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            surface.Present(Handle, 190);
        }

        public void CenterOn(Point screen)
        {
            Location = new Point(screen.X - Width / 2, screen.Y - Height / 2);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE |
                              Native.WS_EX_TRANSPARENT | Native.WS_EX_TOPMOST;
                return cp;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && surface != null) surface.Dispose();
            base.Dispose(disposing);
        }
    }

    sealed class PageHeader : PaintedControl
    {
        string title = "", subtitle = "";

        public void Set(string title, string subtitle)
        {
            this.title = title;
            this.subtitle = subtitle;
            Height = Px(40) + Px(4) + Theme.Footnote.Height;
            Invalidate();
        }

        protected override void Draw(Graphics g)
        {
            TextRenderer.DrawText(g, title, Theme.LargeTitle, Point.Empty, Theme.Label, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, subtitle, Theme.Footnote, new Rectangle(0, Px(44), Width, Theme.Footnote.Height + Px(4)),
                Theme.Label2, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }

    sealed class SectionHeader : PaintedControl
    {
        readonly string title, description;

        public SectionHeader(string title, string description)
        {
            this.title = title;
            this.description = description;
            Cursor = Cursors.Hand;
            Height = Px(24) + Px(2) + Theme.Footnote.Height;
        }

        protected override void Draw(Graphics g)
        {
            var flags = TextFormatFlags.NoPadding;
            TextRenderer.DrawText(g, title, Theme.SectionTitle, Point.Empty, Theme.Label, flags);
            TextRenderer.DrawText(g, description, Theme.Footnote, new Rectangle(0, Px(24), Width, Theme.Footnote.Height + Px(2)),
                Theme.Label2, flags | TextFormatFlags.EndEllipsis);
        }
    }

    sealed class EmptyState : PaintedControl
    {
        string title, message;

        public EmptyState(string title, string message)
        {
            this.title = title;
            this.message = message;
            Height = Px(32) + Px(6) + Theme.Footnote.Height;
        }

        public void Set(string title, string message)
        {
            this.title = title;
            this.message = message;
            Invalidate();
        }

        protected override void Draw(Graphics g)
        {
            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding;
            TextRenderer.DrawText(g, title, Theme.Font(20, 700), new Rectangle(0, 0, Width, Px(30)), Theme.Label, flags);
            TextRenderer.DrawText(g, message, Theme.Footnote, new Rectangle(0, Px(34), Width, Theme.Footnote.Height), Theme.Label2, flags);
        }
    }

    sealed class SidebarItem : PaintedControl
    {
        public char Glyph;
        public string Subtitle;
        public bool Toggle;
        public int Badge;

        bool selectedValue;
        float selectionBlend;
        Anim selectionAnim;

        bool onValue;
        float toggleBlend;
        Anim toggleAnim;

        public bool Selected
        {
            get { return selectedValue; }
            set
            {
                if (value == selectedValue) return;
                selectedValue = value;
                if (IsHandleCreated)
                {
                    if (selectionAnim != null) selectionAnim.Cancel();
                    selectionAnim = Anim.Run(selectionBlend, value ? 1f : 0f, 120,
                        delegate(float v) { selectionBlend = v; Invalidate(); }, delegate { selectionAnim = null; });
                }
                else selectionBlend = value ? 1f : 0f;
            }
        }

        public bool On
        {
            get { return onValue; }
            set
            {
                if (value == onValue) return;
                onValue = value;
                if (IsHandleCreated)
                {
                    if (toggleAnim != null) toggleAnim.Cancel();
                    toggleAnim = Anim.Run(toggleBlend, value ? 1f : 0f, 150,
                        delegate(float v) { toggleBlend = v; Invalidate(); }, delegate { toggleAnim = null; });
                }
                else toggleBlend = value ? 1f : 0f;
            }
        }

        public SidebarItem(char glyph, string text)
        {
            Glyph = glyph;
            Text = text;
            Cursor = Cursors.Hand;
        }

        protected override void Draw(Graphics g)
        {
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            float blend = Math.Max(selectionBlend, HoverBlend * 0.5f);
            if (blend > 0f)
            {
                Color sel = Theme.Selection;
                Theme.FillRound(g, Color.FromArgb((int)(sel.A * blend), sel), r, Px(Theme.RadiusSmall));
            }

            if (selectedValue)
            {
                using (var pill = new SolidBrush(Theme.Link))
                    g.FillRectangle(pill, 0, Px(8), Px(3), Height - Px(16));
            }

            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
            if (Glyph != '\0')
            {
                Color iconColor = selectedValue ? Theme.Link : Theme.Label2;
                TextRenderer.DrawText(g, Glyph.ToString(), Theme.Icons, new Rectangle(Px(12), 0, Px(20), Height), iconColor, flags);
            }

            int textLeft = Glyph != '\0' ? Px(40) : Px(16);
            int textW = Width - textLeft - (Toggle ? Px(46) : Badge > 0 ? Px(40) : Px(12));
            bool hasSub = !string.IsNullOrEmpty(Subtitle);

            if (hasSub)
            {
                int top = (Height - Px(16) - Px(14)) / 2;
                TextRenderer.DrawText(g, Text, selectedValue ? Theme.Font(13, 600) : Theme.Font(13),
                    new Rectangle(textLeft, top, textW, Px(18)), Theme.Label, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, Subtitle, Theme.Caption,
                    new Rectangle(textLeft, top + Px(18), textW, Px(14)), Theme.Label2, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }
            else
            {
                TextRenderer.DrawText(g, Text, selectedValue ? Theme.Font(13, 600) : Theme.Font(13),
                    new Rectangle(textLeft, 0, textW, Height), Theme.Label, flags | TextFormatFlags.EndEllipsis);
            }

            if (Badge > 0)
            {
                int badgeW = Px(22), badgeH = Px(18);
                var bRect = new Rectangle(Width - badgeW - Px(10), (Height - badgeH) / 2, badgeW, badgeH);
                var bRectF = new RectangleF(bRect.X, bRect.Y, bRect.Width, bRect.Height);
                Theme.FillRound(g, Theme.FillHighlight, bRectF, Px(Theme.RadiusSmall));
                TextRenderer.DrawText(g, Badge.ToString(), Theme.CaptionEmphasized, bRect, Theme.Label2,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            if (Toggle)
            {
                var track = new RectangleF(Width - Px(44), (Height - Px(20)) / 2f, Px(34), Px(20));
                Color trackColor = Theme.Blend(Theme.Fill, Theme.PrimaryFill, toggleBlend);
                Theme.FillRound(g, trackColor, track, track.Height / 2);
                float d = track.Height - Px(4);
                float offX = track.Left + Px(2);
                float onX = track.Right - d - Px(2);
                float knobX = offX + (onX - offX) * toggleBlend;
                using (var knob = new SolidBrush(Color.White))
                    g.FillEllipse(knob, knobX, track.Top + Px(2), d, d);
            }
        }

        protected override void OnMouseEnter(EventArgs e) { AnimateHover(true); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { AnimateHover(false); base.OnMouseLeave(e); }
    }
}
