using BoneLib;
using UnityEngine;

namespace QuickGloves
{
    // babysits both gloves
    public static class GloveManager
    {
        private static GloveHand? _left, _right;

        // flips on if Fusion showed up
        public static bool FusionAvailable;

        public static void OnLevelLoaded()
        {
            _left?.Reset();
            _right?.Reset();
        }

        private static void Ensure()
        {
            if (_left != null) return;
            _left     = new GloveHand(true);
            _right    = new GloveHand(false);
            _left.Other  = _right;
            _right.Other = _left;
        }

        private static bool Ready => Player.ControllersExist && Player.HandsExist && Player.PhysicsRig != null;

        public static void OnUpdate()
        {
            if (!GloveSettings.Enabled || !Ready)
            {
                _left?.Disable();
                _right?.Disable();
                return;
            }

            Ensure();
            if (GloveSettings.RightHand) _right!.Update(); else _right!.Disable();
            if (GloveSettings.LeftHand)  _left!.Update();  else _left!.Disable();
        }

        public static void OnFixedUpdate()
        {
            if (!GloveSettings.Enabled || _left == null || !Ready) return;
            if (GloveSettings.RightHand) _right!.FixedTick();
            if (GloveSettings.LeftHand)  _left!.FixedTick();
        }
    }
}
