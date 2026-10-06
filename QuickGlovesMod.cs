using System;
using BoneLib.BoneMenu;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(QuickGloves.QuickGlovesMod), "QuickGloves", "1.0.0", "nontendo")]
[assembly: MelonGame("Stress Level Zero", "BONELAB")]

namespace QuickGloves
{
    public class QuickGlovesMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            GloveSettings.Initialize();
            QuickModsTheme.Initialize();
            GloveSound.Initialize();

            BoneLib.Hooking.OnLevelLoaded += _ => GloveManager.OnLevelLoaded();

            // the one and only Fusion check
            if (MelonBase.FindMelon("LabFusion", "Lakatrazz") != null)
            {
                GloveManager.FusionAvailable = true;
                LoggerInstance.Msg("QuickGloves: Fusion sync active");
            }

            SetupMenu();
            LoggerInstance.Msg("QuickGloves loaded!");
        }

        private static Page GetOrCreateQuickModsPage()
        {
            const string key = "QuickMods.RootPage";
            if (AppDomain.CurrentDomain.GetData(key) is Page p) return p;
            Page page = Page.Root.CreatePage("Quick Mods", new Color(0.35f, 0.27f, 0.9f), 0, true);
            AppDomain.CurrentDomain.SetData(key, page);
            return page;
        }

        private static void SetupMenu()
        {
            Page parent = GetOrCreateQuickModsPage();
            QuickModsTheme.BuildMenu(parent);

            Page root = parent.CreatePage("QuickGloves", GloveFx.Orange);
            root.CreateBool("enabled", Color.white, GloveSettings.Enabled, v => GloveSettings.Enabled = v);
            root.CreateBool("left glove", Color.white, GloveSettings.LeftHand, v => GloveSettings.LeftHand = v);
            root.CreateBool("right glove", Color.white, GloveSettings.RightHand, v => GloveSettings.RightHand = v);
            root.CreateBool("lock with trigger (not grip)", Color.white, GloveSettings.UseTrigger,
                v => GloveSettings.UseTrigger = v);

            Page aim = root.CreatePage("aim", new Color(1f, 0.55f, 0.2f));
            aim.CreateBool("crosshair", Color.white, GloveSettings.ShowCrosshair, v => GloveSettings.ShowCrosshair = v);
            aim.CreateFloat("aim radius", Color.white, GloveSettings.AimRadius, 0.05f, 0.05f, 1f,
                v => GloveSettings.AimRadius = v);
            aim.CreateBool("view radius beam", Color.white, GloveSettings.ShowBeam, v => GloveSettings.ShowBeam = v);

            Page pull = root.CreatePage("pull", new Color(1f, 0.75f, 0.3f));
            pull.CreateFloat("range", Color.white, GloveSettings.Range, 1f, 3f, 30f, v => GloveSettings.Range = v);
            pull.CreateFloat("max item weight", Color.white, GloveSettings.MaxMass, 5f, 5f, 150f,
                v => GloveSettings.MaxMass = v);
            pull.CreateFloat("flick needed (lower = easier)", Color.white, GloveSettings.FlickSpeed, 0.1f, 0.5f, 4f,
                v => GloveSettings.FlickSpeed = v);
            pull.CreateFloat("flight speed", Color.white, GloveSettings.FlightSpeed, 0.1f, 0.5f, 2f,
                v => GloveSettings.FlightSpeed = v);
            pull.CreateBool("gravity pull (follows your hand)", Color.white, GloveSettings.GravityPull,
                v => GloveSettings.GravityPull = v);
            pull.CreateFunction("(lets go 4ft from your hand)", new Color(0.6f, 0.6f, 0.6f), () => { });
            pull.CreateBool("pull depends on force", Color.white, GloveSettings.ForcePull,
                v => GloveSettings.ForcePull = v);
            pull.CreateFloat("force sensitivity", Color.white, GloveSettings.ForceSens, 0.1f, 0.2f, 3f,
                v => GloveSettings.ForceSens = v);

            Page grab = root.CreatePage("catch", new Color(0.5f, 0.9f, 0.6f));
            grab.CreateBool("hold to catch", Color.white, GloveSettings.HoldToCatch, v => GloveSettings.HoldToCatch = v);
            grab.CreateFunction("(off = let go and grab it yourself)", new Color(0.6f, 0.6f, 0.6f), () => { });
            grab.CreateFloat("catch reach", Color.white, GloveSettings.CatchRadius, 0.02f, 0.1f, 0.5f,
                v => GloveSettings.CatchRadius = v);

            Page feel = root.CreatePage("haptics", new Color(0.4f, 0.8f, 1f));
            feel.CreateBool("haptics", Color.white, GloveSettings.Haptics, v => GloveSettings.Haptics = v);
            feel.CreateFloat("strength", Color.white, GloveSettings.HapticPower, 0.1f, 0.2f, 2f,
                v => GloveSettings.HapticPower = v);

            // drop your own in UserData, QuickGloves, Sounds
            Page snd = root.CreatePage("sound", new Color(0.5f, 0.9f, 0.5f));
            snd.CreateBool("sounds", Color.white, GloveSettings.Sounds, v => GloveSettings.Sounds = v);
            snd.CreateFloat("volume", Color.white, GloveSettings.SoundVolume, 0.05f, 0f, 1f,
                v => GloveSettings.SoundVolume = v);
            snd.CreateFunction("reload sounds", Color.white, GloveSound.Reload);

            Page look = root.CreatePage("look", new Color(0.8f, 0.7f, 0.4f));
            look.CreateBool("aim arc", Color.white, GloveSettings.ShowArc, v => GloveSettings.ShowArc = v);
            look.CreateBool("item outline", Color.white, GloveSettings.ShowOutline, v => GloveSettings.ShowOutline = v);
            look.CreateBool("pull trail", Color.white, GloveSettings.ShowTrail, v => GloveSettings.ShowTrail = v);
        }

        public override void OnUpdate()      => GloveManager.OnUpdate();
        public override void OnFixedUpdate() => GloveManager.OnFixedUpdate();
    }
}
