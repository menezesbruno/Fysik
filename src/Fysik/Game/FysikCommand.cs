using System;
using System.Globalization;
using System.IO;
using System.Text;
using Jotunn.Entities;
using UnityEngine;

namespace Fysik.Game
{
    internal sealed class FysikCommand : ConsoleCommand
    {
        public override string Name => "fysik";

        public override string Help =>
            "stats | recalc | dump  (dump writes the structure under the hammer to BepInEx/fysik-dump.txt)";

        public override void Run(string[] args)
        {
            string sub = args.Length > 0 ? args[0].ToLowerInvariant() : "stats";
            switch (sub)
            {
                case "stats":
                    Print($"Fysik {MyPluginInfo.PLUGIN_VERSION}: {StructureManager.Instance.NodeCount} pieces cached, " +
                          $"{StructureManager.Instance.PendingRequests} pending, X-ray {(XRayView.Instance.Active ? "on" : "off")}");
                    Print("Last solve: " + StructureManager.Instance.LastSolve);
                    Print(Ownership());
                    break;
                case "recalc":
                    MaterialTable.Invalidate();
                    StructureManager.Instance.InvalidateAllResults();
                    Print("Fysik: every structure will be solved again.");
                    break;
                case "dump":
                    Dump();
                    break;
                default:
                    Print("Usage: fysik " + Help);
                    break;
            }
        }

        private static string Ownership()
        {
            Player player = Player.m_localPlayer;
            if (player == null || ZNet.instance == null)
                return "Ownership: no local player.";
            long me = ZDOMan.GetSessionID();
            ZNetPeer serverPeer = ZNet.instance.IsServer() ? null : ZNet.instance.GetServerPeer();
            long server = serverPeer != null ? serverPeer.m_uid : me;
            int mine = 0, serverOwned = 0, others = 0, nobody = 0;
            foreach (WearNTear wnt in WearNTear.GetAllInstances())
            {
                if (wnt == null || wnt.m_nview == null || !wnt.m_nview.IsValid() ||
                    (wnt.transform.position - player.transform.position).sqrMagnitude > 64f * 64f)
                    continue;
                long owner = wnt.m_nview.GetZDO().GetOwner();
                if (owner == me)
                    mine++;
                else if (owner == 0)
                    nobody++;
                else if (owner == server)
                    serverOwned++;
                else
                    others++;
            }
            return $"Building pieces within 64 m: {mine} yours (you calculate them), {others} other players', " +
                   $"{serverOwned} the server's, {nobody} without owner.";
        }

        internal static void Dump()
        {
            Piece piece = Player.m_localPlayer != null ? Player.m_localPlayer.GetHoveringPiece() : null;
            WearNTear wnt = piece != null ? piece.GetComponent<WearNTear>() : null;
            PieceNode node = StructureManager.Instance.GetNode(wnt);
            if (node?.Island == null)
            {
                Print("Fysik: aim at a solved piece with the hammer first.");
                Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, "Fysik: aim at a piece with the hammer");
                return;
            }

            CultureInfo inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            Island island = node.Island;
            sb.AppendLine($"Fysik {MyPluginInfo.PLUGIN_VERSION} dump, {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Island: {island.Members.Count} pieces, solver {island.Solver}{(island.Stale ? " (stale)" : "")}");
            sb.AppendLine("Last solve: " + StructureManager.Instance.LastSolve);
            sb.AppendLine();
            sb.AppendLine("# index; prefab; zdo; material; mass kg; center; size; rotation (euler, degrees); ground points; " +
                          "mode; utilization; axial N; moment N·m; torque N·m; carried kg; holds kg; snow kg; riding kg");
            for (int i = 0; i < island.Members.Count; i++)
            {
                PieceNode m = island.Members[i];
                Obb box = m.Geometry.Main;
                sb.AppendLine(string.Format(inv, "{0}; {1}; {2}; {3}; {4:0.0}; {5}; {6}; {7}; {8}; {9}; {10:0.000}; {11:0}; {12:0}; {13:0}; {14:0.0}; {15:0.0}; {16:0.0}; {17:0.0}",
                    i, m.Name, m.Id, m.MaterialName, m.Mass, Format(box.Center), Format(box.Size),
                    Format(box.Rotation.eulerAngles), m.GroundPoints.Count, m.Result.Mode, m.Result.Utilization,
                    m.Result.AxialForce, m.Result.BendingMoment, m.Result.Torque, m.Carried, m.Holds, m.Snow, m.Riding));
            }
            sb.AppendLine();
            sb.AppendLine("# joints: index; index; point");
            for (int i = 0; i < island.Members.Count; i++)
                foreach (Contact c in island.Members[i].Contacts)
                {
                    int j = island.Members.IndexOf(c.Other);
                    if (j > i)
                        sb.AppendLine($"{i}; {j}; {Format(c.Point)}");
                }

            string path = Path.Combine(BepInEx.Paths.BepInExRootPath, "fysik-dump.txt");
            File.WriteAllText(path, sb.ToString());
            Print($"Fysik: {island.Members.Count} pieces written to {path}");
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, $"Fysik: {island.Members.Count} pieces saved to fysik-dump.txt");
        }

        private static string Format(Vector3 v) =>
            string.Format(CultureInfo.InvariantCulture, "({0:0.###} {1:0.###} {2:0.###})", v.x, v.y, v.z);

        private static void Print(string text)
        {
            global::Console.instance?.Print(text);
            Plugin.Log.LogInfo(text);
        }
    }
}
