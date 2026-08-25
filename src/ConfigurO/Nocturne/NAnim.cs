using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ConfigurO
{
    /// <summary>
    /// A single eased 0..1 value, driven by a timer.
    ///
    /// Generalised from the one place that already did this properly --
    /// MoonToggle, whose knob has always slid over 150ms on an ease-out curve
    /// while every other interactive surface in the app snapped. Hover, press,
    /// selection and fade all want the same three things: a value, a target,
    /// and a repaint per frame.
    ///
    /// The timer only runs while the value is actually moving, so an idle
    /// control costs nothing.
    /// </summary>
    internal sealed class NAnim : IDisposable
    {
        /// <summary>~66fps. Matches NScrollPanel so the two never beat against each other.</summary>
        const int FrameMs = 15;

        readonly Timer _timer = new Timer { Interval = FrameMs };
        readonly Action _onFrame;
        readonly int _duration;

        float _value, _target, _from;
        DateTime _start;

        internal NAnim(Action onFrame, int durationMs = 130)
        {
            _onFrame = onFrame;
            _duration = Math.Max(1, durationMs);
            _timer.Tick += Tick;
        }

        /// <summary>Current eased value, 0..1.</summary>
        internal float Value { get { return _value; } }

        /// <summary>True while the value is still travelling.</summary>
        internal bool Running { get { return _timer.Enabled; } }

        /// <summary>Eases towards <paramref name="target"/>. Cheap to call repeatedly.</summary>
        internal void To(float target)
        {
            if (target < 0f) target = 0f;
            if (target > 1f) target = 1f;
            if (Math.Abs(_target - target) < 0.0001f && _timer.Enabled) return;
            if (Math.Abs(_value - target) < 0.0001f) { Set(target); return; }

            // Someone who has turned animation off in Windows should not be
            // made to watch it here. SPI_GETCLIENTAREAANIMATION is the signal
            // the shell itself uses for exactly this.
            if (!AnimationsEnabled) { Set(target); return; }

            _from = _value;
            _target = target;
            _start = DateTime.UtcNow;
            _timer.Start();
        }

        /// <summary>Jumps straight to a value and repaints. No easing.</summary>
        internal void Set(float value)
        {
            _timer.Stop();
            _value = _target = value;
            if (_onFrame != null) _onFrame();
        }

        void Tick(object sender, EventArgs e)
        {
            float t = (float)(DateTime.UtcNow - _start).TotalMilliseconds / _duration;
            if (t >= 1f) { t = 1f; _timer.Stop(); }

            // Ease-out quadratic: leaves fast, settles rather than stopping
            // dead. The same curve MoonToggle uses, so the toggle and the row
            // it sits in move as one thing.
            float eased = 1f - (1f - t) * (1f - t);
            _value = _from + (_target - _from) * eased;
            if (_onFrame != null) _onFrame();
        }

        // ── Windows animation preference ────────────────────────────────
        const int SPI_GETCLIENTAREAANIMATION = 0x1042;

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SystemParametersInfo(int action, int param, ref bool value, int winIni);

        static bool? _animations;

        /// <summary>
        /// Whether Windows wants client-area animation at all. Read once --
        /// this is checked on every hover, and the setting does not move
        /// without a restart of the shell anyway.
        /// </summary>
        internal static bool AnimationsEnabled
        {
            get
            {
                if (_animations.HasValue) return _animations.Value;
                bool on = true;
                try { SystemParametersInfo(SPI_GETCLIENTAREAANIMATION, 0, ref on, 0); }
                catch (DllNotFoundException) { on = true; }
                catch (EntryPointNotFoundException) { on = true; }
                _animations = on;
                return on;
            }
        }

        public void Dispose()
        {
            _timer.Tick -= Tick;
            _timer.Dispose();
        }
    }
}
