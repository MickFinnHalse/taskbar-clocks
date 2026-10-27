using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TaskbarClocks
{
    /// <summary>
    /// Add or change one clock: a label, a city or town (which fills in the time zone),
    /// and a colour from the palette or typed as a hex code.
    ///
    /// Finding the time zone, in order: the built-in city list as you type; Search, which
    /// looks any town up online; "Same time zone as" a nearby listed city when the search
    /// comes up empty or there is no internet; and the time zone list itself.
    /// </summary>
    public sealed class ClockDialog : Form
    {
        private readonly TextBox  _label;
        private readonly TextBox  _city;
        private readonly Button   _search;
        private readonly Label    _status;
        private readonly ListBox  _results;
        private readonly Label    _sameAsCaption;
        private readonly TextBox  _sameAs;
        private readonly ComboBox _zone;
        private readonly Label    _now;
        private readonly TextBox  _hex;
        private readonly Panel    _preview;
        private readonly Button[] _swatches;
        private readonly Timer    _tick;
        private readonly bool     _use24Hour;
        private readonly Font     _previewFont = new Font("Segoe UI", 10f, FontStyle.Regular);

        private Color _colour;
        private bool  _settingHex;
        private bool  _settingCity;
        private Place _picked;       // the search result chosen, while the city box still shows it
        private bool  _zoneSettled;  // the zone was set for the city now showing, not left over
        private bool  _selectingZone;
        private int   _searchSeq;    // only the latest search's answer counts

        public ClockEntry Result { get; private set; }

        public ClockDialog(ClockEntry existing, Color colour, bool use24Hour, ClockBackground background)
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
            ClientSize      = new Size(440, 486);

            Controls.Add(Caption("Label  (e.g. the person's name)", 12));
            _label = new TextBox { Bounds = new Rectangle(12, 32, 416, 24), MaxLength = 40 };
            Controls.Add(_label);

            Controls.Add(Caption("City or town", 66));
            _city = new TextBox
            {
                Bounds             = new Rectangle(12, 86, 330, 24),
                AutoCompleteMode   = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.CustomSource,
                AutoCompleteCustomSource = BuiltInNames()
            };
            Controls.Add(_city);
            _search = new Button { Text = "Search", Bounds = new Rectangle(350, 85, 78, 26) };
            _search.Click += delegate { RunSearch(); };
            Controls.Add(_search);
            new ToolTip().SetToolTip(_search,
                "Looks the name up online with Open-Meteo's free place search, so small towns can be found.");

            // Two lines: the after-a-failed-search messages need the room, and on one line
            // the instruction to use the fallback below was cut off.
            _status = new Label { Bounds = new Rectangle(12, 114, 416, 34), ForeColor = SystemColors.GrayText };
            Controls.Add(_status);

            // One slot, two uses: the search results, or - when the search finds nothing -
            // the "Same time zone as" fallback.
            _results = new ListBox { Bounds = new Rectangle(12, 150, 416, 50), Visible = false, IntegralHeight = false };
            _results.SelectedIndexChanged += delegate { PickResult(); };
            Controls.Add(_results);

            _sameAsCaption = new Label
            {
                Text = "Same time zone as", Bounds = new Rectangle(12, 158, 120, 20), Visible = false
            };
            Controls.Add(_sameAsCaption);
            _sameAs = new TextBox
            {
                Bounds             = new Rectangle(134, 155, 294, 24),
                AutoCompleteMode   = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.CustomSource,
                AutoCompleteCustomSource = BuiltInNames(),
                Visible            = false
            };
            _sameAs.TextChanged += delegate { OnSameAsTyped(); };
            Controls.Add(_sameAs);

            Controls.Add(Caption("Time zone", 206));
            _zone = new ComboBox
            {
                Bounds        = new Rectangle(12, 226, 416, 24),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (TimeZoneInfo tz in TimeZoneInfo.GetSystemTimeZones()) _zone.Items.Add(tz);   // shows DisplayName
            Controls.Add(_zone);

            _now = new Label { Bounds = new Rectangle(12, 256, 416, 34) };
            Controls.Add(_now);

            Controls.Add(Caption("Colour", 300));
            _swatches = new Button[Palette.Colours.Length];
            for (int i = 0; i < Palette.Colours.Length; i++)
            {
                Color sw = Palette.Colours[i];
                int row = i / Palette.PerRow, col = i % Palette.PerRow;
                var b = new Button
                {
                    Bounds    = new Rectangle(12 + col * 32, 320 + row * 32, 26, 26),
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

            Controls.Add(new Label { Text = "or hex code", Bounds = new Rectangle(12, 394, 80, 20) });
            _hex = new TextBox { Bounds = new Rectangle(94, 391, 86, 24), MaxLength = 7 };
            _hex.TextChanged += delegate { OnHexTyped(); };
            Controls.Add(_hex);

            // How it will look, on the chosen background.
            _preview = new Panel
            {
                Bounds      = new Rectangle(196, 386, 232, 34),
                BackColor   = Theme.SolidBack(background),
                BorderStyle = BorderStyle.FixedSingle
            };
            _preview.Paint += OnPreviewPaint;
            Controls.Add(_preview);

            var ok = new Button
            {
                Text   = existing == null ? "Add" : "Save",
                Bounds = new Rectangle(272, 444, 74, 28)
            };
            var cancel = new Button
            {
                Text         = "Cancel",
                DialogResult = DialogResult.Cancel,
                Bounds       = new Rectangle(354, 444, 74, 28)
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
                _zoneSettled = true;
            }
            _city.TextChanged          += delegate { OnCityTyped(); };
            _label.TextChanged         += delegate { _preview.Invalidate(); };
            _zone.SelectedIndexChanged += delegate
            {
                if (!_selectingZone) _zoneSettled = true;   // picked from the list by hand
                ShowTimeThere();
                _preview.Invalidate();
            };

            // A saved clock keeps the zone it was saved with, which can differ from the
            // built-in city of the same name (a searched San Jose, California is not San
            // Jose, Costa Rica) - so don't claim the built-in match when it isn't in use.
            City match = Cities.Find(_city.Text);
            if (existing != null && match != null && match.ZoneId != existing.ZoneId)
                _status.Text = "Using the time zone saved with this clock.";
            else
                ShowBuiltInMatch(match);
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

        private static AutoCompleteStringCollection BuiltInNames()
        {
            var names = new AutoCompleteStringCollection();
            foreach (City c in Cities.All) names.Add(c.Display);
            return names;
        }

        /// <summary>
        /// Enter in the city box searches, rather than closing the dialog - unless the box
        /// already holds a built-in city or the search result just picked, when Enter means
        /// Add/Save as usual. While a search is out, Enter does nothing.
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter && _city.Focused)
            {
                if (!_search.Enabled) return true;
                if (_picked == null && Cities.Find(_city.Text) == null && _city.Text.Trim().Length > 0)
                {
                    RunSearch();
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ---------- city and zone ----------

        private void OnCityTyped()
        {
            if (_settingCity) return;
            _picked = null;
            ShowSlot(null);
            City c = Cities.Find(_city.Text);
            if (c != null) SelectZone(c.ZoneId);
            _zoneSettled = c != null;   // a new unrecognised name has no zone yet
            ShowBuiltInMatch(c);
            _preview.Invalidate();
        }

        private void ShowBuiltInMatch(City c)
        {
            if (_city.Text.Trim().Length == 0)
                _status.Text = "Start typing - the built-in list suggests cities as you go.";
            else if (c != null)
                _status.Text = "Found " + c.Display + ". Its time zone is filled in below.";
            else
                _status.Text = "Not in the built-in list. Press Search (or Enter) to look it up.";
        }

        private void RunSearch()
        {
            string name = _city.Text.Trim();
            if (name.Length == 0) return;
            int seq = ++_searchSeq;
            _search.Enabled = false;
            _status.Text = "Searching for \"" + name + "\"...";
            ShowSlot(null);

            PlaceLookup.Search(name, (places, error) =>
            {
                if (IsDisposed || seq != _searchSeq) return;
                _search.Enabled = true;
                if (_city.Text.Trim() != name) return;   // typed on while it was searching

                if (places.Count > 0)
                {
                    _results.Items.Clear();
                    foreach (Place p in places) _results.Items.Add(p);
                    ShowSlot(_results);
                    _status.Text = places.Count == 1
                                 ? "Found one place:"
                                 : "Found " + places.Count + " places. Pick the right one:";
                    if (places.Count == 1) _results.SelectedIndex = 0;
                    return;
                }

                _status.Text = (error ?? "Nothing called \"" + name + "\" was found online.")
                             + "\r\nType a nearby city in the same time zone:";
                ShowSlot(_sameAs);
                _sameAs.Focus();
            });
        }

        /// <summary>Shows the results list, the "same time zone as" row, or neither.</summary>
        private void ShowSlot(Control which)
        {
            _results.Visible       = which == _results;
            _sameAs.Visible        = which == _sameAs;
            _sameAsCaption.Visible = which == _sameAs;
        }

        private void PickResult()
        {
            Place p = _results.SelectedItem as Place;
            if (p == null) return;
            _picked = p;
            _settingCity = true;
            _city.Text = p.Name;
            _settingCity = false;
            SelectZone(p.ZoneId);
            _zoneSettled = true;
            _status.Text = "Using " + p.Display + ".";
            _preview.Invalidate();
        }

        private void OnSameAsTyped()
        {
            City c = Cities.Find(_sameAs.Text);
            if (c == null) return;
            SelectZone(c.ZoneId);
            _zoneSettled = true;
            _status.Text = "Using the same time zone as " + c.Display + ".";
        }

        private void SelectZone(string id)
        {
            _selectingZone = true;
            for (int i = 0; i < _zone.Items.Count; i++)
                if (((TimeZoneInfo)_zone.Items[i]).Id == id) { _zone.SelectedIndex = i; break; }
            _selectingZone = false;
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
            TextRenderer.DrawText(e.Graphics, caption + " " + time, _previewFont,
                                  new Rectangle(10, 0, _preview.ClientSize.Width - 20, _preview.ClientSize.Height),
                                  _colour, f);
        }

        // ---------- result ----------

        /// <summary>The place picked from a search, the bare name of a built-in city, or what was typed.</summary>
        private string CityName()
        {
            if (_picked != null) return _picked.Name;
            City c = Cities.Find(_city.Text);
            return c != null ? c.Name : _city.Text.Trim();
        }

        private void Commit()
        {
            Color c;
            if (SelectedZone == null)
                Refuse("Pick a time zone - choosing a city or town fills it in for you.", _zone);
            else if (_city.Text.Trim().Length > 0 && !_zoneSettled)
                // The zone showing belongs to whatever the box said before - saving it
                // would quietly give "Smalltown" Sydney's time.
                Refuse("Press Search to find \"" + _city.Text.Trim() + "\", or pick its time zone from the list.",
                       _search);
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
                _previewFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
