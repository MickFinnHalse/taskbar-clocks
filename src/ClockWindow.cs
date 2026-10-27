using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;

namespace TaskbarClocks
{
    public sealed class ClockWindow : Form
    {
        private readonly Settings _cfg;
        private readonly Timer    _tick;
        private readonly ToolTip  _tip = new ToolTip { InitialDelay = 400, ReshowDelay = 100 };

        private readonly List<TimeZoneInfo> _zones = new List<TimeZoneInfo>();   // parallel to _cfg.Clocks; null if missing
        private string[]    _times     = new string[0];
        private string[]    _offsets   = new string[0];   // " (+6)" after each time, or ""
        private bool        _offsetsChanged;
        private TimeZoneInfo _home;                        // where the user is
        private Rectangle[] _cells     = new Rectangle[0];
        private int[]       _cellTimeX = new int[0];   // where each clock's time starts
        private bool        _meetInMiddle;              // 2 x 2: left pair right-aligned, right pair left-aligned
        private Font        _font;
        private Bitmap      _frame;                    // the last thing drawn, painted as is
        private float       _fittedSize;               // the size actually in use after shrinking
        private int         _maxHeight;

        private int  _housekeeping;
        private int  _zoneRefresh;
        private int  _hover = -1;

        private Point _dragOrigin;
        private bool  _pressed;
        private bool  _dragging;

        private const int   Gap      = 8;     // px between our right edge and the tray
        private const int   PadX     = 8;
        private const int   PadY     = 2;
        private const int   LabelGap = 4;     // between the end of a label and its time
        private const int   ColGap   = 14;    // between neighbouring clocks
        private const int   MaxLabel = 160;   // longer labels are cut with an ellipsis
        private const float MinFont  = 6.5f;

        public static readonly float[] TextSizes = { 8f, 9f, 10f, 11f, 12f, 13f, 14f, 16f };

        // Typographic format measures the ink and nothing else, so a label's measured
        // width ends where its last letter ends and LabelGap is the whole gap.
        private static readonly StringFormat Typo = MakeTypo();

        private static StringFormat MakeTypo()
        {
            var f = (StringFormat)StringFormat.GenericTypographic.Clone();
            f.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;
            f.Trimming = StringTrimming.EllipsisCharacter;
            return f;
        }

        public ClockWindow(Settings cfg, System.Threading.WaitHandle showRequests)
        {
            _cfg = cfg;

            // Signalled by a second launch. The wait runs on a pool thread, so the work
            // is passed back to the UI thread.
            System.Threading.ThreadPool.RegisterWaitForSingleObject(showRequests, delegate
            {
                if (IsHandleCreated) BeginInvoke((MethodInvoker)ComeForward);
            }, null, System.Threading.Timeout.Infinite, false);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar   = false;     // no taskbar button of its own
            TopMost         = true;
            StartPosition   = FormStartPosition.Manual;
            DoubleBuffered  = true;

            ResolveZones();
            UpdateTimes();

            ContextMenuStrip = new ContextMenuStrip { ShowImageMargin = false, ShowCheckMargin = true };
            ContextMenuStrip.Opening += OnMenuOpening;

            // Clocks only change once a minute, but a one-second tick means the minute
            // turns over on the taskbar within a second of it turning over in the city.
            _tick = new Timer { Interval = 1000 };
            _tick.Tick += OnTick;

            MouseDown  += OnMouseDown;
            MouseMove  += OnMouseMove;
            MouseUp    += OnMouseUp;
            // Clear the text as well as the index, or coming back in over a gap would pop
            // up the last clock's details, frozen at the time they were set.
            MouseLeave += delegate { _hover = -1; RefreshTip(); };

            HookSystemEvents();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Relayout();
            if (!_cfg.AutoPosition) RestoreManualPosition();
            _tick.Start();
        }

        /// <summary>
        /// Puts a dragged window back where it was left. A spot that is no longer on any
        /// screen (a monitor unplugged, the resolution changed) returns it to the tray.
        /// </summary>
        private void RestoreManualPosition()
        {
            var spot = new Rectangle(_cfg.ManualX, _cfg.ManualY, Width, Height);
            foreach (Screen s in Screen.AllScreens)
                if (s.Bounds.IntersectsWith(spot)) { Location = spot.Location; return; }
            _cfg.AutoPosition = true;
            Reposition();
        }

        /// <summary>A second launch: back on screen if it has strayed off, and on top.</summary>
        private void ComeForward()
        {
            if (_cfg.AutoPosition) Reposition();
            else RestoreManualPosition();
            Native.BringToTop(Handle);
        }

        // Keep the window out of Alt-Tab. Deliberately NOT WS_EX_NOACTIVATE: a window
        // that never activates cannot host a context menu that dismisses properly.
        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_TOOLWINDOW = 0x00000080;
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        // ---------- time ----------

        /// <summary>
        /// Looks each clock's zone up afresh. TimeZoneInfo caches what it reads, so the
        /// cache is cleared first - otherwise a daylight-saving rule that Windows Update
        /// delivers while the clocks are running would not be seen until a restart.
        /// </summary>
        private void ResolveZones()
        {
            TimeZoneInfo.ClearCachedData();
            _zones.Clear();
            foreach (ClockEntry c in _cfg.Clocks)
                _zones.Add(FindZone(c.ZoneId));

            // Clearing the cache also refreshes TimeZoneInfo.Local, so following the PC
            // picks up a change to the PC's own time zone here too.
            _home = FindZone(_cfg.HomeZoneId) ?? TimeZoneInfo.Local;
        }

        private static TimeZoneInfo FindZone(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { return null; }
        }

        private string TimeFormat
        {
            get { return _cfg.Use24Hour ? "HH:mm" : "h:mm tt"; }
        }

        /// <summary>
        /// Returns true when any displayed time or difference changed. A changed
        /// difference also sets _offsetsChanged, because "(+10)" is wider than "(+9)"
        /// and the window has to be measured again rather than just redrawn.
        /// </summary>
        private bool UpdateTimes()
        {
            DateTime utc = DateTime.UtcNow;
            int n = _cfg.Clocks.Count;
            var times   = new string[n];
            var offsets = new string[n];
            bool changed = n != _times.Length || n != _offsets.Length;
            for (int i = 0; i < n; i++)
            {
                TimeZoneInfo tz = _zones[i];
                times[i]   = tz == null ? "--:--"
                           : TimeZoneInfo.ConvertTimeFromUtc(utc, tz).ToString(TimeFormat, CultureInfo.InvariantCulture);
                offsets[i] = tz == null ? "" : OffsetText(tz.GetUtcOffset(utc) - _home.GetUtcOffset(utc));
                if (!changed && times[i] != _times[i]) changed = true;
                if (i < _offsets.Length && offsets[i] != _offsets[i]) _offsetsChanged = true;
            }
            if (n != _offsets.Length) _offsetsChanged = true;
            if (_offsetsChanged) changed = true;
            _times   = times;
            _offsets = offsets;
            return changed;
        }

        /// <summary>" (+6)", " (-1)", " (+5:30)"; nothing when the clock keeps the user's own time.</summary>
        public static string OffsetText(TimeSpan diff)
        {
            if (diff == TimeSpan.Zero) return "";
            string sign = diff < TimeSpan.Zero ? "-" : "+";
            TimeSpan d = diff.Duration();
            string amount = d.Minutes == 0
                ? ((int)d.TotalHours).ToString(CultureInfo.InvariantCulture)
                : string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", (int)d.TotalHours, d.Minutes);
            return " (" + sign + amount + ")";
        }

        /// <summary>Brings the display up to date: redrawn, or re-measured if a difference changed.</summary>
        private void RefreshTimes()
        {
            if (!UpdateTimes()) return;
            if (_offsetsChanged) Relayout();
            else Render();
            if (_hover >= 0) RefreshTip();
        }

        private void OnTick(object sender, EventArgs e)
        {
            RefreshTimes();

            // Every 2s: the taskbar may have moved or changed height, and z-order among
            // topmost windows drifts.
            if (++_housekeeping >= 2)
            {
                _housekeeping = 0;
                Native.BringToTop(Handle);
                if (MaxHeight() != _maxHeight) Relayout();
                else if (_cfg.AutoPosition && !_dragging) Reposition();
            }

            // Hourly: pick up any time zone rule changes Windows Update has delivered.
            if (++_zoneRefresh >= 3600)
            {
                _zoneRefresh = 0;
                ResolveZones();
                RefreshTimes();
            }
        }

        // ---------- geometry ----------

        /// <summary>
        /// Row: every clock side by side. TwoRows: two on top and the rest below, so two
        /// clocks stack one above the other and three or four fill a 2 x 2 grid.
        /// Auto: two rows once all four slots are in use, one row before that.
        /// </summary>
        private void GridShape(int n, out int rows, out int cols)
        {
            bool twoRows = _cfg.Layout == ClockLayout.TwoRows
                        || (_cfg.Layout == ClockLayout.Auto && n >= Settings.MaxClocks);
            if (n <= 1)        { rows = 1; cols = 1; }
            else if (!twoRows) { rows = 1; cols = n; }
            else               { rows = 2; cols = (n + 1) / 2; }
        }

        /// <summary>The tallest the window may be: the height of the taskbar.</summary>
        private static int MaxHeight()
        {
            Rectangle bar = Native.GetTaskbarRect();
            if (!bar.IsEmpty && bar.Height < bar.Width) return bar.Height;
            Screen p = Screen.PrimaryScreen;
            int reserved = p.Bounds.Bottom - p.WorkingArea.Bottom;
            return reserved > 0 ? reserved : 48;
        }

        /// <summary>A 96 dpi surface, so measuring and drawing always agree.</summary>
        private static Bitmap Surface(int w, int h)
        {
            var bmp = new Bitmap(Math.Max(1, w), Math.Max(1, h), PixelFormat.Format32bppArgb);
            bmp.SetResolution(96f, 96f);
            return bmp;
        }

        private static int TextWidth(Graphics g, string s, Font f)
        {
            return (int)Math.Ceiling(g.MeasureString(s, f, PointF.Empty, Typo).Width);
        }

        /// <summary>
        /// Picks the largest font, up to FontSize, at which every row fits inside the
        /// taskbar's height, then measures the cells and sizes the window around them.
        /// </summary>
        private void Relayout()
        {
            _offsetsChanged = false;
            int n = _cfg.Clocks.Count;
            int rows, cols;
            GridShape(n, out rows, out cols);
            _maxHeight = MaxHeight();

            using (Bitmap probe = Surface(1, 1))
            using (Graphics g = Graphics.FromImage(probe))
            {
                float size = _cfg.FontSize;
                int lineH;
                while (true)
                {
                    if (_font != null) _font.Dispose();
                    _font = new Font("Segoe UI", size, FontStyle.Regular, GraphicsUnit.Point);
                    lineH = (int)Math.Ceiling(_font.GetHeight(g));
                    if (rows * lineH + PadY * 2 <= _maxHeight || size <= MinFont) break;
                    size -= 0.5f;
                }
                _fittedSize = size;

                if (n == 0)
                {
                    _meetInMiddle = false;
                    _cells = new Rectangle[0];
                    _cellTimeX = new int[0];
                    ClientSize = new Size(TextWidth(g, PlaceholderText, _font) + PadX * 2, lineH + PadY * 2 + 6);
                }
                else
                {
                    // Each clock's time is measured as the widest time this format can show
                    // plus its own " (+6)", so nothing jitters sideways as the minutes tick
                    // over. Differences only change with daylight saving or a new home city,
                    // and those trigger a fresh layout.
                    string widestTime = _cfg.Use24Hour ? "00:00" : "00:00 PM";

                    // Each column is as wide as its widest label. Labels are right-aligned
                    // in it, so every label ends LabelGap before its time and the times
                    // still line up down the column.
                    var labelW = new int[cols];
                    var timeW  = new int[cols];
                    for (int i = 0; i < n; i++)
                    {
                        int c = i % cols;
                        labelW[c] = Math.Max(labelW[c], Math.Min(TextWidth(g, _cfg.Clocks[i].Caption, _font), MaxLabel));
                        timeW[c]  = Math.Max(timeW[c], TextWidth(g, widestTime + _offsets[i], _font));
                    }

                    var colX = new int[cols];
                    int x = PadX;
                    for (int c = 0; c < cols; c++)
                    {
                        colX[c] = x;
                        x += labelW[c] + LabelGap + timeW[c] + ColGap;
                    }

                    _cells     = new Rectangle[n];
                    _cellTimeX = new int[n];
                    for (int i = 0; i < n; i++)
                    {
                        int r = i / cols, c = i % cols;
                        _cells[i]     = new Rectangle(colX[c], PadY + r * lineH, labelW[c] + LabelGap + timeW[c], lineH);
                        _cellTimeX[i] = colX[c] + labelW[c] + LabelGap;
                    }
                    ClientSize = new Size(x - ColGap + PadX, rows * lineH + PadY * 2);
                    _meetInMiddle = rows == 2 && cols == 2;
                }
            }

            if (_cfg.AutoPosition && !_dragging) Reposition();
            Render();
        }

        private void Reposition()
        {
            Rectangle tray = Native.GetTrayRect();
            Rectangle bar  = Native.GetTaskbarRect();
            if (tray.IsEmpty || bar.IsEmpty)
            {
                // Shell layout not recognised; fall back to the saved manual spot.
                if (_cfg.ManualX >= 0 && _cfg.ManualY >= 0)
                    Location = new Point(_cfg.ManualX, _cfg.ManualY);
                return;
            }
            int x = tray.Left - Width - Gap;
            int y = bar.Top + (bar.Height - Height) / 2;
            Location = new Point(x, y);
        }

        // ---------- drawing ----------

        private const string PlaceholderText = "+ Add a clock";

        /// <summary>
        /// Draws the clocks onto the same 96 dpi surface they were measured on, so sizes
        /// match exactly, and keeps it for OnPaint. Painting straight onto the window would
        /// render the fonts at the monitor's dpi and outgrow the measured cells.
        /// </summary>
        private void Render()
        {
            if (_font == null) return;

            Bitmap bmp = Surface(ClientSize.Width, ClientSize.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Theme.SolidBack(_cfg.Background));
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                if (_cfg.Clocks.Count == 0)
                {
                    using (var b = new SolidBrush(Theme.PlaceholderOn(_cfg.Background)))
                        g.DrawString(PlaceholderText, _font, b, PadX, PadY + 3, Typo);
                }

                for (int i = 0; i < _cells.Length && i < _cfg.Clocks.Count; i++)
                {
                    ClockEntry c = _cfg.Clocks[i];
                    Rectangle r = _cells[i];
                    using (var b = new SolidBrush(c.Colour))
                    {
                        string time = _times[i] + _offsets[i];
                        int w = Math.Min(TextWidth(g, c.Caption, _font), MaxLabel);
                        int labelX, timeX;
                        if (_meetInMiddle)
                        {
                            // Two by two: the left-hand clocks hug the middle from the left
                            // and the right-hand ones from the right, so both pairs meet
                            // in a neat line down the centre.
                            int block = w + LabelGap + TextWidth(g, time, _font);
                            labelX = i % 2 == 0 ? r.Right - block : r.X;
                            timeX  = labelX + w + LabelGap;
                        }
                        else
                        {
                            timeX  = _cellTimeX[i];
                            labelX = timeX - LabelGap - w;
                        }
                        g.DrawString(c.Caption, _font, b, new RectangleF(labelX, r.Y, w + 1, r.Height), Typo);
                        g.DrawString(time, _font, b, timeX, r.Y, Typo);
                    }
                }
            }

            if (_frame != null) _frame.Dispose();
            _frame = bmp;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // An explicit pixel rectangle: "unscaled" drawing still scales a 96 dpi bitmap
            // up to the monitor's dpi, which cut off the right and bottom edges.
            if (_frame != null)
                e.Graphics.DrawImage(_frame, new Rectangle(0, 0, _frame.Width, _frame.Height),
                                     0, 0, _frame.Width, _frame.Height, GraphicsUnit.Pixel);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // The frame covers the whole window; skipping the erase stops a flicker.
        }

        // ---------- hover details ----------

        private int CellAt(Point p)
        {
            for (int i = 0; i < _cells.Length; i++)
            {
                Rectangle r = _cells[i];
                r.Inflate(ColGap / 2, PadY);
                if (r.Contains(p)) return i;
            }
            return -1;
        }

        private void RefreshTip()
        {
            _tip.SetToolTip(this, _hover >= 0 && _hover < _cfg.Clocks.Count ? Describe(_hover) : null);
        }

        /// <summary>"Anna - Sydney / Wed 28 Oct, 08:14 / Daylight saving on (UTC+11:00)"</summary>
        private string Describe(int i)
        {
            ClockEntry c = _cfg.Clocks[i];
            TimeZoneInfo tz = _zones[i];
            string head = string.IsNullOrEmpty(c.City) || c.Caption == c.City
                        ? c.Caption : c.Label + " - " + c.City;
            if (tz == null)
                return head + "\r\nTime zone \"" + c.ZoneId + "\" is not on this PC.\r\nRight-click to change it.";

            DateTime utc   = DateTime.UtcNow;
            DateTime there = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
            DateTime here  = TimeZoneInfo.ConvertTimeFromUtc(utc, _home);   // the user's date, not the PC's
            string day = there.Date > here.Date ? " (tomorrow for you)"
                       : there.Date < here.Date ? " (yesterday for you)" : "";
            return head + "\r\n"
                 + there.ToString("ddd d MMM", CultureInfo.CurrentCulture) + ", "
                 + there.ToString(TimeFormat, CultureInfo.InvariantCulture) + day + "\r\n"
                 + DaylightSaving.Describe(tz, utc) + "\r\n"
                 + Relative(tz.GetUtcOffset(utc) - _home.GetUtcOffset(utc));
        }

        /// <summary>"6 hours ahead of you (Cape Town)", "Same time as you (this PC)".</summary>
        private string Relative(TimeSpan diff)
        {
            string where = " (" + HomeName() + ")";
            if (diff == TimeSpan.Zero) return "Same time as you" + where;
            TimeSpan d = diff.Duration();
            string amount = d.Minutes == 0
                ? (int)d.TotalHours + ((int)d.TotalHours == 1 ? " hour" : " hours")
                : string.Format(CultureInfo.InvariantCulture, "{0}h {1:00}m", (int)d.TotalHours, d.Minutes);
            return amount + (diff > TimeSpan.Zero ? " ahead of you" : " behind you") + where;
        }

        // ---------- input ----------

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            _dragOrigin = e.Location;
            _pressed    = true;
            _dragging   = false;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_pressed && e.Button == MouseButtons.Left)
            {
                if (!_dragging &&
                    (Math.Abs(e.X - _dragOrigin.X) > 4 || Math.Abs(e.Y - _dragOrigin.Y) > 4))
                {
                    _dragging = true;
                    _cfg.AutoPosition = false;   // dragging opts out of following the tray
                    _tip.Hide(this);
                }
                if (_dragging)
                    Location = new Point(Location.X + e.X - _dragOrigin.X,
                                         Location.Y + e.Y - _dragOrigin.Y);
                return;
            }

            int cell = CellAt(e.Location);
            if (cell != _hover)
            {
                _hover = cell;
                RefreshTip();
            }
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !_pressed) return;
            _pressed = false;
            if (_dragging) EndDrag();
            else if (_cfg.Clocks.Count == 0) AddClock();
        }

        /// <summary>
        /// The mouse can be taken away mid-drag (the Windows key, a UAC prompt, another
        /// window grabbing focus) and then no MouseUp ever comes. Finish the drag where it
        /// got to, or the window stays "dragging" and stops following the tray for good.
        /// Only a drag is finished here: an ordinary release can also lose capture just
        /// before MouseUp, and a plain click must still reach MouseUp to open "Add a clock".
        /// </summary>
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!_dragging) return;
            _pressed = false;
            EndDrag();
        }

        private void EndDrag()
        {
            _dragging    = false;
            _cfg.ManualX = Location.X;
            _cfg.ManualY = Location.Y;
            _cfg.Save();
        }

        // ---------- menu ----------

        // Rebuilt on every open, so it always lists the clocks as they are now.
        private void OnMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ToolStripItemCollection items = ContextMenuStrip.Items;
            items.Clear();

            for (int i = 0; i < _cfg.Clocks.Count; i++)
            {
                int idx = i;
                ClockEntry c = _cfg.Clocks[i];
                string title = c.Caption == c.City || string.IsNullOrEmpty(c.City)
                             ? c.Caption : c.Caption + "  (" + c.City + ")";
                var sub = new ToolStripMenuItem(title.Replace("&", "&&"));
                sub.ForeColor = ReadableOnMenu(c.Colour);
                sub.DropDownItems.Add(new ToolStripMenuItem("Change...", null, delegate { EditClock(idx); }));
                sub.DropDownItems.Add(new ToolStripMenuItem("Move earlier", null, delegate { MoveClock(idx, -1); })
                                      { Enabled = idx > 0 });
                sub.DropDownItems.Add(new ToolStripMenuItem("Move later", null, delegate { MoveClock(idx, +1); })
                                      { Enabled = idx < _cfg.Clocks.Count - 1 });
                sub.DropDownItems.Add(new ToolStripSeparator());
                sub.DropDownItems.Add(new ToolStripMenuItem("Remove", null, delegate { RemoveClock(idx); }));
                items.Add(sub);
            }

            // Where the user is, which every (+6) is measured from.
            var home = new ToolStripMenuItem("Your time zone");
            home.DropDownItems.Add(new ToolStripMenuItem(
                "Follow this PC  (" + TimeZoneInfo.Local.DisplayName.Replace("&", "&&") + ")", null,
                delegate { SetHome("", ""); }) { Checked = string.IsNullOrEmpty(_cfg.HomeZoneId) });
            bool ownCity = !string.IsNullOrEmpty(_cfg.HomeZoneId);
            home.DropDownItems.Add(new ToolStripMenuItem(
                ownCity ? "Your city: " + HomeName().Replace("&", "&&") + "  (change...)" : "Set your city...", null,
                delegate { PickHome(); }) { Checked = ownCity });
            items.Add(home);
            items.Add(new ToolStripSeparator());

            bool full = _cfg.Clocks.Count >= Settings.MaxClocks;
            items.Add(new ToolStripMenuItem(full ? "Add a clock (4 is the most)" : "Add a clock...", null,
                                            delegate { AddClock(); }) { Enabled = !full });
            items.Add(new ToolStripSeparator());

            var layout = new ToolStripMenuItem("Layout");
            layout.DropDownItems.Add(new ToolStripMenuItem("Automatic (two rows at four clocks)", null,
                delegate { SetLayout(ClockLayout.Auto); }) { Checked = _cfg.Layout == ClockLayout.Auto });
            layout.DropDownItems.Add(new ToolStripMenuItem("All in one row", null,
                delegate { SetLayout(ClockLayout.Row); }) { Checked = _cfg.Layout == ClockLayout.Row });
            layout.DropDownItems.Add(new ToolStripMenuItem("Two rows", null,
                delegate { SetLayout(ClockLayout.TwoRows); }) { Checked = _cfg.Layout == ClockLayout.TwoRows });
            items.Add(layout);

            // A size too big for the taskbar is shrunk to fit; the tick stays on the size
            // that was picked, and the note says what is actually showing.
            var textSize = new ToolStripMenuItem("Text size");
            foreach (float s in TextSizes)
            {
                float size = s;
                bool chosen = Math.Abs(_cfg.FontSize - size) < 0.01f;
                string text = size.ToString("0", CultureInfo.InvariantCulture);
                if (chosen && _fittedSize < size - 0.01f)
                    text += "  (shrunk to " + _fittedSize.ToString("0.#", CultureInfo.InvariantCulture)
                          + " to fit the taskbar)";
                textSize.DropDownItems.Add(new ToolStripMenuItem(text, null, delegate
                {
                    _cfg.FontSize = size;
                    _cfg.Save();
                    Relayout();
                }) { Checked = chosen });
            }
            items.Add(textSize);

            var back = new ToolStripMenuItem("Background");
            string[]          backNames  = { "Grey", "White", "Black" };
            ClockBackground[] backValues = { ClockBackground.Grey, ClockBackground.White, ClockBackground.Black };
            for (int b = 0; b < backValues.Length; b++)
            {
                ClockBackground value = backValues[b];
                back.DropDownItems.Add(new ToolStripMenuItem(backNames[b], null, delegate
                {
                    _cfg.Background = value;
                    _cfg.Save();
                    Render();
                }) { Checked = _cfg.Background == value });
            }
            items.Add(back);

            items.Add(new ToolStripMenuItem("24-hour time", null, delegate
            {
                _cfg.Use24Hour = !_cfg.Use24Hour;
                _cfg.Save();
                UpdateTimes();
                Relayout();
            }) { Checked = _cfg.Use24Hour });
            items.Add(new ToolStripSeparator());

            items.Add(new ToolStripMenuItem("Snap back to the tray", null, delegate
            {
                _cfg.AutoPosition = true;
                _cfg.Save();
                Reposition();
            }) { Enabled = !_cfg.AutoPosition });
            items.Add(new ToolStripMenuItem("Start with Windows", null,
                delegate { Autostart.Set(!Autostart.IsEnabled); }) { Checked = Autostart.IsEnabled });
            items.Add(new ToolStripSeparator());
            items.Add(new ToolStripMenuItem("Exit", null, delegate { Close(); }));

            // A strip that was empty when the right-click arrived starts out cancelled.
            e.Cancel = false;
        }

        /// <summary>
        /// The menu has a light background, so a pale clock colour (white, light yellow)
        /// would vanish there; those fall back to the normal menu text colour.
        /// </summary>
        private static Color ReadableOnMenu(Color c)
        {
            double luminance = 0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B;
            return luminance > 170 ? SystemColors.MenuText : c;
        }

        private void SetLayout(ClockLayout layout)
        {
            _cfg.Layout = layout;
            _cfg.Save();
            Relayout();
        }

        private void AddClock()
        {
            if (_cfg.Clocks.Count >= Settings.MaxClocks) return;
            using (var dlg = new ClockDialog(null, NextFreeColour(), _cfg.Use24Hour, _cfg.Background))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _cfg.Clocks.Add(dlg.Result);
            }
            Committed();
        }

        private void EditClock(int i)
        {
            using (var dlg = new ClockDialog(_cfg.Clocks[i], _cfg.Clocks[i].Colour, _cfg.Use24Hour, _cfg.Background))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _cfg.Clocks[i] = dlg.Result;
            }
            Committed();
        }

        private void MoveClock(int i, int step)
        {
            int j = i + step;
            if (j < 0 || j >= _cfg.Clocks.Count) return;
            ClockEntry t = _cfg.Clocks[i];
            _cfg.Clocks[i] = _cfg.Clocks[j];
            _cfg.Clocks[j] = t;
            Committed();
        }

        private void RemoveClock(int i)
        {
            ClockEntry c = _cfg.Clocks[i];
            string who = string.IsNullOrEmpty(c.City) || c.Caption == c.City ? c.Caption
                       : c.Caption + " (" + c.City + ")";
            if (MessageBox.Show(this, "Remove the clock for " + who + "?", "Taskbar Clocks",
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _cfg.Clocks.RemoveAt(i);
            Committed();
        }

        private void PickHome()
        {
            var current = new ClockEntry
            {
                Label  = "",
                City   = _cfg.HomeCity,
                ZoneId = string.IsNullOrEmpty(_cfg.HomeZoneId) ? TimeZoneInfo.Local.Id : _cfg.HomeZoneId,
                Colour = Palette.StartingColours[0]
            };
            using (var dlg = new ClockDialog(current, current.Colour, _cfg.Use24Hour, _cfg.Background, true))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                SetHome(dlg.Result.City, dlg.Result.ZoneId);
            }
        }

        /// <summary>An empty zone means follow this PC. Every difference is worked out again.</summary>
        private void SetHome(string city, string zoneId)
        {
            _cfg.HomeCity   = city ?? "";
            _cfg.HomeZoneId = zoneId ?? "";
            Committed();
        }

        /// <summary>The user's city, or the zone's name when only a zone was chosen.</summary>
        private string HomeName()
        {
            if (!string.IsNullOrEmpty(_cfg.HomeCity)) return _cfg.HomeCity;
            return string.IsNullOrEmpty(_cfg.HomeZoneId) ? "this PC" : _home.StandardName;
        }

        private void Committed()
        {
            _cfg.Save();
            ResolveZones();
            UpdateTimes();
            Relayout();
        }

        /// <summary>The first of the starting colours that no clock is using yet.</summary>
        private Color NextFreeColour()
        {
            foreach (Color p in Palette.StartingColours)
            {
                bool used = false;
                foreach (ClockEntry c in _cfg.Clocks)
                    if (c.Colour.ToArgb() == p.ToArgb()) { used = true; break; }
                if (!used) return p;
            }
            return Palette.StartingColours[0];
        }

        // ---------- system ----------

        private void HookSystemEvents()
        {
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (s, e) =>
            {
                Theme.Refresh();
                Relayout();
            };
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                Theme.Refresh();
                Render();
            };

            // Fires when the PC's own clock or time zone changes - which, while following
            // the PC, also moves every (+6).
            Microsoft.Win32.SystemEvents.TimeChanged += (s, e) =>
            {
                ResolveZones();
                RefreshTimes();
            };
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _cfg.Save();
            base.OnFormClosing(e);
        }
    }

    public static class DaylightSaving
    {
        /// <summary>"Daylight saving on (UTC+11:00)", "No daylight saving (UTC+05:30)", ...</summary>
        public static string Describe(TimeZoneInfo tz, DateTime utc)
        {
            TimeSpan off = tz.GetUtcOffset(utc);
            string utcText = "UTC" + (off < TimeSpan.Zero ? "-" : "+") + off.Duration().ToString(@"hh\:mm");
            if (!UsesDaylightSavingThisYear(tz, utc)) return "No daylight saving (" + utcText + ")";
            // Asked of the UTC instant, not the local time there: in the hour the clocks go
            // back, the local time happens twice and the local overload answers "off" for
            // both, though the first is still daylight time.
            return (tz.IsDaylightSavingTime(utc) ? "Daylight saving on (" : "Daylight saving off (") + utcText + ")";
        }

        /// <summary>
        /// SupportsDaylightSavingTime is true for any zone that has EVER changed its
        /// clocks - Mexico City, say, which stopped in 2022. Asking whether the rule in
        /// force right now shifts the clock gives the answer people actually mean.
        /// </summary>
        public static bool UsesDaylightSavingThisYear(TimeZoneInfo tz, DateTime utc)
        {
            if (!tz.SupportsDaylightSavingTime) return false;
            DateTime there = TimeZoneInfo.ConvertTimeFromUtc(utc, tz).Date;
            foreach (TimeZoneInfo.AdjustmentRule rule in tz.GetAdjustmentRules())
                if (rule.DateStart <= there && there <= rule.DateEnd)
                    return rule.DaylightDelta != TimeSpan.Zero
                        && !rule.DaylightTransitionStart.Equals(rule.DaylightTransitionEnd);
            return false;
        }
    }
}
