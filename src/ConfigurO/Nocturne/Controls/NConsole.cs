using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// The dark monospace output panel used by the Pinger.
    ///
    /// Stays on the neutral-900 ground in both themes -- the handoff keeps
    /// console surfaces dark so terminal output reads the same way everywhere.
    /// </summary>
    internal sealed class NConsole : NControl
    {
        readonly List<string> _lines = new List<string>();

        internal NConsole()
        {
            LineHeight = 18;
        }

        internal int LineHeight { get; set; }

        internal void Clear() { _lines.Clear(); Invalidate(); }

        internal void Write(string line)
        {
            _lines.Add(line ?? string.Empty);
            Invalidate();
        }

        internal void Set(params string[] lines)
        {
            _lines.Clear();
            if (lines != null) _lines.AddRange(lines);
            Invalidate();
        }

        internal IEnumerable<string> Lines { get { return _lines; } }

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

            NocturneDraw.Card(g, new Rectangle(0, 0, Width, Height),
                              NocturneTheme.Backdrop, NocturneTheme.Border, NocturneTheme.RadiusMd);

            int pad = NocturneScale.S(12);
            int lh = NocturneScale.S(LineHeight);
            int visible = Math.Max(1, (Height - pad * 2) / lh);
            int first = Math.Max(0, _lines.Count - visible);

            using (Font f = NocturneFonts.Code())
            {
                for (int i = first; i < _lines.Count; i++)
                {
                    NocturneDraw.Text(g, _lines[i], f, NocturneTheme.ConsoleText,
                        new RectangleF(pad, pad + (i - first) * lh,
                                       Math.Max(0, Width - pad * 2), lh), NocturneDraw.Left);
                }
            }
        }
    }
}
