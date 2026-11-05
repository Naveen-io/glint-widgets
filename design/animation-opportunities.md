# Animation Opportunities

Places in the app where animations would improve the experience.
Status: planned. Implement one at a time, starting from the top.

## High Impact

1. **Right-click menu fade in/out** (`src/PopupMenu.cs`)
   Animate `UpdateLayeredWindow` opacity 0→255 over ~150ms on open, reverse on close. Submenus too.

2. **Widget appear/remove** (`src/WidgetWindow.cs`, `src/App.cs`)
   When placed on desktop or removed: scale 0.8→1.0 + opacity 0→255 over ~200ms.

3. **Alert/input dialog appear** (`src/AlertDialog.cs`)
   ~150ms fade-in on show, fade-out on close. Matches macOS sheets.

4. **Widget drag lift/drop** (`src/WidgetWindow.cs`)
   Subtle scale-up (~1.02x) with shadow when picking up. Scale back on drop.

## Medium Impact

5. **Library page transitions** (`src/LibraryWindow.cs`)
   Switching Explore/Installed/Settings: cross-fade or ~100ms content slide.

6. **Sidebar selection** (`src/LibraryWindow.cs`)
   ~120ms fill color transition between sidebar items.

7. **ExploreCard / WidgetCard hover** (`src/LibraryWindow.cs`)
   Subtle background color ease over ~150ms.

8. **Widget opacity changes** (`src/WidgetWindow.cs`)
   Animate `UpdateLayeredWindow` byte parameter over ~200ms.

9. **Tray notification style**
   If using custom notifications, slide in from system tray area.

## Polish

10. **Lock/unlock visual feedback**
    Brief pulse or border flash when toggling "Lock Position".

11. **Settings toggle switches** (`SidebarItem` with `Toggle = true`)
    ~150ms slide animation for the switch knob.

12. **Scroll to section**
    Extend `SmoothPanel` spring physics to programmatic scrolls on sidebar click.

13. **Update badge**
    Fade in the update label when "Check for Updates" finds something.
