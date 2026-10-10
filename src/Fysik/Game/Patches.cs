using System;
using HarmonyLib;
using UnityEngine;

namespace Fysik.Game
{
    internal static class Patches
    {
        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Awake))]
        private static class PieceAwake
        {
            private static void Postfix(WearNTear __instance)
            {
                try { Hooks.PieceAppeared(__instance); }
                catch (Exception e) { Guard.Report("Piece tracking (placed)", e); }
            }
        }

        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.OnDestroy))]
        private static class PieceDestroyed
        {
            private static void Prefix(WearNTear __instance)
            {
                try { StructureManager.Instance.OnPieceDestroyed(__instance); }
                catch (Exception e) { Guard.Report("Piece tracking (removed)", e); }
            }
        }

        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.RPC_HealthChanged))]
        private static class HealthChanged
        {
            private static void Postfix(WearNTear __instance)
            {
                try { StructureManager.Instance.OnPieceHealthChanged(__instance); }
                catch (Exception e) { Guard.Report("Piece tracking (health)", e); }
            }
        }

        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Highlight))]
        private static class Highlight
        {
            private static bool Prefix(WearNTear __instance)
            {
                try { return !Hooks.Highlight(__instance); }
                catch (Exception e) { Guard.Report("Hammer highlight", e); return true; }
            }
        }

        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.ResetHighlight))]
        private static class ResetHighlight
        {
            private static bool Prefix(WearNTear __instance)
            {
                try { return !Hooks.KeepXRayColor(__instance); }
                catch (Exception e) { Guard.Report("X-ray highlight reset", e); return true; }
            }
        }

        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.HaveSupport))]
        private static class HaveSupport
        {
            private static bool Prefix(WearNTear __instance, ref bool __result)
            {
                try
                {
                    if (Hooks.VanillaSupportDecides(__instance))
                        return true;
                    __result = true;
                    return false;
                }
                catch (Exception e) { Guard.Report("Vanilla support switch", e); return true; }
            }
        }

        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.GetSupportColorValue))]
        private static class SnowSupport
        {
            private static bool Prefix(WearNTear __instance, ref float __result)
            {
                try
                {
                    if (Hooks.VanillaSupportDecides(__instance))
                        return true;
                    __result = -1f;
                    return false;
                }
                catch (Exception e) { Guard.Report("Vanilla snow damage switch", e); return true; }
            }
        }

        [HarmonyPatch(typeof(Container), nameof(Container.Load))]
        private static class ContainerLoaded
        {
            private static void Postfix(Container __instance, bool __result)
            {
                try
                {
                    if (__result)
                        StructureManager.Instance.OnContainerChanged(__instance);
                }
                catch (Exception e) { Guard.Report("Chest contents (loaded)", e); }
            }
        }

        [HarmonyPatch(typeof(Container), nameof(Container.Save))]
        private static class ContainerSaved
        {
            private static void Postfix(Container __instance)
            {
                try { StructureManager.Instance.OnContainerChanged(__instance); }
                catch (Exception e) { Guard.Report("Chest contents (saved)", e); }
            }
        }

        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.RPC_CreateFragments))]
        private static class CreateFragments
        {
            private static bool Prefix(WearNTear __instance)
            {
                try { return !FallingDebris.Instance.TryTakeOver(__instance); }
                catch (Exception e) { Guard.Report("Collapse animation", e); return true; }
            }
        }

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
        private static class RegisterRpcs
        {
            private static void Postfix()
            {
                try { FallingDebris.Instance.RegisterRpc(); }
                catch (Exception e) { Guard.Report("RPC registration", e); }
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.UpdatePlacementGhost))]
        private static class PlacementGhost
        {
            private static void Postfix(Player __instance)
            {
                try { Hooks.PlacementGhost(__instance); }
                catch (Exception e) { Guard.Report("Placement preview", e); }
            }
        }

        [HarmonyPatch(typeof(Hud), nameof(Hud.UpdateCrosshair))]
        private static class Crosshair
        {
            private static void Postfix(Hud __instance, Player player)
            {
                try { Hooks.Crosshair(__instance, player); }
                catch (Exception e) { Guard.Report("Crosshair stress text", e); }
            }
        }
    }

    internal static class Hooks
    {
        public static void PieceAppeared(WearNTear wnt)
        {
            if (wnt.m_nview != null && wnt.m_nview.GetZDO() != null)
                StructureManager.Instance.OnPieceAppeared(wnt);
        }

        public static bool Highlight(WearNTear wnt)
        {
            if (!FysikConfig.HammerInfo.Value)
                return false;
            PieceNode node = StructureManager.Instance.Request(wnt);
            if (node == null || !node.Structural)
                return false;
            StressDisplay.Apply(wnt, StressDisplay.For(node));
            wnt.CancelInvoke(nameof(WearNTear.ResetHighlight));
            wnt.Invoke(nameof(WearNTear.ResetHighlight), 0.2f);
            return true;
        }

        public static bool KeepXRayColor(WearNTear wnt)
        {
            if (!XRayView.Instance.Holds(wnt))
                return false;
            XRayView.Instance.Reapply(wnt);
            return true;
        }

        public static bool VanillaSupportDecides(WearNTear wnt) =>
            FysikConfig.Mode.Value == StructureMode.DisplayOnly ||
            (FysikConfig.Mode.Value == StructureMode.Physics && PieceNode.KeepsVanilla(wnt));

        public static void PlacementGhost(Player player)
        {
            StructureManager manager = StructureManager.Instance;
            WearNTear ghost = ActiveGhost(player);
            if (ghost == null)
            {
                manager.ClearPreview();
                return;
            }
            manager.RequestPreview(ghost);
            if (manager.Preview != null && player.m_placementStatus == Player.PlacementStatus.Valid)
                StressDisplay.Apply(ghost, StressDisplay.ForPreview(manager.Preview));
        }

        public static void Crosshair(Hud hud, Player player)
        {
            string text = null;
            StructureManager manager = StructureManager.Instance;
            WearNTear ghost = ActiveGhost(player);
            if (ghost != null)
                text = StressDisplay.PreviewText(manager.Preview, manager.PreviewPending, StressDisplay.PieceName(ghost));

            Piece piece = FysikConfig.HammerInfo.Value ? player.GetHoveringPiece() : null;
            WearNTear wnt = piece != null ? piece.GetComponent<WearNTear>() : null;
            PieceNode node = wnt != null ? manager.Request(wnt) : null;
            string hover = StressDisplay.HoverText(node);
            if (!string.IsNullOrEmpty(hover))
                text = string.IsNullOrEmpty(text) ? hover : text + "\n" + hover;
            Overlay.Instance.Show(hud, Overlay.CanShow(hud) ? text : null);
        }

        private static WearNTear ActiveGhost(Player player)
        {
            if (!FysikConfig.PlacementPreview.Value || !player.InPlaceMode())
                return null;
            GameObject ghost = player.m_placementGhost;
            return ghost != null && ghost.activeSelf ? ghost.GetComponent<WearNTear>() : null;
        }
    }
}
