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
            "stats | recalc | dump | hash  (dump writes the structure under the hammer to BepInEx/fysik-dump.txt; " +
            "hash prints a fingerprint of its result, the same on every player's game when they agree)";

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
                case "hash":
                    PrintHash();
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

        private static Island AimedIsland()
        {
            Piece piece = Player.m_localPlayer != null ? Player.m_localPlayer.GetHoveringPiece() : null;
            WearNTear wnt = piece != null ? piece.GetComponent<WearNTear>() : null;
            PieceNode node = StructureManager.Instance.GetNode(wnt);
            if (node?.Island != null)
                return node.Island;
            Print("Fysik: aim at a solved piece with the hammer first.");
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, "Fysik: aim at a piece with the hammer");
            return null;
        }

        internal static ulong Hash(Island island)
        {
            ulong hash = 14695981039346656037UL;
            Mix(ref hash, (ulong)island.Members.Count);
            foreach (PieceNode m in island.Members)
            {
                Mix(ref hash, (ulong)m.Id.UserID);
                Mix(ref hash, m.Id.ID);
                Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(m.Result.Utilization));
                Mix(ref hash, (ulong)m.Result.Mode);
            }
            return hash;
        }

        private static void Mix(ref ulong hash, ulong value)
        {
            for (int i = 0; i < 8; i++)
            {
                hash ^= (value >> (8 * i)) & 0xff;
                hash *= 1099511628211UL;
            }
        }

        private static string Describe(Island island)
        {
            PieceNode worst = null;
            foreach (PieceNode m in island.Members)
                if (worst == null || m.Result.Utilization > worst.Result.Utilization)
                    worst = m;
            string text = $"{island.Members.Count} pieces, hash {Hash(island):X16}";
            if (worst != null)
                text += $", worst {worst.Name} {StructureManager.Describe(worst.Result)}";
            return island.Stale || island.LoadsChanged ? text + " (recalculating)" : text;
        }

        private static void PrintHash()
        {
            Island island = AimedIsland();
            if (island == null)
                return;
            string text = "Fysik: " + Describe(island);
            Print(text);
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, text);
        }

        internal static void Dump()
        {
            Island island = AimedIsland();
            if (island == null)
                return;

            CultureInfo inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine($"Fysik {MyPluginInfo.PLUGIN_VERSION} dump, {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Island: {island.Members.Count} pieces, solver {island.Solver}{(island.Stale ? " (stale)" : "")}");
            sb.AppendLine("Result: " + Describe(island));
            sb.AppendLine("Last solve: " + StructureManager.Instance.LastSolve);
            sb.AppendLine();
            sb.AppendLine("# index; prefab; zdo; material; mass kg; center; size; rotation (euler, degrees); ground points; " +
                          "mode; utilization; axial N; moment N·m; torque N·m; carried kg; holds kg; snow kg; riding kg; rests on");
            for (int i = 0; i < island.Members.Count; i++)
            {
                PieceNode m = island.Members[i];
                Obb box = m.Geometry.Main;
                sb.AppendLine(string.Format(inv, "{0}; {1}; {2}; {3}; {4:0.0}; {5}; {6}; {7}; {8}; {9}; {10:0.000}; {11:0}; {12:0}; {13:0}; {14:0.0}; {15:0.0}; {16:0.0}; {17:0.0}; {18}",
                    i, m.Name, m.Id, m.MaterialName, m.Mass, Format(box.Center), Format(box.Size),
                    Format(box.Rotation.eulerAngles), m.GroundContacts.Count, m.Result.Mode, m.Result.Utilization,
                    m.Result.AxialForce, m.Result.BendingMoment, m.Result.Torque, m.Carried, m.Holds, m.Snow, m.Riding,
                    m.RestsOn));
            }
            sb.AppendLine();
            sb.AppendLine("# joints: index; index; point; span on the first (x, y, z: from to); span on the second");
            for (int i = 0; i < island.Members.Count; i++)
                foreach (Contact c in island.Members[i].Contacts)
                {
                    int j = island.Members.IndexOf(c.Other);
                    if (j > i)
                        sb.AppendLine($"{i}; {j}; {Format(c.Point)}; {Format(c.Span)}; {Format(c.OtherSpan)}");
                }
            sb.AppendLine();
            sb.AppendLine("# ground: index; point; span (x, y, z: from to)");
            for (int i = 0; i < island.Members.Count; i++)
                foreach (GroundContact g in island.Members[i].GroundContacts)
                    sb.AppendLine($"{i}; {Format(g.Point)}; {Format(g.Span)}");

            string path = Path.Combine(BepInEx.Paths.BepInExRootPath, "fysik-dump.txt");
            File.WriteAllText(path, sb.ToString());
            Print($"Fysik: {island.Members.Count} pieces written to {path}");
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, $"Fysik: {island.Members.Count} pieces saved to fysik-dump.txt");
        }

        private static string Format(Vector3 v) =>
            string.Format(CultureInfo.InvariantCulture, "({0:0.###} {1:0.###} {2:0.###})", v.x, v.y, v.z);

        private static string Format(double[] span) =>
            span == null
                ? "point"
                : string.Format(CultureInfo.InvariantCulture, "({0:0.###} {1:0.###} {2:0.###} {3:0.###} {4:0.###} {5:0.###})",
                                span[0], span[1], span[2], span[3], span[4], span[5]);

        private static void Print(string text)
        {
            global::Console.instance?.Print(text);
            Plugin.Log.LogInfo(text);
        }
    }
}
