using System;
using BoneLib.BoneMenu;
using MelonLoader;
using UnityEngine;

namespace QuickGloves
{
    // one theme to rule every Quick mod.
    // first mod to load builds it.
    // everybody else just mooches off it.
    public static class QuickModsTheme
    {
        private const string ShareKey = "QuickMods.GlobalTheme";
        private const string MenuKey  = "QuickMods.GlobalThemeMenuBuilt";

        private static MelonPreferences_Category    _cat       = null!;
        private static MelonPreferences_Entry<bool>  _useGlobal = null!;
        private static MelonPreferences_Entry<float> _hue       = null!;
        private static MelonPreferences_Entry<float> _sat       = null!;
        private static MelonPreferences_Entry<bool>  _forceQB   = null!;
        private static MelonPreferences_Entry<bool>  _rainbow      = null!;
        private static MelonPreferences_Entry<float> _rainbowSpeed = null!;

        // the big switch: everyone uses this color
        public static bool UseGlobalTheme
        {
            get => _useGlobal.Value;
            set { _useGlobal.Value = value; Save(); }
        }

        public static float Hue
        {
            get => _hue.Value;
            set { _hue.Value = value; Save(); }
        }

        public static float Saturation
        {
            get => _sat.Value;
            set { _sat.Value = value; Save(); }
        }

        // i <3 QuickBeats, no opting out.. even tho yall dont have it.
        public static bool ForceQuickBeatsSync
        {
            get => _forceQB.Value;
            set { _forceQB.Value = value; Save(); }
        }

        public static Color GlobalColor => Color.HSVToRGB(Hue / 360f, Saturation, 1f);

        // rainbow mode
        public static bool RainbowMode
        {
            get => _rainbow.Value;
            set { _rainbow.Value = value; Save(); }
        }

        // rainbow laps per second
        public static float RainbowSpeed
        {
            get => _rainbowSpeed.Value;
            set { _rainbowSpeed.Value = value; Save(); }
        }

        // no update loop, just reads the clock
        public static Color RainbowColor => Color.HSVToRGB((Time.time * RainbowSpeed) % 1f, 0.85f, 1f);

        // who wins the color fight, top first:
        //  1. rainbow mode, obviously
        //  2. forced QuickBeats color
        //  3. the global theme color
        //  4. this mod's own song color
        //  5. the boring default
        // qbGetter says null if QuickBeats is missing
        public static Color ResolveAccent(Color modDefault, bool modThemeToSong, Func<Color?> qbGetter)
        {
            if (RainbowMode)
                return RainbowColor;
            if (ForceQuickBeatsSync)
            {
                var c = SafeGet(qbGetter);
                if (c.HasValue) return c.Value;
            }
            if (UseGlobalTheme)
                return GlobalColor;
            if (modThemeToSong)
            {
                var c = SafeGet(qbGetter);
                if (c.HasValue) return c.Value;
            }
            return modDefault;
        }

        private static Color? SafeGet(Func<Color?> getter)
        {
            try { return getter?.Invoke(); } catch { return null; }
        }

        public static void Initialize()
        {
            // old Quick mods send short lists, careful
            if (AppDomain.CurrentDomain.GetData(ShareKey) is object[] shared && shared.Length >= 7)
            {
                try
                {
                    _cat          = (MelonPreferences_Category)shared[0];
                    _useGlobal    = (MelonPreferences_Entry<bool>)shared[1];
                    _hue          = (MelonPreferences_Entry<float>)shared[2];
                    _sat          = (MelonPreferences_Entry<float>)shared[3];
                    _forceQB      = (MelonPreferences_Entry<bool>)shared[4];
                    _rainbow      = (MelonPreferences_Entry<bool>)shared[5];
                    _rainbowSpeed = (MelonPreferences_Entry<float>)shared[6];
                    return;
                }
                catch { /* stale share, rebuild below */ }
            }

            // CreateCategory reuses, CreateEntry throws a fit
            _cat          = MelonPreferences.CreateCategory("QuickModsTheme");
            _useGlobal    = Entry("UseGlobalTheme",      false);
            _hue          = Entry("Hue",                 200f);
            _sat          = Entry("Saturation",          0.72f);
            _forceQB      = Entry("ForceQuickBeatsSync", false);
            _rainbow      = Entry("RainbowMode",         false);
            _rainbowSpeed = Entry("RainbowSpeed",        0.15f);

            AppDomain.CurrentDomain.SetData(ShareKey, new object[] { _cat, _useGlobal, _hue, _sat, _forceQB, _rainbow, _rainbowSpeed });
        }

        // borrow the old mod's entry, no crashing
        private static MelonPreferences_Entry<T> Entry<T>(string id, T fallback)
        {
            try
            {
                var existing = _cat.GetEntry<T>(id);
                if (existing != null) return existing;
            }
            catch { _cat.DeleteEntry(id); }
            return _cat.CreateEntry(id, fallback);
        }

        // adds the theme page, first caller only
        public static void BuildMenu(Page rootPage)
        {
            if (AppDomain.CurrentDomain.GetData(MenuKey) is bool built && built) return;
            AppDomain.CurrentDomain.SetData(MenuKey, true);

            Page page = rootPage.CreatePage("Global Theme", new Color(1f, 0.55f, 0.85f));
            page.CreateBool ("Use Global Theme",             Color.white,   UseGlobalTheme,      v => UseGlobalTheme = v);
            page.CreateFloat("Hue",                          Color.white,   Hue,        5f,    0f,   360f, v => Hue = v);
            page.CreateFloat("Saturation",                   Color.white,   Saturation, 0.05f, 0.1f, 1f,   v => Saturation = v);
            page.CreateBool ("Force QuickBeats (All Mods)",  Color.magenta, ForceQuickBeatsSync, v => ForceQuickBeatsSync = v);
            page.CreateBool ("Rainbow Mode (All Mods)",      Color.red,     RainbowMode, v => RainbowMode = v);
            page.CreateFloat("Rainbow Speed",                Color.red,     RainbowSpeed, 0.02f, 0.02f, 1f, v => RainbowSpeed = v);
        }

        private static void Save() => _cat.SaveToFile(false);
    }
}
