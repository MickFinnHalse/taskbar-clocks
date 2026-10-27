using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TaskbarClocks
{
    /// <summary>
    /// The swatches offered when choosing a clock's colour: the classic 20-colour Paint
    /// palette, in its two rows of ten.
    /// </summary>
    public static class Palette
    {
        public const int PerRow = 10;

        public static readonly Color[] Colours =
        {
            ColourCode.FromHex("#000000"),   // black
            ColourCode.FromHex("#7F7F7F"),   // grey
            ColourCode.FromHex("#880015"),   // dark red
            ColourCode.FromHex("#ED1C24"),   // red
            ColourCode.FromHex("#FF7F27"),   // orange
            ColourCode.FromHex("#FFF200"),   // yellow
            ColourCode.FromHex("#22B14C"),   // green
            ColourCode.FromHex("#00A2E8"),   // turquoise
            ColourCode.FromHex("#3F48CC"),   // indigo
            ColourCode.FromHex("#A349A4"),   // purple

            ColourCode.FromHex("#FFFFFF"),   // white
            ColourCode.FromHex("#C3C3C3"),   // light grey
            ColourCode.FromHex("#B97A57"),   // brown
            ColourCode.FromHex("#FFAEC9"),   // rose
            ColourCode.FromHex("#FFC90E"),   // gold
            ColourCode.FromHex("#EFE4B0"),   // light yellow
            ColourCode.FromHex("#B5E61D"),   // lime
            ColourCode.FromHex("#99D9EA"),   // light turquoise
            ColourCode.FromHex("#7092BE"),   // blue-grey
            ColourCode.FromHex("#C8BFE7")    // lavender
        };

        /// <summary>
        /// Given to new clocks in turn. Strong, distinct colours that read on a light or a
        /// dark taskbar - the palette's own order would start new clocks on black.
        /// </summary>
        public static readonly Color[] StartingColours =
        {
            ColourCode.FromHex("#00A2E8"),   // turquoise
            ColourCode.FromHex("#22B14C"),   // green
            ColourCode.FromHex("#ED1C24"),   // red
            ColourCode.FromHex("#A349A4")    // purple
        };
    }

    /// <summary>
    /// Hex colour codes: "#1E9E44", "1E9E44", or the short form "#3BE".
    /// </summary>
    public static class ColourCode
    {
        public static bool TryParse(string input, out Color colour)
        {
            colour = Color.Empty;
            if (string.IsNullOrEmpty(input)) return false;
            string s = input.Trim().TrimStart('#');
            if (s.Length == 3)
                s = new string(new[] { s[0], s[0], s[1], s[1], s[2], s[2] });
            if (s.Length != 6) return false;

            int rgb;
            if (!int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb)) return false;
            colour = Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
            return true;
        }

        public static Color FromHex(string hex)
        {
            Color c;
            return TryParse(hex, out c) ? c : Color.White;
        }

        public static string ToHex(Color c)
        {
            return string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
        }
    }

    /// <summary>Run-key entry, so the clocks come back after a reboot.</summary>
    public static class Autostart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string Name   = "IllumEdTaskbarClocks";

        public static bool IsEnabled
        {
            get
            {
                try
                {
                    using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey))
                        return k != null && k.GetValue(Name) != null;
                }
                catch { return false; }
            }
        }

        public static void Set(bool enabled)
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (enabled)
                        k.SetValue(Name, "\"" + Application.ExecutablePath + "\"");
                    else if (k.GetValue(Name) != null)
                        k.DeleteValue(Name, false);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not change the startup setting.\r\n\r\n" + ex.Message,
                                "Taskbar Clocks", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
