using MelonLoader;

namespace QuickGloves
{
    // all your saved knobs
    public static class GloveSettings
    {
        private static MelonPreferences_Category _cat = null!;

        private static MelonPreferences_Entry<bool>  _enabled       = null!;
        private static MelonPreferences_Entry<bool>  _leftHand      = null!;
        private static MelonPreferences_Entry<bool>  _rightHand     = null!;
        private static MelonPreferences_Entry<bool>  _useTrigger    = null!;
        private static MelonPreferences_Entry<bool>  _holdToCatch   = null!;
        private static MelonPreferences_Entry<float> _range         = null!;
        private static MelonPreferences_Entry<float> _maxMass       = null!;
        private static MelonPreferences_Entry<float> _flickSpeed    = null!;
        private static MelonPreferences_Entry<float> _flightSpeed   = null!;
        private static MelonPreferences_Entry<bool>  _gravityPull   = null!;
        private static MelonPreferences_Entry<bool>  _forcePull     = null!;
        private static MelonPreferences_Entry<float> _forceSens     = null!;
        private static MelonPreferences_Entry<float> _catchRadius   = null!;
        private static MelonPreferences_Entry<float> _aimRadius     = null!;
        private static MelonPreferences_Entry<bool>  _showCrosshair = null!;
        private static MelonPreferences_Entry<bool>  _showBeam      = null!;
        private static MelonPreferences_Entry<bool>  _haptics       = null!;
        private static MelonPreferences_Entry<float> _hapticPower   = null!;
        private static MelonPreferences_Entry<bool>  _sounds        = null!;
        private static MelonPreferences_Entry<float> _soundVolume   = null!;
        private static MelonPreferences_Entry<bool>  _showArc       = null!;
        private static MelonPreferences_Entry<bool>  _showOutline   = null!;
        private static MelonPreferences_Entry<bool>  _showTrail     = null!;

        public static bool  Enabled       { get => _enabled.Value;       set { _enabled.Value       = value; Save(); } }
        public static bool  LeftHand      { get => _leftHand.Value;      set { _leftHand.Value      = value; Save(); } }
        public static bool  RightHand     { get => _rightHand.Value;     set { _rightHand.Value     = value; Save(); } }
        // trigger people, this one's for you
        public static bool  UseTrigger    { get => _useTrigger.Value;    set { _useTrigger.Value    = value; Save(); } }
        // off = no auto catch, do it yourself
        public static bool  HoldToCatch   { get => _holdToCatch.Value;   set { _holdToCatch.Value   = value; Save(); } }
        public static float Range         { get => _range.Value;         set { _range.Value         = value; Save(); } }
        public static float MaxMass       { get => _maxMass.Value;       set { _maxMass.Value       = value; Save(); } }
        // how fast a flick has to be
        public static float FlickSpeed    { get => _flickSpeed.Value;    set { _flickSpeed.Value    = value; Save(); } }
        public static float FlightSpeed   { get => _flightSpeed.Value;   set { _flightSpeed.Value   = value; Save(); } }
        // prop chases the glove till it's close
        public static bool  GravityPull   { get => _gravityPull.Value;   set { _gravityPull.Value   = value; Save(); } }
        // flick harder = comes in hotter
        public static bool  ForcePull     { get => _forcePull.Value;     set { _forcePull.Value     = value; Save(); } }
        public static float ForceSens     { get => _forceSens.Value;     set { _forceSens.Value     = value; Save(); } }
        public static float CatchRadius   { get => _catchRadius.Value;   set { _catchRadius.Value   = value; Save(); } }
        // how chunky the aim beam is
        public static float AimRadius     { get => _aimRadius.Value;     set { _aimRadius.Value     = value; Save(); } }
        public static bool  ShowCrosshair { get => _showCrosshair.Value; set { _showCrosshair.Value = value; Save(); } }
        // shows how chunky it really is
        public static bool  ShowBeam      { get => _showBeam.Value;      set { _showBeam.Value      = value; Save(); } }
        public static bool  Haptics       { get => _haptics.Value;       set { _haptics.Value       = value; Save(); } }
        public static float HapticPower   { get => _hapticPower.Value;   set { _hapticPower.Value   = value; Save(); } }
        public static bool  Sounds        { get => _sounds.Value;        set { _sounds.Value        = value; Save(); } }
        public static float SoundVolume   { get => _soundVolume.Value;   set { _soundVolume.Value   = value; Save(); } }
        public static bool  ShowArc       { get => _showArc.Value;       set { _showArc.Value       = value; Save(); } }
        public static bool  ShowOutline   { get => _showOutline.Value;   set { _showOutline.Value   = value; Save(); } }
        public static bool  ShowTrail     { get => _showTrail.Value;     set { _showTrail.Value     = value; Save(); } }

        public static void Initialize()
        {
            _cat           = MelonPreferences.CreateCategory("QuickGloves");
            _enabled       = _cat.CreateEntry("Enabled",       true);
            _leftHand      = _cat.CreateEntry("LeftHand",      true);
            _rightHand     = _cat.CreateEntry("RightHand",     true);
            _useTrigger    = _cat.CreateEntry("UseTrigger",    false);
            _holdToCatch   = _cat.CreateEntry("HoldToCatch",   true);
            _range         = _cat.CreateEntry("Range",         12f);
            _maxMass       = _cat.CreateEntry("MaxMass",       25f);
            _flickSpeed    = _cat.CreateEntry("FlickSpeed",    1.4f);
            _flightSpeed   = _cat.CreateEntry("FlightSpeed",   1f);
            _gravityPull   = _cat.CreateEntry("GravityPull",   true);
            _forcePull     = _cat.CreateEntry("ForcePull",     true);
            _forceSens     = _cat.CreateEntry("ForceSens",     1f);
            _catchRadius   = _cat.CreateEntry("CatchRadius",   0.22f);
            _aimRadius     = _cat.CreateEntry("AimRadius",     0.3f);
            _showCrosshair = _cat.CreateEntry("ShowCrosshair", true);
            _showBeam      = _cat.CreateEntry("ShowBeam",      false);
            _haptics       = _cat.CreateEntry("Haptics",       true);
            _hapticPower   = _cat.CreateEntry("HapticPower",   1f);
            _sounds        = _cat.CreateEntry("Sounds",        true);
            _soundVolume   = _cat.CreateEntry("SoundVolume",   0.8f);
            _showArc       = _cat.CreateEntry("ShowArc",       true);
            _showOutline   = _cat.CreateEntry("ShowOutline",   true);
            _showTrail     = _cat.CreateEntry("ShowTrail",     true);
        }

        private static void Save() => _cat.SaveToFile(false);

        public static MelonPreferences_Category Category => _cat;
    }
}
