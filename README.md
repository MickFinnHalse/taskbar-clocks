# Taskbar Clocks

Up to four clocks for other places, each with your own label and colour, sitting just
left of the system tray:

```
 Anna 23:00   Ben 12:00
Carla 08:00   Dev 17:30
```

Same docking as [Taskbar Timer](../taskbar-timer/README.md). Windows 11 removed the
deskband API, so this is a small always-on-top window placed against the notification
area rather than something hosted inside the taskbar.

## What it does

- Shows up to four clocks, each as **label + time** in a colour you pick
- Finds **any town or city**, not just big ones (see below)
- Lays them out in one row, or in two rows stacked inside the taskbar's height
- Text size is yours to choose; anything too big for the taskbar shrinks to fit
- Background in grey, white or black
- Handles **daylight saving** through Windows' own time zone rules
- Hover over a clock for its place, the date there, whether daylight saving is on, and
  the UTC offset
- No taskbar button of its own, and stays out of Alt-Tab

## Use

| Action | Result |
|---|---|
| Left click on "+ Add a clock" | Add the first clock |
| Left drag | Move it; it stops following the tray |
| Hover | Details for the clock under the pointer |
| Right click | Each clock (Change, Move earlier/later, Remove), Add a clock, Layout, Text size, Background, 24-hour time, Snap back, Start with Windows, Exit |

**Adding or changing a clock:**

- **Label:** usually the person's name. If you leave it blank, the place name is shown.
- **City or town:** pick from the 191 built-in cities as you type. For anywhere else,
  press **Search** (or Enter) and choose from what comes back, e.g. *Port Hope, Ontario,
  Canada*. The time zone fills in by itself.
- **Same time zone as:** if the search finds nothing, or there is no internet, type a
  nearby built-in city instead. The clock keeps the name you typed and uses that city's
  time zone.
- **Time zone:** you can also pick it from the list directly. The dialog shows the time
  there now and whether daylight saving is on.
- **Colour:** click one of the 20 Paint-palette swatches, or type a hex code (`#3B8EEA`,
  `3B8EEA` or the short `#3BE`). A preview shows how it will look.

**Layout** (right-click → Layout):

| Setting | Effect |
|---|---|
| Automatic *(default)* | One row with up to three clocks; two rows of two at four |
| All in one row | Always side by side |
| Two rows | Two on top, the rest below |

Labels in a column are right-aligned, so every label sits one space from its time and
the times still line up.

Taskbar Timer sits in the same spot by the tray. If you run both, drag one of them aside
once; each remembers where you put it.

## Looking up towns

**Search** sends the name you typed to
[Open-Meteo's geocoding service](https://open-meteo.com/en/docs/geocoding-api). The
service is free, needs no key or account, and is built on GeoNames, so it knows small
towns. Only the typed name is sent. Each result comes back with its tz database zone
(e.g. `America/Toronto`), and Windows' own ICU library (`icu.dll`, part of Windows 10
1903 and later) turns that into the Windows zone the clock runs on. Airports, parks and
landmarks that share a town's name are left out of the results.

## Daylight saving

Each clock stores a Windows time zone id, and the time is worked out with
`TimeZoneInfo`, which reads the rules Windows keeps in the registry. Those rules come
through Windows Update, so a country changing its clocks needs no change here. The clocks
also re-read the rules every hour, so an update is picked up without a restart.

Checked on 27 Oct 2026: all 191 built-in cities were compared with the IANA tz database
at 26 dates across 2026–27, and every one matched. The one difference, Casablanca, turned
out to be the comparison copy being out of date: Morocco moved to permanent UTC+0 on
20 Sep 2026 (tzdata 2026c), and Windows already had that rule.

## Build

```
powershell -ExecutionPolicy Bypass -File "H:\My Drive\Master\Illumin-Ed\Tools\Claude\taskbar-clocks\build.ps1"
```

Compiles with the C# compiler already in `C:\Windows\Microsoft.NET\`. No SDK, no NuGet,
no .NET download. **Writes (overwriting) `TaskbarClocks.exe` in this folder and copies it to
`C:\Tools\TaskbarClocks`**, closing any running copy first. Run it from there, not from H:,
for the same antivirus reason as the timer.

## Settings

`settings.ini` sits next to the exe and is written on every change. You can edit it by hand,
but close the clocks first.

| Key | Meaning |
|---|---|
| `Clock1`–`Clock4` | `Label \| City \| Windows time zone id \| #RRGGBB` |
| `Layout` | `Auto`, `Row` or `TwoRows` |
| `Background` | `Grey`, `White` or `Black`. Grey is `#E4E4E4` in light mode and `#202020` in dark |
| `FontSize` | Text size in points (default 10); shrinks to fit the taskbar |
| `Use24Hour` | `true` or `false` |
| `AutoPosition` | `true` follows the tray, `false` uses `ManualX` / `ManualY` |

## Notes

- **Start with Windows** writes one `HKCU\...\Run` entry named `IllumEdTaskbarClocks`.
  Unticking it removes that entry. Nothing else in the registry is touched.
- Only one copy runs at a time; launching a second one exits silently.
- A full-screen app covers the clocks. Every overlay has this problem, because the taskbar
  itself is hidden then.
