using MelonLoader;
using UnityEngine;

namespace QuickGloves
{
    // every glowy thing picks its own color
    public static class GloveColors
    {
        public const int Arc = 0, Crosshair = 1, Outline = 2, Trail = 3, Beam = 4;
        public const int Count = 5;

        public static readonly string[] Names = { "aim arc", "crosshair", "item outline", "pull trail", "radius beam" };
        private static readonly string[] Keys = { "Arc", "Crosshair", "Outline", "Trail", "Beam" };

        private static readonly MelonPreferences_Entry<bool>[]  _custom  = new MelonPreferences_Entry<bool>[Count];
        private static readonly MelonPreferences_Entry<float>[] _hue     = new MelonPreferences_Entry<float>[Count];
        private static readonly MelonPreferences_Entry<float>[] _sat     = new MelonPreferences_Entry<float>[Count];
        private static readonly MelonPreferences_Entry<bool>[]  _rainbow = new MelonPreferences_Entry<bool>[Count];

        private static MelonPreferences_Category _cat = null!;
        private static bool _ready;

        public static void Initialize(MelonPreferences_Category cat)
        {
            _cat = cat;
            for (int i = 0; i < Count; i++)
            {
                _custom[i]  = cat.CreateEntry(Keys[i] + "OwnColor", false);
                _hue[i]     = cat.CreateEntry(Keys[i] + "Hue",      30f);
                _sat[i]     = cat.CreateEntry(Keys[i] + "Sat",      0.9f);
                _rainbow[i] = cat.CreateEntry(Keys[i] + "Rainbow",  false);
            }
            _ready = true;
        }

        public static bool  Custom(int part)  => _custom[part].Value;
        public static float Hue(int part)     => _hue[part].Value;
        public static float Sat(int part)     => _sat[part].Value;
        public static bool  Rainbow(int part) => _rainbow[part].Value;

        public static void SetCustom(int part, bool v)  { _custom[part].Value  = v; Save(); }
        public static void SetHue(int part, float v)    { _hue[part].Value     = v; Save(); }
        public static void SetSat(int part, float v)    { _sat[part].Value     = v; Save(); }
        public static void SetRainbow(int part, bool v) { _rainbow[part].Value = v; Save(); }

        private static void Save() => _cat.SaveToFile(false);

        // rainbow beats own color beats the theme
        public static Color Get(int part)
        {
            try
            {
                if (_ready && part >= 0 && part < Count)
                {
                    // each part sits a bit apart on the wheel
                    if (_rainbow[part].Value)
                        return Color.HSVToRGB(Mathf.Repeat(Time.time * 0.25f + part * 0.13f, 1f), 0.9f, 1f);

                    if (_custom[part].Value)
                        return Color.HSVToRGB(Mathf.Repeat(_hue[part].Value / 360f, 1f), Mathf.Clamp01(_sat[part].Value), 1f);
                }
            }
            catch { }
            return GloveFx.Accent();
        }
    }
}
