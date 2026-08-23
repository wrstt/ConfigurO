using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// The grouped tweak list.
    ///
    /// Rows are painted rather than built from child controls: at ~85 tweaks a
    /// control per row costs thousands of window handles and makes filtering
    /// visibly slow. The toggle pill is drawn inline and hit-tested, with a
    /// short slide animation on the row that changed so it still reads as a
    /// switch rather than a checkbox.
    /// </summary>
    internal sealed class NTweakList : NControl
    {
        sealed class Row
        {
            internal TweakDef Def;
            internal bool On;
            internal int Y, Height;
            internal float Anim;      // 0 = off, 1 = on
        }

        sealed class Group
        {
            internal string Title;
            internal int Y, Height;
            /// <summary>Bounds of the panel the group's rows sit in.</summary>
            internal int CardTop, CardBottom;
            internal readonly List<Row> Rows = new List<Row>();
        }

        readonly List<Group> _groups = new List<Group>();
        readonly Timer _animation = new Timer { Interval = 15 };
        readonly List<Row> _animating = new List<Row>();

        string _query = string.Empty;
        Row _hover;
        bool _showTips = true;

        internal NTweakList()
        {
            _animation.Tick += Animate;
        }

        internal event EventHandler<TweakDef> Toggled;

        /// <summary>Hide the tip line under each row (Settings > "Show help messages").</summary>
        internal bool ShowTips
        {
            get { return _showTips; }
            set { if (_showTips == value) return; _showTips = value; Rebuild(); }
        }

        internal string Query
        {
            get { return _query; }
            set
            {
                string v = (value ?? string.Empty).Trim();
                if (_query == v) return;
                _query = v;
                Rebuild();
            }
        }

        // Rhythm.
        //
        // A row is a name over a note, and the pair has to read as one object
        // with air around it rather than as two lines pushed together. At 54
        // the pair filled 36 of the row's 54 pixels, leaving 9 above and below
        // -- less separation between a row and its neighbour than between the
        // two lines inside it, which is what made the screen feel crammed. 64
        // puts 13 above and 12 below, so the gap between rows is wider than the
        // gap within one and the eye groups them correctly.
        int GroupHeaderHeight { get { return NocturneScale.S(40); } }
        int RowHeight { get { return NocturneScale.S(_showTips ? 64 : 46); } }
        int NameTop { get { return NocturneScale.S(13); } }
        int NoteTop { get { return NocturneScale.S(34); } }

        /// <summary>Text inset. 12 sat the labels almost on the pane's edge.</summary>
        int SidePad { get { return NocturneScale.S(20); } }

        /// <summary>Air between one group and the next section label.</summary>
        int GroupGap { get { return NocturneScale.S(32); } }

        /// <summary>Vertical gap between one row's card and the next.</summary>
        int CardGap { get { return NocturneScale.S(6); } }

        /// <summary>Reloads from <see cref="TweakRegistry"/> and re-applies the filter.</summary>
        internal void Load()
        {
            _groups.Clear();
            Options o = OptionsHelper.CurrentOptions;
            foreach (IGrouping<TweakGroup, TweakDef> g in TweakRegistry.Available())
            {
                Group group = new Group { Title = TweakRegistry.GroupTitle(g.Key) };
                foreach (TweakDef d in g)
                {
                    bool on = d.Get(o);
                    group.Rows.Add(new Row { Def = d, On = on, Anim = on ? 1f : 0f });
                }
                _groups.Add(group);
            }
            Rebuild();
        }

        bool Matches(Row r)
        {
            if (_query.Length == 0) return true;
            return r.Def.ResolvedLabel.IndexOf(_query, StringComparison.OrdinalIgnoreCase) >= 0
                || r.Def.ResolvedSummary.IndexOf(_query, StringComparison.OrdinalIgnoreCase) >= 0
                // The long-form text is searched too: it carries the detail
                // someone would actually recall a tweak by.
                || r.Def.ResolvedDetail.IndexOf(_query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Recomputes row positions and the control's total height.</summary>
        internal void Rebuild()
        {
            int y = 0;
            foreach (Group g in _groups)
            {
                List<Row> visible = g.Rows.Where(Matches).ToList();
                if (visible.Count == 0) { g.Height = 0; continue; }

                g.Y = y;
                y += GroupHeaderHeight;

                foreach (Row r in g.Rows)
                {
                    if (!Matches(r)) { r.Height = 0; continue; }
                    r.Y = y;
                    r.Height = RowHeight;
                    y += RowHeight;
                }
                g.Height = y - g.Y;
                y += GroupGap;
            }
            Height = Math.Max(y, 1);
            Invalidate();
        }

        internal int VisibleCount
        {
            get { return _groups.Sum(g => g.Rows.Count(Matches)); }
        }

        internal int TotalCount { get { return _groups.Sum(g => g.Rows.Count); } }

        internal int AppliedCount { get { return _groups.Sum(g => g.Rows.Count(r => r.On)); } }

        // ── Long-form help on hover ─────────────────────────────────────
        //
        // The row shows a one-line summary; the translated long-form text lives
        // here, where there is room for its paragraphs and bullet lists. Drawn
        // rather than left to the shell: a default tooltip is a light-grey
        // system rectangle, which on a dark themed window looks like a defect.

        readonly ToolTip _tips = new ToolTip
        {
            OwnerDraw = true,
            InitialDelay = 450,
            ReshowDelay = 120,
            AutoPopDelay = 32000,
            UseFading = false,
            UseAnimation = false
        };

        bool _tipsWired;

        int TipPadX { get { return NocturneScale.S(12); } }
        int TipPadY { get { return NocturneScale.S(10); } }
        int TipMaxWidth { get { return NocturneScale.S(420); } }

        void UpdateTip(Row r)
        {
            if (!_tipsWired)
            {
                _tips.Popup += OnTipPopup;
                _tips.Draw += OnTipDraw;
                _tipsWired = true;
            }
            _tips.SetToolTip(this, r != null && r.Def.HasDetail ? r.Def.ResolvedDetail : null);
        }

        void OnTipPopup(object sender, PopupEventArgs e)
        {
            string text = _tips.GetToolTip(this);
            if (string.IsNullOrEmpty(text)) { e.Cancel = true; return; }

            using (Graphics g = NocturneDraw.CreateMeasureGraphics())
            using (Font f = NocturneFonts.Tip())
            {
                SizeF s = g.MeasureString(text, f, TipMaxWidth - TipPadX * 2);
                e.ToolTipSize = new Size((int)Math.Ceiling(s.Width) + TipPadX * 2,
                                         (int)Math.Ceiling(s.Height) + TipPadY * 2);
            }
        }

        void OnTipDraw(object sender, DrawToolTipEventArgs e)
        {
            Graphics g = e.Graphics;
            NocturneDraw.Prepare(g);

            Rectangle r = new Rectangle(0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
            NocturneTheme.FillRounded(g, r, NocturneTheme.RadiusMd, NocturneTheme.SurfaceAlt);
            NocturneTheme.DrawRounded(g, r, NocturneTheme.RadiusMd, NocturneTheme.Border);

            using (Font f = NocturneFonts.Tip())
            using (SolidBrush b = new SolidBrush(NocturneTheme.Text))
            using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic))
            {
                // Wrapping is the point here, so NoWrap stays off -- but
                // LineLimit has to go or the last line is dropped whenever it
                // does not fit the box whole.
                sf.FormatFlags &= ~StringFormatFlags.LineLimit;
                sf.Trimming = StringTrimming.None;
                g.DrawString(e.ToolTipText, f, b,
                    new RectangleF(TipPadX, TipPadY,
                                   e.Bounds.Width - TipPadX * 2,
                                   e.Bounds.Height - TipPadY * 2), sf);
            }
        }

        Row RowAt(Point p)
        {
            foreach (Group g in _groups)
            {
                if (g.Height == 0) continue;
                if (p.Y < g.Y || p.Y >= g.Y + g.Height) continue;
                foreach (Row r in g.Rows)
                {
                    if (r.Height == 0) continue;
                    if (p.Y >= r.Y && p.Y < r.Y + r.Height) return r;
                }
            }
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Row r = RowAt(e.Location);
            if (r != _hover)
            {
                _hover = r;
                UpdateTip(r);
                Cursor = r != null ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = null;
            UpdateTip(null);
            Cursor = Cursors.Default;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            Row r = RowAt(e.Location);
            if (r != null) Toggle(r);
            base.OnMouseClick(e);
        }

        void Toggle(Row r)
        {
            TweakDef d = r.Def;

            if (!r.On && !string.IsNullOrEmpty(d.ConfirmKey))
            {
                string prompt = I18n.Get(d.ConfirmKey,
                    "This change is hard to undo. Continue?");
                if (!HelperForm.Confirm(FindForm(), prompt))
                    return;
            }

            r.On = !r.On;
            TweakRunner.Set(d, r.On);

            if (!_animating.Contains(r)) _animating.Add(r);
            _animation.Start();

            EventHandler<TweakDef> h = Toggled;
            if (h != null) h(this, d);
        }

        void Animate(object sender, EventArgs e)
        {
            bool busy = false;
            foreach (Row r in _animating.ToList())
            {
                float target = r.On ? 1f : 0f;
                float step = 0.14f;
                if (Math.Abs(r.Anim - target) <= step) { r.Anim = target; _animating.Remove(r); }
                else { r.Anim += r.Anim < target ? step : -step; busy = true; }
            }
            if (!busy && _animating.Count == 0) _animation.Stop();
            Invalidate();
        }

        protected override void OnScaleChanged() { Rebuild(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Render(e);
            // Raise Paint last so anything attached to it draws on top.
            // Skipping this silently disables every `x.Paint += ...` handler.
            base.OnPaint(e);
        }

        void Render(PaintEventArgs e)
{
            Graphics g = e.Graphics;
            NocturneDraw.Prepare(g);

            Rectangle clip = e.ClipRectangle;
            int tw = NocturneScale.S(NocturneTheme.ToggleWidth);
            int th = NocturneScale.S(NocturneTheme.ToggleHeight);

            using (Font section = NocturneFonts.SectionLabel())
            using (Font name = NocturneFonts.RowName())
            using (Font tip = NocturneFonts.Tip())
            using (Font onLabel = NocturneFonts.Tip())
            {
                foreach (Group grp in _groups)
                {
                    if (grp.Height == 0) continue;
                    if (grp.Y + grp.Height < clip.Top || grp.Y > clip.Bottom) continue;

                    NocturneDraw.SectionLabel(g, grp.Title, section, SidePad,
                        grp.Y + NocturneScale.S(12), NocturneScale.S(18));



                    foreach (Row r in grp.Rows)
                    {
                        if (r.Height == 0) continue;
                        if (r.Y + r.Height < clip.Top || r.Y > clip.Bottom) continue;

                        // Every row is its own card.
                        //
                        // A row is a name on the left and a toggle against the
                        // right edge, and on a wide window several hundred
                        // pixels separate them. One panel behind the whole group
                        // does not fix that: it gives the section a surface but
                        // leaves each individual pair looking unrelated, so the
                        // span still reads as a gap. Giving the row itself a
                        // card makes the span belong to one object, and the
                        // toggle plainly belongs to the name at the other end of
                        // it. This is what Windows' own settings do, for exactly
                        // this reason.
                        Rectangle card = new Rectangle(
                            0, r.Y, Width, Math.Max(0, r.Height - CardGap));

                        NocturneTheme.FillRounded(g, card, NocturneTheme.RadiusMd,
                            r == _hover ? NocturneTheme.SurfaceAlt : NocturneTheme.Surface);
                        NocturneTheme.DrawRounded(g, card, NocturneTheme.RadiusMd,
                            NocturneTheme.Border);

                        int toggleX = Width - SidePad - tw;
                        int textRight = toggleX - NocturneScale.S(12);

                        // "On" caption, only while the tweak is applied
                        if (r.Anim > 0.5f)
                        {
                            string on = I18n.Get("toggleOn", "On");
                            float w = NocturneDraw.Width(g, on, onLabel);
                            NocturneDraw.Text(g, on, onLabel, NocturneTheme.AccentText,
                                new RectangleF(textRight - w, r.Y, w, r.Height), NocturneDraw.Left);
                            textRight -= (int)w + NocturneScale.S(10);
                        }

                        int textW = Math.Max(0, textRight - SidePad);
                        if (_showTips)
                        {
                            NocturneDraw.Text(g, r.Def.ResolvedLabel, name, NocturneTheme.Text,
                                new RectangleF(SidePad, r.Y + NameTop, textW, NocturneScale.S(20)),
                                NocturneDraw.Left);
                            NocturneDraw.Text(g, r.Def.ResolvedSummary, tip, NocturneTheme.TextMuted,
                                new RectangleF(SidePad, r.Y + NoteTop, textW, NocturneScale.S(18)),
                                NocturneDraw.Left);
                        }
                        else
                        {
                            NocturneDraw.Text(g, r.Def.ResolvedLabel, name, NocturneTheme.Text,
                                new RectangleF(SidePad, r.Y, textW, r.Height), NocturneDraw.Left);
                        }

                        NocturneTogglePill.DrawAnimated(g,
                            new Rectangle(toggleX, r.Y + (r.Height - CardGap - th) / 2, tw - 1, th - 1),
                            r.Anim);

                        // No rule under the row. Eighty-four of them stacked is
                        // a page of horizontal lines, and the spacing above
                        // already separates one row from the next -- a divider
                        // as well is belt and braces, and it is what made the
                        // list look like a spreadsheet.

                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _animation.Dispose(); _tips.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
