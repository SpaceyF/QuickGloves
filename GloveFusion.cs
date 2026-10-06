using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Interaction;
using LabFusion.Entities;
using LabFusion.Network;
using UnityEngine;

namespace QuickGloves
{
    // all the Fusion stuff hides in here
    // nobody calls this unless Fusion showed up
    public static class GloveFusion
    {
        // dibs on the prop so everyone sees it
        public static void TakeOwnership(GameObject go)
        {
            try
            {
                if (!NetworkInfo.HasServer) return;
                var entity = go.GetComponentInParent<MarrowEntity>();
                if (entity == null) return;
                if (IMarrowEntityExtender.Cache.TryGet(entity, out NetworkEntity ne) && ne != null && !ne.IsOwner)
                    NetworkEntityManager.TakeOwnership(ne);
            }
            catch { }
        }

        // is it ours yet? is it? now?
        public static bool CanMove(GameObject go)
        {
            try
            {
                if (!NetworkInfo.HasServer) return true;
                var entity = go.GetComponentInParent<MarrowEntity>();
                if (entity == null) return true;
                if (!IMarrowEntityExtender.Cache.TryGet(entity, out NetworkEntity ne) || ne == null) return true;
                return ne.IsOwner;
            }
            catch { return true; }
        }

        // grab it, but everybody sees it
        public static void Attach(Grip grip, Hand hand)
        {
            if (NetworkInfo.HasServer) LabFusion.Extensions.GripExtensions.TryAttach(grip, hand, true);
            else grip.OnGrabConfirm(hand, true);
        }
    }
}
