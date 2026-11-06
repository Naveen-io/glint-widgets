using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace DeskWidgets
{
    sealed class Anim
    {
        static readonly Timer timer = new Timer { Interval = 12 };
        static readonly List<Anim> active = new List<Anim>();

        readonly float from, to;
        readonly int duration;
        readonly Action<float> tick;
        readonly Action done;
        int elapsed;

        static Anim()
        {
            timer.Tick += delegate
            {
                for (int i = active.Count - 1; i >= 0; i--)
                {
                    var a = active[i];
                    a.elapsed += timer.Interval;
                    float t = Math.Min(1f, a.elapsed / (float)a.duration);
                    t = t * (2f - t);
                    float value = a.from + (a.to - a.from) * t;
                    a.tick(value);
                    if (t >= 1f)
                    {
                        active.RemoveAt(i);
                        if (a.done != null) a.done();
                    }
                }
                if (active.Count == 0) timer.Stop();
            };
        }

        Anim(float from, float to, int duration, Action<float> tick, Action done)
        {
            this.from = from;
            this.to = to;
            this.duration = duration;
            this.tick = tick;
            this.done = done;
        }

        public static Anim Run(float from, float to, int durationMs, Action<float> onTick)
        {
            return Run(from, to, durationMs, onTick, null);
        }

        public static Anim Run(float from, float to, int durationMs, Action<float> onTick, Action onDone)
        {
            var a = new Anim(from, to, durationMs, onTick, onDone);
            active.Add(a);
            timer.Start();
            onTick(from);
            return a;
        }

        public void Cancel()
        {
            active.Remove(this);
            if (active.Count == 0) timer.Stop();
        }
    }
}
