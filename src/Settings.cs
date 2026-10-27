using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;

namespace TaskbarClocks
{
    /// <summary>
    /// Auto: two rows once there are four clocks, otherwise one row.
    /// Row: all clocks side by side. TwoRows: two on top, the rest below.
    /// </summary>
    public enum ClockLayout { Auto, Row, TwoRows }

    /// <summary>Grey matches the taskbar's lightness, for light or dark mode.</summary>
    public enum ClockBackground { Grey, White, Black }

    /// <summary>One label-and-time pair on the taskbar.</summary>
    public sealed class ClockEntry
    {
        public string Label  { get; set; }
        public string City   { get; set; }
        public string ZoneId { get; set; }   // Windows time zone id, e.g. "AUS Eastern Standard Time"
        public Color  Colour { get; set; }

        /// <summary>What shows on the taskbar: the label, or the city when no label was given.</summary>
        public string Caption
        {
            get { return string.IsNullOrEmpty(Label) ? (City ?? "") : Label; }
        }
    }

    /// <summary>
    /// Plain key=value file kept next to the exe, so it stays portable and hand-editable.
    /// Each clock is one line: ClockN = Label | City | Windows time zone id | #RRGGBB
    /// </summary>
    public sealed class Settings
    {
        public const int MaxClocks = 4;

        public List<ClockEntry> Clocks       { get; private set; }
        public ClockLayout      Layout       { get; set; }
        public ClockBackground  Background   { get; set; }
        public bool             Use24Hour    { get; set; }
        public bool             AutoPosition { get; set; }

        // Where the user is, for the (+6) / (-1) after each time. An empty zone means
        // "follow this PC's own time zone", which is the default.
        public string           HomeCity     { get; set; }
        public string           HomeZoneId   { get; set; }
        public int              ManualX      { get; set; }
        public int              ManualY      { get; set; }
        public float            FontSize     { get; set; }   // largest size; shrinks to fit the taskbar

        private static string Path
        {
            get
            {
                string dir = System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
                return System.IO.Path.Combine(dir, "settings.ini");
            }
        }

        public static Settings Defaults()
        {
            return new Settings
            {
                Clocks       = new List<ClockEntry>(),
                Layout       = ClockLayout.Auto,
                Background   = ClockBackground.Grey,
                Use24Hour    = true,
                AutoPosition = true,
                HomeCity     = "",
                HomeZoneId   = "",
                ManualX      = -1,
                ManualY      = -1,
                FontSize     = 10f
            };
        }

        public static Settings Load()
        {
            Settings s = Defaults();
            var slots = new ClockEntry[MaxClocks];
            try
            {
                if (!File.Exists(Path)) return s;
                foreach (string raw in File.ReadAllLines(Path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string v = line.Substring(eq + 1).Trim();

                    if (k.Length == 6 && k.StartsWith("clock") && k[5] >= '1' && k[5] <= '0' + MaxClocks)
                    {
                        slots[k[5] - '1'] = ParseClock(v);
                        continue;
                    }
                    switch (k)
                    {
                        case "layout":       s.Layout       = ParseEnum(v, s.Layout);        break;
                        case "background":   s.Background   = ParseEnum(v, s.Background);    break;
                        case "use24hour":    s.Use24Hour    = ParseBool(v, s.Use24Hour);     break;
                        case "autoposition": s.AutoPosition = ParseBool(v, s.AutoPosition);  break;
                        case "manualx":      s.ManualX      = ParseInt(v, s.ManualX);        break;
                        case "manualy":      s.ManualY      = ParseInt(v, s.ManualY);        break;
                        case "fontsize":     s.FontSize     = ParseFloat(v, s.FontSize);     break;
                        case "homecity":     s.HomeCity     = v;                             break;
                        case "homezone":     s.HomeZoneId   = v;                             break;
                    }
                }
            }
            catch { /* a corrupt settings file must never stop the clocks starting */ }

            foreach (ClockEntry c in slots)
                if (c != null) s.Clocks.Add(c);

            if (s.FontSize < 6f)  s.FontSize = 6f;
            if (s.FontSize > 20f) s.FontSize = 20f;
            return s;
        }

        public void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# Taskbar Clocks settings. Close the clocks before editing by hand.");
                sb.AppendLine("# ClockN   = Label | City | Windows time zone id | #RRGGBB   (up to 4)");
                sb.AppendLine("# Layout     = Auto (two rows at four clocks) | Row | TwoRows");
                sb.AppendLine("# Background = Grey | White | Black");
                sb.AppendLine("# FontSize = largest size in points; shrinks automatically to fit the taskbar");
                sb.AppendLine("# HomeCity / HomeZone = where you are, for the (+6) after each time;");
                sb.AppendLine("#            HomeZone blank = follow this PC's own time zone");
                for (int i = 0; i < Clocks.Count && i < MaxClocks; i++)
                {
                    ClockEntry c = Clocks[i];
                    sb.AppendLine("Clock" + (i + 1) + "       = " + Clean(c.Label) + " | " + Clean(c.City)
                                  + " | " + c.ZoneId + " | " + ColourCode.ToHex(c.Colour));
                }
                sb.AppendLine("Layout       = " + Layout);
                sb.AppendLine("Background   = " + Background);
                sb.AppendLine("Use24Hour    = " + (Use24Hour ? "true" : "false"));
                sb.AppendLine("AutoPosition = " + (AutoPosition ? "true" : "false"));
                sb.AppendLine("ManualX      = " + ManualX);
                sb.AppendLine("ManualY      = " + ManualY);
                sb.AppendLine("FontSize     = " + FontSize.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("HomeCity     = " + Clean(HomeCity));
                sb.AppendLine("HomeZone     = " + (HomeZoneId ?? ""));
                File.WriteAllText(Path, sb.ToString());
            }
            catch { }
        }

        /// <summary>The bar is the field separator, so it cannot appear inside a field.</summary>
        public static string Clean(string s)
        {
            return (s ?? "").Replace("|", "/").Trim();
        }

        private static ClockEntry ParseClock(string v)
        {
            string[] parts = v.Split('|');
            if (parts.Length < 3) return null;
            string zone = parts[2].Trim();
            if (zone.Length == 0) return null;

            Color colour;
            if (parts.Length < 4 || !ColourCode.TryParse(parts[3], out colour))
                colour = Palette.Colours[0];

            return new ClockEntry
            {
                Label  = parts[0].Trim(),
                City   = parts[1].Trim(),
                ZoneId = zone,
                Colour = colour
            };
        }

        private static int   ParseInt(string v, int d)    { int r;   return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out r) ? r : d; }
        private static float ParseFloat(string v, float d){ float r; return float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out r) ? r : d; }
        private static bool  ParseBool(string v, bool d)  { return v.Equals("true", StringComparison.OrdinalIgnoreCase) ? true
                                                                 : v.Equals("false", StringComparison.OrdinalIgnoreCase) ? false : d; }
        private static T ParseEnum<T>(string v, T d) where T : struct
        {
            T r;
            return Enum.TryParse(v, true, out r) && Enum.IsDefined(typeof(T), r) ? r : d;
        }
    }

    /// <summary>
    /// Colours not chosen per clock. RestBack is the Grey background, and follows the
    /// Windows light/dark setting. Each clock's text colour is the user's own choice.
    /// </summary>
    public static class Theme
    {
        public static Color RestBack        { get; private set; }
        public static Color PlaceholderFore { get; private set; }

        public static Color SolidBack(ClockBackground b)
        {
            return b == ClockBackground.Black ? Color.Black
                 : b == ClockBackground.White ? Color.White : RestBack;
        }

        /// <summary>The "+ Add a clock" grey, readable on whichever background is in use.</summary>
        public static Color PlaceholderOn(ClockBackground b)
        {
            return b == ClockBackground.Black ? Color.FromArgb(170, 170, 170)
                 : b == ClockBackground.White ? Color.FromArgb(96, 96, 96) : PlaceholderFore;
        }

        static Theme() { Refresh(); }

        public static void Refresh()
        {
            bool light = SystemUsesLightTheme();
            // E4E4E4: a neutral grey at the light taskbar's measured lightness (sampled
            // at about 228 of 255 under its wallpaper tint, 27 Oct 2026).
            RestBack        = light ? Color.FromArgb(228, 228, 228) : Color.FromArgb(32, 32, 32);
            PlaceholderFore = light ? Color.FromArgb(96, 96, 96)    : Color.FromArgb(170, 170, 170);
        }

        private static bool SystemUsesLightTheme()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (k == null) return false;
                    object v = k.GetValue("SystemUsesLightTheme");
                    return v is int && (int)v == 1;
                }
            }
            catch { return false; }
        }
    }
}
