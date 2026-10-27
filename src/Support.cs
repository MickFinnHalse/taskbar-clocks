using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TaskbarClocks
{
    /// <summary>The swatches offered when choosing a clock's colour.</summary>
    public static class Palette
    {
        public static readonly Color[] Colours =
        {
            ColourCode.FromHex("#1E9E44"),   // green
            ColourCode.FromHex("#19B5A5"),   // teal
            ColourCode.FromHex("#3B8EEA"),   // blue
            ColourCode.FromHex("#A970FF"),   // purple
            ColourCode.FromHex("#E85AA0"),   // pink
            ColourCode.FromHex("#E5534B"),   // red
            ColourCode.FromHex("#F2802B"),   // orange
            ColourCode.FromHex("#FFAB35"),   // amber
            ColourCode.FromHex("#8A8F98"),   // grey
            ColourCode.FromHex("#FFFFFF")    // white
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
