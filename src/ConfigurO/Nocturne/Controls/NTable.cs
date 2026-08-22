using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>How a column's cell is rendered.</summary>
    internal enum NCellKind
    {
        Text,
        /// <summary>Monospace -- registry paths, IPs, file locations.</summary>
        Mono,
        /// <summary>Muted secondary text.</summary>
        Muted,
        /// <summary>Accent-tinted pill.</summary>
        Tag,
        /// <summary>Toggle pill, hit-testable.</summary>
        Toggle,
        /// <summary>Icon button, hit-testable.</summary>
        Action
    }

    internal sealed class NColumn
    {
        internal string Header;
        internal NCellKind Kind = NCellKind.Text;
        /// <summary>Fixed pixel width at 96 DPI, or 0 to share the remaining space.</summary>
        internal int Width;
        /// <summary>Relative share of the leftover width when <see cref="Width"/> is 0.</summary>
        internal float Weight = 1f;
        internal ContentAlignment Align = ContentAlignment.MiddleLeft;
        /// <summary>Icon name for <see cref="NCellKind.Action"/> columns.</summary>
        internal string Icon;
    }

    internal sealed class NRowEventArgs : EventArgs
    {
        internal int RowIndex;
        internal int ColumnIndex;
        internal NRowEventArgs(int row, int column) { RowIndex = row; ColumnIndex = column; }
    }

    /// <summary>
    /// The Nocturne table: 11px uppercase headers at 60% text, 1px row rules
    /// that fade at both ends, and a 4% text wash on hover. Rows are plain
    /// string arrays plus a per-row boolean for toggle columns, so screens can
    /// bind whatever shape their helper returns without an adapter.
    /// </summary>
    /// <summary>
    /// The table is always sized to its full content height; the screen's
    /// <see cref="NScrollPanel"/> does the scrolling, so there is no internal
    /// scroll offset to track.
    /// </summary>
    internal sealed class NTable : NControl
    {
        readonly List<string[]> _rows = new List<string[]>();
        readonly List<bool> _flags = new List<bool>();
        NColumn[] _columns = new NColumn[0];
        int _hoverRow = -1, _hoverCol = -1;

        internal NTable()
        {
            RowHeight = 34;
            HeaderHeight = 26;
        }

        /// <summary>Row height at 96 DPI.</summary>
        internal int RowHeight { get; set; }
        internal int HeaderHeight { get; set; }

        internal event EventHandler<NRowEventArgs> ToggleChanged;
        internal event EventHandler<NRowEventArgs> ActionClicked;

        /// <summary>
        /// Raised on right-click over a row. <see cref="ContextRow"/> holds the
        /// row index for the duration of the handler; screens use it to build a
        /// menu of per-row actions.
        /// </summary>
        internal event EventHandler<NRowEventArgs> RowContextMenu;

        /// <summary>Row the last right-click landed on, or -1.</summary>
        internal int ContextRow { get; private set; }

        internal int RowCount { get { return _rows.Count; } }

        internal void SetColumns(params NColumn[] columns)
        {
            _columns = columns ?? new NColumn[0];
            Invalidate();
        }

        internal void Clear()
        {
            _rows.Clear();
            _flags.Clear();
            Invalidate();
        }

        internal void AddRow(string[] cells, bool flag = false)
        {
            _rows.Add(cells);
            _flags.Add(flag);
            Invalidate();
        }

        internal string[] Row(int i) { return i >= 0 && i < _rows.Count ? _rows[i] : null; }
        internal bool Flag(int i) { return i >= 0 && i < _flags.Count && _flags[i]; }

        internal void SetFlag(int i, bool value)
        {
            if (i < 0 || i >= _flags.Count) return;
            _flags[i] = value;
            Invalidate();
        }

        internal void RemoveRow(int i)
        {
            if (i < 0 || i >= _rows.Count) return;
            _rows.RemoveAt(i);
            _flags.RemoveAt(i);
            Invalidate();
        }

        /// <summary>Total height the table would need without scrolling.</summary>
        internal int ContentHeight
        {
            get { return NocturneScale.S(HeaderHeight) + _rows.Count * NocturneScale.S(RowHeight); }
        }

        // ── Geometry ────────────────────────────────────────────────────
        int[] ColumnWidths()
        {
            int[] w = new int[_columns.Length];
            int fixedTotal = 0;
            float weightTotal = 0f;
            for (int i = 0; i < _columns.Length; i++)
            {
                if (_columns[i].Width > 0) { w[i] = NocturneScale.S(_columns[i].Width); fixedTotal += w[i]; }
                else weightTotal += _columns[i].Weight;
            }
            int free = Math.Max(0, Width - fixedTotal);
            for (int i = 0; i < _columns.Length; i++)
                if (_columns[i].Width <= 0)
                    w[i] = weightTotal > 0 ? (int)(free * (_columns[i].Weight / weightTotal)) : 0;
            return w;
        }

        int RowAt(int y)
        {
            int header = NocturneScale.S(HeaderHeight);
            if (y < header) return -1;
            int i = (y - header) / NocturneScale.S(RowHeight);
            return i >= 0 && i < _rows.Count ? i : -1;
        }

        int ColumnAt(int x)
        {
            int[] w = ColumnWidths();
            int acc = 0;
            for (int i = 0; i < w.Length; i++)
            {
                if (x >= acc && x < acc + w[i]) return i;
                acc += w[i];
            }
            return -1;
        }

        // ── Input ───────────────────────────────────────────────────────
        protected override void OnMouseMove(MouseEventArgs e)
        {
            int r = RowAt(e.Y), c = ColumnAt(e.X);
            if (r != _hoverRow || c != _hoverCol)
            {
                _hoverRow = r; _hoverCol = c;
                Cursor = Interactive(c) && r >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        bool Interactive(int c)
        {
            return c >= 0 && c < _columns.Length &&
                   (_columns[c].Kind == NCellKind.Toggle || _columns[c].Kind == NCellKind.Action);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoverRow = _hoverCol = -1;
            Cursor = Cursors.Default;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                ContextRow = RowAt(e.Y);
                if (ContextRow >= 0)
                {
                    // Keep the row highlighted while its menu is open.
                    _hoverRow = ContextRow;
                    Invalidate();
                    EventHandler<NRowEventArgs> h = RowContextMenu;
                    if (h != null) h(this, new NRowEventArgs(ContextRow, ColumnAt(e.X)));
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) { base.OnMouseClick(e); return; }
            int r = RowAt(e.Y), c = ColumnAt(e.X);
            if (r >= 0 && Interactive(c))
            {
                if (_columns[c].Kind == NCellKind.Toggle)
                {
                    _flags[r] = !_flags[r];
                    Invalidate();
                    EventHandler<NRowEventArgs> h = ToggleChanged;
                    if (h != null) h(this, new NRowEventArgs(r, c));
                }
                else
                {
                    EventHandler<NRowEventArgs> h = ActionClicked;
                    if (h != null) h(this, new NRowEventArgs(r, c));
                }
            }
            base.OnMouseClick(e);
        }

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

            int[] w = ColumnWidths();
            int header = NocturneScale.S(HeaderHeight);
            int rowH = NocturneScale.S(RowHeight);

            // ── header ──
            if (header > 0)
            using (Font f = NocturneFonts.TableHeader())
            {
                int x = 0;
                Color hc = NocturneTheme.Alpha(NocturneTheme.Text, 0.60);
                for (int i = 0; i < _columns.Length; i++)
                {
                    NocturneDraw.Text(g, (_columns[i].Header ?? string.Empty).ToUpperInvariant(), f, hc,
                        new RectangleF(x + NocturneScale.S(4), 0, Math.Max(0, w[i] - NocturneScale.S(8)), header),
                        FormatFor(_columns[i].Align));
                    x += w[i];
                }
            }
            if (header > 0) NocturneTheme.DrawFadedRule(g, 0, header - 1, Width, NocturneTheme.Border);

            // ── rows ──
            for (int r = 0; r < _rows.Count; r++)
            {
                int y = header + r * rowH;
                if (y + rowH < header || y > Height) continue;

                if (r == _hoverRow)
                {
                    using (SolidBrush b = new SolidBrush(NocturneTheme.Alpha(NocturneTheme.Text, 0.04)))
                        g.FillRectangle(b, 0, y, Width, rowH);
                }

                int x = 0;
                for (int c = 0; c < _columns.Length; c++)
                {
                    PaintCell(g, r, c, new Rectangle(x, y, w[c], rowH));
                    x += w[c];
                }

                if (r < _rows.Count - 1)
                    NocturneTheme.DrawFadedRule(g, 0, y + rowH - 1, Width, NocturneTheme.Border);
            }
        }

        void PaintCell(Graphics g, int r, int c, Rectangle cell)
        {
            NColumn col = _columns[c];
            string[] row = _rows[r];
            string value = c < row.Length ? row[c] : string.Empty;
            Rectangle inner = new Rectangle(cell.X + NocturneScale.S(4), cell.Y,
                                            Math.Max(0, cell.Width - NocturneScale.S(8)), cell.Height);

            switch (col.Kind)
            {
                case NCellKind.Toggle:
                {
                    int tw = NocturneScale.S(NocturneTheme.ToggleWidth);
                    int th = NocturneScale.S(NocturneTheme.ToggleHeight);
                    Rectangle pill = new Rectangle(inner.X, cell.Y + (cell.Height - th) / 2, tw - 1, th - 1);
                    NocturneTogglePill.Draw(g, pill, _flags[r]);
                    break;
                }
                case NCellKind.Action:
                {
                    int s = NocturneScale.S(16);
                    bool hot = r == _hoverRow && c == _hoverCol;
                    NocturneIcons.Draw(g, col.Icon ?? NocturneIcons.Trash,
                        inner.X, cell.Y + (cell.Height - s) / 2, s,
                        hot ? NocturneTheme.AccentText : NocturneTheme.TextDim);
                    break;
                }
                case NCellKind.Tag:
                {
                    if (string.IsNullOrEmpty(value)) break;
                    using (Font f = NocturneFonts.Tag())
                    {
                        int tw = (int)Math.Ceiling(NocturneDraw.Width(g, value, f)) + NocturneScale.S(14);
                        int th = NocturneScale.S(18);
                        Rectangle tag = new Rectangle(inner.X, cell.Y + (cell.Height - th) / 2, tw, th);
                        NocturneDraw.Card(g, tag, NocturneTheme.TagBg, Color.Empty, NocturneTheme.RadiusSm);
                        NocturneDraw.Text(g, value, f, NocturneTheme.AccentStrong, tag, NocturneDraw.Center);
                    }
                    break;
                }
                default:
                {
                    Font f = col.Kind == NCellKind.Mono ? NocturneFonts.Code() : NocturneFonts.Row();
                    Color color = col.Kind == NCellKind.Muted || col.Kind == NCellKind.Mono
                                  ? NocturneTheme.TextFaint : NocturneTheme.Text;
                    using (f) NocturneDraw.Text(g, value, f, color, inner, FormatFor(col.Align));
                    break;
                }
            }
        }

        static StringFormat FormatFor(ContentAlignment a)
        {
            if (a == ContentAlignment.MiddleRight) return NocturneDraw.Right;
            if (a == ContentAlignment.MiddleCenter) return NocturneDraw.Center;
            return NocturneDraw.Left;
        }
    }

    /// <summary>The toggle pill drawn inline by table cells, without a child control.</summary>
    internal static class NocturneTogglePill
    {
        internal static void Draw(Graphics g, Rectangle pill, bool on)
        {
            DrawAnimated(g, pill, on ? 1f : 0f);
        }

        /// <summary>
        /// Interpolated form used by <see cref="NTweakList"/>, where
        /// <paramref name="t"/> runs 0 to 1 as the knob slides across.
        /// </summary>
        internal static void DrawAnimated(Graphics g, Rectangle pill, float t)
        {
            bool on = t > 0.5f;
            int knob = NocturneScale.S(NocturneTheme.ToggleKnobSize);
            int pad = (pill.Height + 1 - knob) / 2;

            using (System.Drawing.Drawing2D.GraphicsPath p =
                       NocturneTheme.RoundedRect(pill, (pill.Height + 1) / 2))
            {
                float k = Math.Max(0f, Math.Min(1f, t));
                using (SolidBrush b = new SolidBrush(
                           NocturneTheme.Mix(NocturneTheme.ToggleOn, NocturneTheme.ToggleOff, k)))
                    g.FillPath(b, p);
                using (Pen pen = new Pen(
                           NocturneTheme.Mix(NocturneTheme.Accent, NocturneTheme.ToggleOffEdge, k)))
                    g.DrawPath(pen, p);
            }

            float travel = pill.Width + 1 - pad * 2 - knob;
            float x = pill.X + pad + travel * Math.Max(0f, Math.Min(1f, t));
            using (SolidBrush b = new SolidBrush(NocturneTheme.ToggleKnob))
                g.FillEllipse(b, x, pill.Y + pad, knob, knob);

            // On a light ground the knob and the pale accent fill are almost
            // the same value, so the knob needs an edge to stay legible.
            if (!NocturneTheme.IsDark)
            {
                using (Pen pen = new Pen(NocturneTheme.Alpha(NocturneTheme.Neutral900, 0.16)))
                    g.DrawEllipse(pen, x, pill.Y + pad, knob, knob);
            }
        }
    }
}
