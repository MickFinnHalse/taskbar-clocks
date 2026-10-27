using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TaskbarClocks
{
    /// <summary>
    /// Add or change one clock: a label, a city (which fills in the time zone), and a
    /// colour from the palette or typed as a hex code.
    /// </summary>
    public sealed class ClockDialog : Form
    {
        private readonly TextBox  _label;
        private readonly TextBox  _city;
        private readonly Label    _match;
        private readonly ComboBox _zone;
        private readonly Label    _now;
        private readonly TextBox  _hex;
        private readonly Panel    _preview;
        private readonly Button[] _swatches;
        private readonly Timer    _tick;
        private readonly bool     _use24Hour;
        private readonly Font     _previewLabelFont = new Font("Segoe UI", 10f, FontStyle.Regular);
        private readonly Font     _previewTimeFont  = new Font("Segoe UI", 10f, FontStyle.Bold);

        private Color _colour;
        private bool  _settingHex;

        public ClockEntry Result { get; private set; }

        public ClockDialog(ClockEntry existing, Color colour, bool use24Hour)
        {
            _colour    = colour;
            _use24Hour = use24Hour;

            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode       = AutoScaleMode.Dpi;
            Text            = existing == null ? "Add a clock" : "Change clock";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterScreen;
            MinimizeBox     = false;
            MaximizeBox     = false;
            ShowInTaskbar   = false;
            TopMost         = true;
            ClientSize      = new Size(440, 402);

            Controls.Add(Caption("Label  (e.g. the person's name)", 12));
            _label = new TextBox { Bounds = new Rectangle(12, 32, 416, 24), MaxLength = 40 };
            Controls.Add(_label);

            Controls.Add(Caption("City  (start typing, e.g. Sydney)", 66));
            _city = new TextBox
            {
                Bounds             = new Rectangle(12, 86, 416, 24),
                AutoCompleteMode   = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.CustomSource
            };
            var names = new AutoCompleteStringCollection();
            foreach (City c in Cities.All) names.Add(c.Display);
            _city.AutoCompleteCustomSource = names;
            Controls.Add(_city);

            _match = new Label { Bounds = new Rectangle(12, 112, 416, 18), ForeColor = SystemColors.GrayText };
            Controls.Add(_match);

            Controls.Add(Caption("Time zone", 136));
            _zone = new ComboBox
            {
                Bounds        = new Rectangle(12, 156, 416, 24),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            ReadOnlyCollection<TimeZoneInfo> zones = TimeZoneInfo.GetSystemTimeZones();
            foreach (TimeZoneInfo tz in zones) _zone.Items.Add(tz);   // shows DisplayName
            Controls.Add(_zone);

            _now = new Label { Bounds = new Rectangle(12, 186, 416, 34) };
            Controls.Add(_now);

            Controls.Add(Caption("Colour", 234));
            _swatches = new Button[Palette.Colours.Length];
            for (int i = 0; i < Palette.Colours.Length; i++)
            {
                Color sw = Palette.Colours[i];
                var b = new Button
                {
                    Bounds    = new Rectangle(12 + i * 32, 254, 26, 26),
                    BackColor = sw,
                    FlatStyle = FlatStyle.Flat,
                    TabStop   = false
                };
                b.FlatAppearance.MouseOverBackColor = sw;
                b.FlatAppearance.MouseDownBackColor = sw;
                b.Click += delegate { SetColour(sw, true); };
                _swatches[i] = b;
                Controls.Add(b);
            }

            Controls.Add(new Label { Text = "or hex code", Bounds = new Rectangle(12, 296, 80, 20) });
            _hex = new TextBox { Bounds = new Rectangle(94, 293, 86, 24), MaxLength = 7 };
            _hex.TextChanged += delegate { OnHexTyped(); };
            Controls.Add(_hex);

            // How it will look on the taskbar, on the taskbar's own background.
            _preview = new Panel
            {
                Bounds      = new Rectangle(196, 288, 232, 34),
                BackColor   = Theme.RestBack,
                BorderStyle = BorderStyle.FixedSingle
            };
            _preview.Paint += OnPreviewPaint;
            Controls.Add(_preview);

            var ok = new Button
            {
                Text   = existing == null ? "Add" : "Save",
                Bounds = new Rectangle(272, 358, 74, 28)
            };
            var cancel = new Button
            {
                Text         = "Cancel",
                DialogResult = DialogResult.Cancel,
                Bounds       = new Rectangle(354, 358, 74, 28)
            };
            ok.Click += delegate { Commit(); };
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;

            if (existing != null)
            {
                _label.Text = existing.Label;
                _city.Text  = existing.City;
                SelectZone(existing.ZoneId);
            }
            _city.TextChanged          += delegate { OnCityTyped(); };
            _label.TextChanged         += delegate { _preview.Invalidate(); };
            _zone.SelectedIndexChanged += delegate { ShowTimeThere(); _preview.Invalidate(); };

            ShowMatch(Cities.Find(_city.Text));
            SetColour(_colour, true);
            ShowTimeThere();

            // Keep "time there now" honest if the dialog is left open over a minute change.
            _tick = new Timer { Interval = 1000 };
            _tick.Tick += delegate { ShowTimeThere(); };
            _tick.Start();
        }

        private static Label Caption(string text, int y)
        {
            return new Label
            {
                Text   = text,
                Bounds = new Rectangle(12, y, 416, 18),
                Font   = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
        }

        // ---------- city and zone ----------

        private void OnCityTyped()
        {
            City c = Cities.Find(_city.Text);
            if (c != null) SelectZone(c.ZoneId);
            ShowMatch(c);
            _preview.Invalidate();
        }

        private void ShowMatch(City c)
        {
            if (_city.Text.Trim().Length == 0)
                _match.Text = "";
            else if (c != null)
                _match.Text = "Found " + c.Display + ". Its time zone is filled in below.";
            else
                _match.Text = "Not in the built-in list - pick its time zone below.";
        }

        private void SelectZone(string id)
        {
            for (int i = 0; i < _zone.Items.Count; i++)
                if (((TimeZoneInfo)_zone.Items[i]).Id == id) { _zone.SelectedIndex = i; return; }
        }

        private TimeZoneInfo SelectedZone
        {
            get { return _zone.SelectedItem as TimeZoneInfo; }
        }

        private void ShowTimeThere()
        {
            TimeZoneInfo tz = SelectedZone;
            if (tz == null) { _now.Text = ""; return; }
            DateTime utc   = DateTime.UtcNow;
            DateTime there = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
            string text = "Time there now: " + there.ToString(TimeFormat, CultureInfo.InvariantCulture)
                        + ", " + there.ToString("ddd d MMM", CultureInfo.CurrentCulture)
                        + "\r\n" + DaylightSaving.Describe(tz, utc);
            if (_now.Text != text)
            {
                _now.Text = text;
                _preview.Invalidate();
            }
        }

        private string TimeFormat
        {
            get { return _use24Hour ? "HH:mm" : "h:mm tt"; }
        }

        // ---------- colour ----------

        private void SetColour(Color c, bool updateHex)
        {
            _colour = c;
            if (updateHex)
            {
                _settingHex = true;
                _hex.Text = ColourCode.ToHex(c);
                _settingHex = false;
            }
            _hex.BackColor = SystemColors.Window;

            // The chosen swatch gets a thick outline; the rest a thin one.
            for (int i = 0; i < _swatches.Length; i++)
            {
                bool chosen = Palette.Colours[i].ToArgb() == c.ToArgb();
                _swatches[i].FlatAppearance.BorderSize  = chosen ? 3 : 1;
                _swatches[i].FlatAppearance.BorderColor = chosen ? SystemColors.Highlight : SystemColors.ControlDark;
            }
            _preview.Invalidate();
        }

        private void OnHexTyped()
        {
            if (_settingHex) return;
            Color c;
            if (ColourCode.TryParse(_hex.Text, out c))
                SetColour(c, false);
            else
                _hex.BackColor = Color.FromArgb(255, 228, 228);   // not a colour yet
        }

        private void OnPreviewPaint(object sender, PaintEventArgs e)
        {
            string caption = _label.Text.Trim().Length > 0 ? _label.Text.Trim()
                           : _city.Text.Trim().Length  > 0 ? CityName() : "Label";
            TimeZoneInfo tz = SelectedZone;
            string time = tz == null ? "--:--"
                        : TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).ToString(TimeFormat, CultureInfo.InvariantCulture);

            const TextFormatFlags f = TextFormatFlags.SingleLine | TextFormatFlags.NoPadding
                                    | TextFormatFlags.VerticalCenter;
            Size ls = TextRenderer.MeasureText(caption, _previewLabelFont, Size.Empty, f);
            Rectangle r = _preview.ClientRectangle;
            TextRenderer.DrawText(e.Graphics, caption, _previewLabelFont,
                                  new Rectangle(10, 0, ls.Width, r.Height), _colour, f);
            TextRenderer.DrawText(e.Graphics, time, _previewTimeFont,
                                  new Rectangle(10 + ls.Width + 6, 0, r.Width - ls.Width - 16, r.Height), _colour, f);
        }

        // ---------- result ----------

        /// <summary>The bare city name when it matched the list, otherwise what was typed.</summary>
        private string CityName()
        {
            City c = Cities.Find(_city.Text);
            return c != null ? c.Name : _city.Text.Trim();
        }

        private void Commit()
        {
            Color c;
            if (SelectedZone == null)
                Refuse("Pick a time zone - typing a city from the list fills it in for you.", _zone);
            else if (!ColourCode.TryParse(_hex.Text, out c))
                Refuse("That hex code is not a colour. It should look like #3B8EEA.", _hex);
            else if (_label.Text.Trim().Length == 0 && _city.Text.Trim().Length == 0)
                Refuse("Give the clock a label or a city, so you can tell it apart.", _label);
            else
            {
                Result = new ClockEntry
                {
                    Label  = Settings.Clean(_label.Text),
                    City   = Settings.Clean(CityName()),
                    ZoneId = SelectedZone.Id,
                    Colour = c
                };
                DialogResult = DialogResult.OK;
            }
        }

        private void Refuse(string message, Control focus)
        {
            MessageBox.Show(this, message, "Taskbar Clocks", MessageBoxButtons.OK, MessageBoxIcon.Information);
            focus.Focus();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tick.Dispose();
                _previewLabelFont.Dispose();
                _previewTimeFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
