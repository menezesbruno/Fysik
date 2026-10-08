using System.Collections.Generic;
using Fysik.Structure;
using UnityEngine;

namespace Fysik.Game
{
    internal static class StressDisplay
    {
        private static readonly int s_color = Shader.PropertyToID("_Color");
        private static readonly int s_emission = Shader.PropertyToID("_EmissionColor");

        private static readonly float[] s_stopAt = { 0f, 0.33f, 0.66f, 1f };
        private static readonly Color[] s_stopColor =
        {
            new Color32(48, 112, 232, 255),
            new Color32(52, 188, 98, 255),
            new Color32(248, 206, 54, 255),
            new Color32(228, 56, 42, 255),
        };

        public static readonly Color Pending = new Color(0.6f, 0.8f, 1f);

        public static readonly Color Unsupported = new Color(0.85f, 0.2f, 0.85f);

        public static readonly Color World = new Color(0.7f, 0.7f, 0.7f);

        public static Color ForUtilization(double u)
        {
            float t = Mathf.Clamp01((float)u);
            for (int i = 1; i < s_stopAt.Length; i++)
                if (t <= s_stopAt[i])
                    return Color.Lerp(s_stopColor[i - 1], s_stopColor[i], (t - s_stopAt[i - 1]) / (s_stopAt[i] - s_stopAt[i - 1]));
            return s_stopColor[s_stopColor.Length - 1];
        }

        public static Color For(PieceNode node)
        {
            if (node != null && node.Anchored)
                return World;
            if (node == null || !node.HasResult)
                return Pending;
            return node.Result.Mode == StressMode.Unsupported ? Unsupported : ForUtilization(node.Result.Utilization);
        }

        public static void Apply(WearNTear wnt, Color color)
        {
            MaterialMan.instance.SetValue(wnt.gameObject, s_emission, color * 0.4f);
            MaterialMan.instance.SetValue(wnt.gameObject, s_color, color);
        }

        public static void Reset(WearNTear wnt)
        {
            MaterialMan.instance.ResetValue(wnt.gameObject, s_color);
            MaterialMan.instance.ResetValue(wnt.gameObject, s_emission);
        }

        public static Color ForPreview(PreviewResult p)
        {
            if (p.Ghost.Mode == StressMode.Unsupported)
                return Unsupported;
            double u = p.Ghost.Utilization;
            if (p.WorstOther != null && p.WorstOtherResult.Utilization >= 1)
                u = System.Math.Max(u, p.WorstOtherResult.Utilization);
            return ForUtilization(u);
        }

        public static string PreviewText(PreviewResult p, bool pending)
        {
            if (p == null)
                return pending ? $"<color=#{ColorUtility.ToHtmlStringRGB(Pending)}>{L("$fysik_preview")}: {L("$fysik_pending")}</color>" : null;

            string headline = p.Ghost.Mode == StressMode.Unsupported
                ? $"{L("$fysik_preview")}: {L("$fysik_unsupported")}"
                : $"{L("$fysik_preview")}: {L(ModeToken(p.Ghost.Mode))} {Percent(p.Ghost.Utilization)}";
            if (p.Ghost.Mode != StressMode.Unsupported && p.Ghost.Utilization >= 1)
                headline += " · " + L("$fysik_overloaded");
            string text = $"<color=#{ColorUtility.ToHtmlStringRGB(ForPreview(p))}><b>{headline}</b></color>";

            if (p.WorstOther != null && p.WorstOther.Alive && p.WorstOtherResult.Utilization >= 1 &&
                p.WorstOtherResult.Mode != StressMode.Unsupported)
            {
                Piece other = p.WorstOther.Wnt.GetComponent<Piece>();
                string name = other != null ? L(other.m_name) : p.WorstOther.Name;
                text += $"\n<color=#{ColorUtility.ToHtmlStringRGB(ForUtilization(1))}>{L("$fysik_overloads")} {name} " +
                        $"({Percent(p.WorstOtherResult.Utilization)})</color>";
            }
            return text;
        }

        private static string Percent(double u) => $"{Mathf.RoundToInt((float)(u * 100))}%";

        public static string HoverText(PieceNode node)
        {
            if (node == null || !node.Structural)
                return null;
            if (node.Anchored)
                return $"<color=#{ColorUtility.ToHtmlStringRGB(World)}>{L("$fysik_world")}</color>";
            if (!node.HasResult)
                return $"<color=#{ColorUtility.ToHtmlStringRGB(Pending)}>{L("$fysik_pending")}</color>";

            BodyResult r = node.Result;
            Color color = For(node);
            string headline = r.Mode == StressMode.Unsupported
                ? L("$fysik_unsupported")
                : $"{L(ModeToken(r.Mode))} {Mathf.RoundToInt((float)(r.Utilization * 100))}%";
            if (node.CrackTicks != 0)
                headline += " · " + L("$fysik_cracking");
            else if (r.Mode != StressMode.Unsupported && r.Utilization >= 1)
                headline += " · " + L("$fysik_overloaded");

            string details = $"{L("$fysik_mat_" + node.MaterialName.ToLowerInvariant())} · {node.Mass:0} kg";
            if (node.Island != null && node.Island.Solver == SolverKind.LoadPath)
                details += " · " + L("$fysik_loadpath");
            return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}><b>{headline}</b></color>\n<size=75%>{details}</size>";
        }

        private static string ModeToken(StressMode mode)
        {
            switch (mode)
            {
                case StressMode.Compression: return "$fysik_compression";
                case StressMode.Tension: return "$fysik_tension";
                case StressMode.Bending: return "$fysik_bending";
                case StressMode.Shear: return "$fysik_shear";
                case StressMode.Torsion: return "$fysik_torsion";
                default: return "$fysik_stress";
            }
        }

        private static string L(string token) => Localization.instance.Localize(token);
    }

    internal sealed class XRayView
    {
        private const float RefreshInterval = 0.25f;
        private const int MaxRecolorsPerRefresh = 150;
        private const int MaxNewPiecesPerRefresh = 200;

        public static XRayView Instance { get; } = new XRayView();

        private readonly Dictionary<WearNTear, Color> _colored = new Dictionary<WearNTear, Color>();
        private readonly List<WearNTear> _toReset = new List<WearNTear>();
        private float _nextRefresh;

        public bool Active { get; private set; }

        public void Tick()
        {
            if (Player.m_localPlayer == null)
                return;
            if (FysikConfig.DumpKey.Value.IsDown() && !TextInputActive())
                FysikCommand.Dump();
            else if (FysikConfig.XRayKey.Value.IsDown() && !TextInputActive())
                Toggle();
            else if (FysikConfig.XRayRadiusKey.Value.IsDown() && !TextInputActive())
                Widen();
            if (!Active || Time.time < _nextRefresh)
                return;
            _nextRefresh = Time.time + RefreshInterval;
            Refresh(Player.m_localPlayer.transform.position);
        }

        public void Toggle()
        {
            Active = !Active;
            if (!Active)
                Clear();
            _nextRefresh = 0;
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft,
                                          Active ? "$fysik_xray_on" : "$fysik_xray_off");
        }

        public void Widen()
        {
            float current = FysikConfig.XRayRadius.Value;
            float next = Mathf.Floor(current / FysikConfig.XRayRadiusStep) * FysikConfig.XRayRadiusStep + FysikConfig.XRayRadiusStep;
            if (next > FysikConfig.XRayRadiusMax + 0.01f)
                next = FysikConfig.XRayRadiusMin;
            FysikConfig.XRayRadius.Value = next;
            _nextRefresh = 0;
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft,
                                          Localization.instance.Localize("$fysik_xray_radius") + $" {next:0} m");
        }

        public bool Holds(WearNTear wnt) => Active && _colored.ContainsKey(wnt);

        public void Reapply(WearNTear wnt)
        {
            if (_colored.TryGetValue(wnt, out Color color))
                StressDisplay.Apply(wnt, color);
        }

        public void Clear()
        {
            foreach (WearNTear wnt in _colored.Keys)
                if (wnt != null)
                    StressDisplay.Reset(wnt);
            _colored.Clear();
        }

        private void Refresh(Vector3 center)
        {
            float radius = FysikConfig.XRayRadius.Value;
            float r2 = radius * radius;
            int recolors = 0, newPieces = 0;
            StructureManager manager = StructureManager.Instance;

            foreach (WearNTear wnt in WearNTear.GetAllInstances())
            {
                if (wnt == null || (wnt.transform.position - center).sqrMagnitude > r2)
                    continue;
                if (!manager.Knows(wnt) && newPieces++ >= MaxNewPiecesPerRefresh)
                    continue;
                PieceNode node = manager.Request(wnt);
                if (node == null || !node.Structural)
                    continue;
                Color color = StressDisplay.For(node);
                if (_colored.TryGetValue(wnt, out Color current) && current == color)
                    continue;
                if (recolors++ >= MaxRecolorsPerRefresh)
                    break;
                StressDisplay.Apply(wnt, color);
                _colored[wnt] = color;
            }

            _toReset.Clear();
            foreach (WearNTear wnt in _colored.Keys)
                if (wnt == null || (wnt.transform.position - center).sqrMagnitude > r2)
                    _toReset.Add(wnt);
            foreach (WearNTear wnt in _toReset)
            {
                if (wnt != null)
                    StressDisplay.Reset(wnt);
                _colored.Remove(wnt);
            }
        }

        private static bool TextInputActive() =>
            (Chat.instance != null && Chat.instance.HasFocus()) || global::Console.IsVisible() || TextInput.IsVisible() ||
            Menu.IsVisible() || InventoryGui.IsVisible();
    }
}
