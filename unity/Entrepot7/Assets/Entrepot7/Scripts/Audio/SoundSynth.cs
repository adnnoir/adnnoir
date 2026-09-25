using System;
using System.Collections.Generic;
using UnityEngine;

namespace E7
{
    /// <summary>
    /// Petit synthétiseur « hors ligne » : fabrique des AudioClip à partir de bruits filtrés et de tonalités,
    /// comme la version web (Web Audio). Tout est calculé une fois au démarrage.
    /// </summary>
    public class SoundSynth
    {
        public const int SR = 44100;
        readonly float[] buf;
        readonly System.Random rng;

        public SoundSynth(float seconds, int seed = 1)
        {
            buf = new float[Mathf.CeilToInt(seconds * SR) + 1];
            rng = new System.Random(seed);
        }

        public float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        // Filtre biquad (formules RBJ)
        struct Biquad
        {
            float b0, b1, b2, a1, a2, x1, x2, y1, y2;
            public void Set(string type, float f, float q)
            {
                f = Mathf.Clamp(f, 10f, SR * 0.45f);
                float w = 2f * Mathf.PI * f / SR, cs = Mathf.Cos(w), sn = Mathf.Sin(w), alpha = sn / (2f * Mathf.Max(0.05f, q));
                float a0;
                switch (type)
                {
                    case "highpass": b0 = (1 + cs) / 2; b1 = -(1 + cs); b2 = (1 + cs) / 2; a0 = 1 + alpha; a1 = -2 * cs; a2 = 1 - alpha; break;
                    case "bandpass": b0 = alpha; b1 = 0; b2 = -alpha; a0 = 1 + alpha; a1 = -2 * cs; a2 = 1 - alpha; break;
                    default: b0 = (1 - cs) / 2; b1 = 1 - cs; b2 = (1 - cs) / 2; a0 = 1 + alpha; a1 = -2 * cs; a2 = 1 - alpha; break;
                }
                b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;
            }
            public float Run(float x)
            {
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return y;
            }
        }

        static float Env(float t, float dur, float attack, float gain)
        {
            // enveloppe exponentielle comme exponentialRampToValueAtTime (0.0001 → gain → 0.0001)
            if (t < attack) return gain * Mathf.Pow(gain / 0.0001f, t / attack - 1f) * 1f;
            float k = (t - attack) / Mathf.Max(1e-4f, dur - attack);
            return gain * Mathf.Pow(0.0001f / gain, Mathf.Clamp01(k));
        }

        /// <summary>Bruit filtré. type : lowpass / highpass / bandpass. fEnd : balayage exponentiel de la fréquence.</summary>
        public SoundSynth Noise(float at, float dur, float freq, float gain, string type = "lowpass", float q = 0.7f, float fEnd = 0, float attack = 0.001f)
        {
            var bq = new Biquad();
            int s0 = (int)(at * SR), n = (int)(dur * SR);
            for (int i = 0; i < n && s0 + i < buf.Length; i++)
            {
                float t = i / (float)SR;
                if ((i & 31) == 0) bq.Set(type, fEnd > 0 ? freq * Mathf.Pow(fEnd / freq, t / dur) : freq, q);
                float x = (float)rng.NextDouble() * 2f - 1f;
                buf[s0 + i] += bq.Run(x) * Env(t, dur, attack, gain);
            }
            return this;
        }

        /// <summary>Tonalité (sinus, carré, triangle, dent de scie) avec glissement de fréquence f0 → f1.</summary>
        public SoundSynth Tone(float at, float dur, float f0, float gain, string wave = "sine", float f1 = 0, float attack = 0.002f)
        {
            int s0 = (int)(at * SR), n = (int)(dur * SR);
            double ph = 0;
            for (int i = 0; i < n && s0 + i < buf.Length; i++)
            {
                float t = i / (float)SR;
                float f = f1 > 0 ? f0 * Mathf.Pow(f1 / f0, t / dur) : f0;
                ph += f / SR;
                float p = (float)(ph - Math.Floor(ph));
                float w;
                switch (wave)
                {
                    case "square": w = p < 0.5f ? 1f : -1f; break;
                    case "triangle": w = 1f - 4f * Mathf.Abs(p - 0.5f); break;
                    case "sawtooth": w = 2f * p - 1f; break;
                    default: w = Mathf.Sin(p * 2f * Mathf.PI); break;
                }
                buf[s0 + i] += w * Env(t, dur, attack, gain);
            }
            return this;
        }

        public AudioClip Clip(string name, float gain = 1f, bool normalize = false)
        {
            float peak = 0f;
            for (int i = 0; i < buf.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(buf[i]));
            float k = gain * (normalize && peak > 0 ? 0.9f / peak : (peak > 1f ? 1f / peak : 1f));
            var data = new float[buf.Length];
            for (int i = 0; i < buf.Length; i++) data[i] = Mathf.Clamp(buf[i] * k, -1f, 1f);
            var c = AudioClip.Create(name, data.Length, 1, SR, false);
            c.SetData(data, 0);
            return c;
        }

        // -------------------------------------------------------------------
        // Banque de sons procéduraux (repris de la version web)
        // -------------------------------------------------------------------
        public static Dictionary<string, AudioClip> BuildBank()
        {
            var b = new Dictionary<string, AudioClip>();
            SoundSynth S(float s, int seed) => new SoundSynth(s, seed);
            for (int v = 0; v < 4; v++)
            {
                var s = S(0.16f, 10 + v); s.Noise(0, 0.03f, 2600, 0.8f, "bandpass", 2); s.Noise(0.02f, 0.09f, s.R(350, 600), 1f, "bandpass", 1.4f);
                b["step" + v] = s.Clip("step" + v, 0.9f, true);
                var l = S(0.2f, 20 + v); l.Noise(0, 0.03f, 2600, 0.8f, "bandpass", 2); l.Noise(0.02f, 0.09f, l.R(350, 600), 1f, "bandpass", 1.4f);
                for (int i = 0; i < 3; i++) l.Tone(l.R(0, 0.08f), 0.03f, l.R(3500, 6000), 0.06f);
                b["stepLoud" + v] = l.Clip("stepLoud" + v, 1f, true);
            }
            b["click"] = S(0.06f, 2).Noise(0, 0.03f, 2600, 1, "highpass").Tone(0, 0.03f, 650, 0.3f, "square").Clip("click", 0.5f, true);
            b["dry"] = S(0.06f, 3).Noise(0, 0.03f, 3800, 1, "highpass").Tone(0, 0.03f, 950, 0.3f, "square").Clip("dry", 0.45f, true);
            b["magOut"] = S(0.2f, 4).Noise(0, 0.03f, 1800, 1, "highpass").Tone(0, 0.03f, 450, 0.3f, "square").Noise(0.05f, 0.12f, 900, 0.8f, "bandpass", 1.5f).Clip("magOut", 0.7f, true);
            b["pMagOut"] = S(0.06f, 5).Noise(0, 0.03f, 2600, 1, "highpass").Tone(0, 0.03f, 650, 0.3f, "square").Clip("pMagOut", 0.5f, true);
            b["pouch"] = S(0.2f, 6).Noise(0, 0.18f, 1400, 1, "bandpass", 0.8f).Clip("pouch", 0.5f, true);
            b["magIn"] = S(0.14f, 7).Noise(0, 0.05f, 1200, 1, "bandpass", 2).Tone(0, 0.05f, 280, 0.3f, "square").Noise(0.09f, 0.03f, 3000, 0.8f, "highpass").Clip("magIn", 0.8f, true);
            b["pMagIn"] = S(0.06f, 8).Noise(0, 0.04f, 1700, 1, "bandpass", 2).Tone(0, 0.04f, 420, 0.3f, "square").Clip("pMagIn", 0.75f, true);
            b["charge"] = S(0.24f, 9).Noise(0, 0.07f, 2200, 1, "bandpass", 2).Noise(0.16f, 0.06f, 1500, 1, "bandpass", 2).Tone(0.16f, 0.04f, 350, 0.3f, "square").Clip("charge", 0.75f, true);
            b["slideRel"] = S(0.06f, 10).Noise(0, 0.05f, 2500, 1, "bandpass", 2).Tone(0, 0.04f, 500, 0.3f, "square").Clip("slideRel", 0.8f, true);
            b["pumpBack"] = S(0.1f, 11).Noise(0, 0.09f, 1100, 1, "bandpass", 1.8f).Tone(0, 0.05f, 240, 0.3f, "square").Clip("pumpBack", 0.85f, true);
            b["pumpFwd"] = S(0.1f, 12).Noise(0, 0.07f, 1500, 1, "bandpass", 2).Noise(0.05f, 0.03f, 3000, 0.8f, "highpass").Clip("pumpFwd", 0.85f, true);
            b["shellIn"] = S(0.07f, 13).Noise(0, 0.05f, 1800, 1, "bandpass", 2).Tone(0.02f, 0.03f, 330, 0.25f, "square").Clip("shellIn", 0.65f, true);
            b["holster"] = S(0.22f, 14).Noise(0, 0.2f, 900, 1, "bandpass", 0.8f).Noise(0, 0.03f, 2000, 0.5f, "highpass").Clip("holster", 0.5f, true);
            b["draw"] = S(0.16f, 15).Noise(0, 0.14f, 1300, 1, "bandpass", 0.9f).Tone(0.1f, 0.05f, 380, 0.25f, "square").Clip("draw", 0.5f, true);
            { var s = S(0.3f, 16); for (int i = 0; i < 4; i++) s.Noise(i * 0.06f, 0.03f, s.R(1500, 3000), 0.8f, "bandpass", 3); b["rattle"] = s.Clip("rattle", 0.35f, true); }
            b["impact_concrete"] = S(0.1f, 17).Noise(0, 0.08f, 1400, 1, "bandpass", 1.5f).Clip("impact_concrete", 0.9f, true);
            b["impact_wood"] = S(0.1f, 18).Noise(0, 0.08f, 700, 1, "bandpass", 1.5f).Clip("impact_wood", 0.9f, true);
            b["impact_cardboard"] = S(0.1f, 19).Noise(0, 0.08f, 500, 1, "bandpass", 1.5f).Clip("impact_cardboard", 0.9f, true);
            { var s = S(0.32f, 20); s.Noise(0, 0.25f, 3200, 1, "bandpass", 9).Tone(0, 0.3f, s.R(1800, 2600), 0.2f); b["impact_metal"] = s.Clip("impact_metal", 0.9f, true); }
            b["impact_flesh"] = S(0.12f, 21).Noise(0, 0.1f, 420, 1, "bandpass", 1.2f).Tone(0, 0.06f, 110, 0.6f, "sine", 60).Clip("impact_flesh", 0.9f, true);
            b["crack"] = S(0.2f, 22).Noise(0, 0.025f, 3500, 1, "highpass").Noise(0.008f, 0.16f, 3000, 0.4f, "bandpass", 4, 600).Clip("crack", 0.8f, true);
            { var s = S(0.25f, 23); float f = s.R(3200, 4600); s.Tone(0, 0.14f, f, 1).Tone(0, 0.1f, f * 1.47f, 0.5f); b["tink"] = s.Clip("tink", 0.35f, true); }
            b["tinkHeavy"] = S(0.12f, 24).Tone(0, 0.08f, 1100, 1).Noise(0, 0.05f, 800, 0.6f, "bandpass", 2).Clip("tinkHeavy", 0.35f, true);
            b["hurt"] = S(0.3f, 25).Tone(0, 0.25f, 90, 1, "sine", 40).Noise(0, 0.12f, 900, 0.6f).Clip("hurt", 0.9f, true);
            b["heart"] = S(0.35f, 26).Tone(0, 0.12f, 60, 1, "sine", 40).Tone(0.18f, 0.12f, 55, 0.8f, "sine", 38).Clip("heart", 0.9f, true);
            b["breathIn"] = S(0.5f, 27).Noise(0, 0.45f, 1300, 1, "bandpass", 0.9f, 0, 0.12f).Clip("breathIn", 0.35f, true);
            b["breathOut"] = S(0.4f, 28).Noise(0, 0.35f, 900, 1, "bandpass", 0.9f, 0, 0.12f).Clip("breathOut", 0.35f, true);
            b["body"] = S(0.25f, 29).Noise(0, 0.2f, 300, 1).Tone(0, 0.15f, 70, 0.8f, "sine", 40).Clip("body", 1f, true);
            { var s = S(0.35f, 30); for (int i = 0; i < 3; i++) s.Noise(i * 0.09f, 0.07f, s.R(1500, 3000), 1f / (i + 1), "bandpass", 4); b["clatter"] = s.Clip("clatter", 0.8f, true); }
            b["clank"] = S(0.14f, 31).Tone(0, 0.12f, 1150, 1).Noise(0, 0.05f, 2200, 0.8f, "bandpass", 3).Clip("clank", 0.8f, true);
            b["pin"] = S(0.32f, 32).Tone(0, 0.08f, 3800, 0.6f).Noise(0.1f, 0.05f, 3000, 0.8f, "highpass").Tone(0.2f, 0.1f, 2600, 0.4f).Clip("pin", 0.6f, true);
            b["bang"] = S(1.0f, 33).Noise(0, 0.01f, 3000, 1.5f, "highpass").Noise(0, 0.9f, 6000, 1.8f, "lowpass", 0.7f, 200).Tone(0, 0.6f, 70, 1.6f, "sine", 25).Clip("bang", 1f, true);
            { var s = S(0.4f, 34); for (int i = 0; i < 6; i++) s.Tone(i * 0.035f, 0.04f, s.R(2500, 4000), 0.15f, "square"); s.Noise(0.3f, 0.05f, 3000, 0.6f, "highpass"); b["cuffs"] = s.Clip("cuffs", 0.7f, true); }
            b["enemyReload"] = S(2.2f, 35).Noise(0.3f, 0.05f, 1600, 1, "bandpass", 2).Noise(1.4f, 0.05f, 1200, 1, "bandpass", 2).Noise(2.1f, 0.08f, 2000, 1, "bandpass", 2).Clip("enemyReload", 0.8f, true);
            b["pickup"] = S(0.14f, 36).Noise(0, 0.12f, 900, 1, "bandpass", 2).Noise(0, 0.03f, 2000, 0.5f, "highpass").Clip("pickup", 0.6f, true);
            b["squelch"] = S(0.2f, 37).Noise(0, 0.12f, 1900, 1, "bandpass", 1.5f).Tone(0.12f, 0.06f, 1250, 0.2f, "square").Clip("squelch", 0.45f, true);
            b["ui"] = S(0.05f, 38).Tone(0, 0.04f, 1800, 1, "triangle").Clip("ui", 0.25f, true);
            b["uiHi"] = S(0.05f, 39).Tone(0, 0.04f, 2400, 1, "triangle").Clip("uiHi", 0.25f, true);
            b["paper"] = S(0.08f, 40).Noise(0, 0.06f, 900, 1, "bandpass", 1).Clip("paper", 0.6f, true);
            { var s = S(1.3f, 41); float f = s.R(1150, 1350); s.Tone(0, 1.2f, f, 1).Tone(0, 0.9f, f * 2.76f, 0.35f).Tone(0, 0.6f, f * 5.4f, 0.12f); b["ding"] = s.Clip("ding", 0.7f, true); }
            b["tinnitus"] = S(3f, 42).Tone(0, 3f, 4000, 1, "sine", 0, 0.05f).Clip("tinnitus", 0.25f, true);
            b["thunder"] = S(3.6f, 43).Noise(0, 3.5f, 160, 1, "lowpass", 0.7f, 60, 0.4f).Clip("thunder", 0.9f, true);
            { var s = S(0.1f, 44); s.Tone(0, 0.08f, s.R(1400, 2600), 1, "sine", s.R(700, 1200)); b["drip"] = s.Clip("drip", 0.5f, true); }
            b["siren"] = Siren();
            b["rain"] = Loop("rain", 4f, 45, "bandpass", 2600, 0.5f);
            b["room"] = Loop("room", 4f, 46, "lowpass", 380, 0.7f);
            b["hum"] = HumLoop();
            b["music"] = Music();
            return b;
        }

        /// <summary>Bruit filtré continu qui boucle sans clic (pluie, bruit de fond de la pièce).</summary>
        static AudioClip Loop(string name, float len, int seed, string type, float freq, float q)
        {
            var rng = new System.Random(seed);
            int fade = SR / 10, n = Mathf.CeilToInt(len * SR) + fade;
            var raw = new float[n];
            var f = new Biquad(); f.Set(type, freq, q);
            for (int i = 0; i < n; i++) raw[i] = f.Run((float)rng.NextDouble() * 2f - 1f);
            var data = new float[n - fade];
            Array.Copy(raw, data, data.Length);
            for (int i = 0; i < fade; i++) { float k = i / (float)fade; data[i] = raw[i] * k + raw[n - fade + i] * (1 - k); }
            float peak = 0; foreach (var x in data) peak = Mathf.Max(peak, Mathf.Abs(x));
            for (int i = 0; i < data.Length; i++) data[i] *= 0.8f / Mathf.Max(peak, 1e-4f);
            var c = AudioClip.Create(name, data.Length, 1, SR, false);
            c.SetData(data, 0);
            return c;
        }

        static AudioClip HumLoop()
        {
            int n = SR; // 1 s : 100 Hz tombe juste
            var d = new float[n];
            float[] f = { 100, 200, 300, 400 }, g = { 1, 0.5f, 0.25f, 0.1f };
            for (int i = 0; i < n; i++) { float t = i / (float)SR, v = 0; for (int k = 0; k < 4; k++) v += Mathf.Sin(2 * Mathf.PI * f[k] * t) * g[k]; d[i] = v * 0.45f; }
            var c = AudioClip.Create("hum", n, 1, SR, false); c.SetData(d, 0); return c;
        }

        static AudioClip Siren()
        {
            int n = SR * 7; var d = new float[n]; double ph = 0;
            var lp = new Biquad(); lp.Set("lowpass", 900, 0.7f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR, sec = t % 1f;
                float f = sec < 0.5f ? Mathf.Lerp(620, 900, sec / 0.5f) : 900 - (sec - 0.5f) * 0f;
                if (sec >= 0.5f) f = 900;
                ph += f / SR; float saw = (float)(2 * (ph - Math.Floor(ph)) - 1);
                float env = t < 1.5f ? t / 1.5f : Mathf.Clamp01((7f - t) / 5.5f);
                d[i] = lp.Run(saw) * env * 0.8f;
            }
            var c = AudioClip.Create("siren", n, 1, SR, false); c.SetData(d, 0); return c;
        }

        /// <summary>Musique du menu : nappes sombres qui changent d'accord toutes les 8 s (comme la version web).</summary>
        static AudioClip Music()
        {
            const int sr = 22050; int per = sr * 8;
            int[][] chords = { new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 48, 52, 55 }, new[] { 55, 59, 62 }, new[] { 57, 60, 64 }, new[] { 53, 57, 62 }, new[] { 52, 55, 59 }, new[] { 52, 56, 59 } };
            int n = per * chords.Length; var d = new float[n];
            double[] ph = new double[7];
            float[] cur = new float[4];
            var lp = new Biquad();
            float Hz(int m) => 440f * Mathf.Pow(2f, (m - 69) / 12f);
            for (int i = 0; i < n; i++)
            {
                int ci = i / per; var ch = chords[ci];
                float t = i / (float)sr;
                for (int k = 0; k < 3; k++) cur[k] = Mathf.Lerp(cur[k] == 0 ? Hz(ch[k]) / 2 : cur[k], Hz(ch[k]) / 2, 1f / (sr * 1.1f) * 3f);
                cur[3] = Mathf.Lerp(cur[3] == 0 ? Hz(ch[0]) / 4 : cur[3], Hz(ch[0]) / 4, 1f / (sr * 0.8f) * 3f);
                if ((i & 63) == 0) lp.Set("lowpass", 700 + 380 * Mathf.Sin(2 * Mathf.PI * 0.045f * t), 1.2f);
                float v = 0;
                for (int k = 0; k < 3; k++)
                {
                    ph[k] += cur[k] / sr; ph[k + 3] += cur[k] * 1.0064f / sr; // léger désaccord
                    v += (float)(2 * (ph[k] - Math.Floor(ph[k])) - 1) * 0.045f + (float)(2 * (ph[k + 3] - Math.Floor(ph[k + 3])) - 1) * 0.045f;
                }
                float o = lp.Run(v);
                ph[6] += cur[3] / sr; o += Mathf.Sin((float)(ph[6] * 2 * Math.PI)) * 0.16f;
                // petite impulsion « sonar » au début de chaque accord
                float tl = (i % per) / (float)sr - 0.1f;
                if (tl > 0 && tl < 1.4f) o += Mathf.Sin(2 * Mathf.PI * Hz(ch[2]) * 2 * tl) * 0.05f * Mathf.Exp(-tl * 4f);
                d[i] = o * 1.6f;
            }
            var c = AudioClip.Create("music", n, 1, sr, false); c.SetData(d, 0); return c;
        }
    }
}
