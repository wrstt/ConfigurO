using System;
using System.Drawing;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// The first-run language list: every language, its flag, and its own name.
    ///
    /// One table drives the whole control. The form this replaced kept the
    /// three facts apart -- a PictureBox held the flag, a RadioButton held the
    /// name, and a hand-written Click handler joined them -- and had drifted:
    /// the Korea flag selected Chinese, the Ukraine and Bulgaria flags were
    /// wired to nothing at all, and the Taiwan flag was never shown because
    /// China's was used twice. None of those can be expressed here.
    /// </summary>
    internal sealed class NLanguagePicker : NControl
    {
        struct Entry
        {
            internal readonly LanguageCode Code;
            internal readonly Bitmap Flag;
            internal readonly string Name;
            internal Entry(LanguageCode code, Bitmap flag, string name)
            {
                Code = code; Flag = flag; Name = name;
            }
        }

        // Ordered as the languages appear in LanguageCode, which is roughly by
        // when each was contributed. English first; the rest follow.
        static readonly Entry[] Entries =
        {
            new Entry(LanguageCode.EN, Properties.Resources.united_kingdom, Constants.ENGLISH),
            new Entry(LanguageCode.RU, Properties.Resources.russia,         Constants.RUSSIAN),
            new Entry(LanguageCode.EL, Properties.Resources.greece,         Constants.HELLENIC),
            new Entry(LanguageCode.TR, Properties.Resources.turkey,         Constants.TURKISH),
            new Entry(LanguageCode.DE, Properties.Resources.germany,        Constants.GERMAN),
            new Entry(LanguageCode.ES, Properties.Resources.spain,          Constants.SPANISH),
            new Entry(LanguageCode.PT, Properties.Resources.brazil,         Constants.PORTUGUESE),
            new Entry(LanguageCode.FR, Properties.Resources.france,         Constants.FRENCH),
            new Entry(LanguageCode.IT, Properties.Resources.italy,          Constants.ITALIAN),
            new Entry(LanguageCode.CN, Properties.Resources.china,          Constants.CHINESE),
            new Entry(LanguageCode.CZ, Properties.Resources.czech,          Constants.CZECH),
            new Entry(LanguageCode.TW, Properties.Resources.taiwan,         Constants.TAIWANESE),
            new Entry(LanguageCode.KO, Properties.Resources.korea,          Constants.KOREAN),
            new Entry(LanguageCode.PL, Properties.Resources.poland,         Constants.POLISH),
            new Entry(LanguageCode.AR, Properties.Resources.egypt,          Constants.ARABIC),
            new Entry(LanguageCode.KU, Properties.Resources.kurdish,        Constants.KURDISH),
            new Entry(LanguageCode.HU, Properties.Resources.hungary,        Constants.HUNGARIAN),
            new Entry(LanguageCode.RO, Properties.Resources.romania,        Constants.ROMANIAN),
            new Entry(LanguageCode.NL, Properties.Resources.dutch,          Constants.DUTCH),
            new Entry(LanguageCode.UA, Properties.Resources.ukraine,        Constants.UKRAINIAN),
            new Entry(LanguageCode.JA, Properties.Resources.japan,          Constants.JAPANESE),
            new Entry(LanguageCode.FA, Properties.Resources.iran,           Constants.PERSIAN),
            new Entry(LanguageCode.NE, Properties.Resources.nepal,          Constants.NEPALI),
            new Entry(LanguageCode.BG, Properties.Resources.bulgaria,       Constants.BULGARIAN),
            new Entry(LanguageCode.VN, Properties.Resources.vietnam,        Constants.VIETNAMESE),
            new Entry(LanguageCode.UR, Properties.Resources.pakistan,       Constants.URDU),
            new Entry(LanguageCode.ID, Properties.Resources.indonesia,      Constants.INDONESIAN),
            new Entry(LanguageCode.HR, Properties.Resources.croatia,        Constants.CROATIAN),
        };

        int _selected;
        int _hover = -1;

        internal event EventHandler SelectionChanged;

        internal NLanguagePicker()
        {
            TabStop = true;
        }

        internal LanguageCode Selected
        {
            get { return Entries[_selected].Code; }
            set
            {
                for (int i = 0; i < Entries.Length; i++)
                {
                    if (Entries[i].Code != value) continue;
                    SetSelected(i, false);
                    return;
                }
            }
        }

        internal int Columns { get { return 2; } }
        int Rows { get { return (Entries.Length + Columns - 1) / Columns; } }

        int RowHeight { get { return NocturneScale.S(38); } }
        int ColumnWidth { get { return Math.Max(1, Width / Columns); } }

        /// <summary>Height this control needs to show every language at once.</summary>
        internal int PreferredHeight { get { return Rows * RowHeight; } }

        Rectangle CellBounds(int index)
        {
            int col = index / Rows, row = index % Rows;
            return new Rectangle(col * ColumnWidth, row * RowHeight, ColumnWidth, RowHeight);
        }

        int IndexAt(Point p)
        {
            for (int i = 0; i < Entries.Length; i++)
                if (CellBounds(i).Contains(p)) return i;
            return -1;
        }

        void SetSelected(int index, bool notify)
        {
            if (index < 0 || index >= Entries.Length || index == _selected) return;
            _selected = index;
            Invalidate();
            if (!notify) return;
            EventHandler h = SelectionChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = IndexAt(e.Location);
            if (i == _hover) return;
            _hover = i;
            Cursor = i >= 0 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            SetSelected(IndexAt(e.Location), true);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Up: case Keys.Down: case Keys.Left: case Keys.Right:
                    return true;
                default:
                    return base.IsInputKey(keyData);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            int next = _selected;
            switch (e.KeyCode)
            {
                case Keys.Up:    next = _selected - 1; break;
                case Keys.Down:  next = _selected + 1; break;
                case Keys.Left:  next = _selected - Rows; break;
                case Keys.Right: next = _selected + Rows; break;
                default: return;
            }
            if (next >= 0 && next < Entries.Length) SetSelected(next, true);
            e.Handled = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            NocturneDraw.Prepare(g);

            using (SolidBrush bg = new SolidBrush(NocturneTheme.Bg))
                g.FillRectangle(bg, ClientRectangle);

            int pad = NocturneScale.S(10);
            int flagW = NocturneScale.S(26);
            int flagH = NocturneScale.S(16);

            for (int i = 0; i < Entries.Length; i++)
            {
                Rectangle cell = CellBounds(i);
                if (!cell.IntersectsWith(e.ClipRectangle)) continue;
                Entry entry = Entries[i];

                Rectangle inner = new Rectangle(cell.X + pad / 2, cell.Y + NocturneScale.S(2),
                                                cell.Width - pad, cell.Height - NocturneScale.S(4));
                if (i == _selected)
                    NocturneDraw.Card(g, inner, NocturneTheme.SelectedFill,
                                      NocturneTheme.AccentStrong, NocturneTheme.RadiusSm);
                else if (i == _hover)
                    NocturneDraw.Card(g, inner, NocturneTheme.HoverFill,
                                      Color.Empty, NocturneTheme.RadiusSm);

                int fx = inner.X + pad;
                int fy = inner.Y + (inner.Height - flagH) / 2;
                Rectangle flag = new Rectangle(fx, fy, flagW, flagH);
                g.DrawImage(entry.Flag, flag);
                // Japan, and any other flag with a white field, has no edge of
                // its own against a light surface.
                NocturneTheme.DrawRounded(g, flag, 0, NocturneTheme.Border);

                int tx = fx + flagW + pad;
                // Each name is written in its own script, so the face has to be
                // chosen per row -- the app's current language says nothing
                // about what this row needs.
                using (Font f = NocturneFonts.SansFor(entry.Code, 10.125f))
                    NocturneDraw.Text(g, entry.Name, f,
                        i == _selected ? NocturneTheme.AccentText : NocturneTheme.Text,
                        new RectangleF(tx, inner.Y, inner.Right - tx - pad, inner.Height),
                        NocturneDraw.Left);
            }

            if (Focused)
                NocturneDraw.FocusRing(g, CellBounds(_selected), NocturneTheme.RadiusSm);

            base.OnPaint(e);
        }

        protected override void OnGotFocus(EventArgs e)  { base.OnGotFocus(e);  Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    }
}
