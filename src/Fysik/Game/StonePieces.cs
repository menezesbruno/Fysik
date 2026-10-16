using System;
using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Fysik.Game
{
    internal static class StonePieces
    {
        public static void Add(GameObject prefab, string token, Sprite icon = null)
        {
            var config = new PieceConfig
            {
                Name = "$" + token,
                Description = "$" + token + "_description",
                PieceTable = "Hammer",
            };
            if (icon == null)
                icon = Render(prefab, Quaternion.identity);
            if (icon != null)
                config.Icon = icon;
            PieceManager.Instance.AddPiece(new CustomPiece(prefab, false, config));
        }

        public static Sprite Render(GameObject prefab, Quaternion rotation)
        {
            try
            {
                return RenderManager.Instance.Render(new RenderManager.RenderRequest(prefab) { Rotation = rotation });
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Piece {prefab.name} keeps the stone icon: {e.Message}");
                return null;
            }
        }

        public static Sprite Render(GameObject prefab, Quaternion rotation, IList<MeshFilter> filters, IList<Mesh> shapes)
        {
            var kept = new Mesh[filters.Count];
            for (int i = 0; i < filters.Count; i++)
            {
                kept[i] = filters[i].sharedMesh;
                filters[i].sharedMesh = shapes[i];
            }
            try
            {
                return Render(prefab, rotation);
            }
            finally
            {
                for (int i = 0; i < filters.Count; i++)
                {
                    filters[i].sharedMesh = kept[i];
                    UnityEngine.Object.Destroy(shapes[i]);
                }
            }
        }

        public static void ScaleRecipe(Piece piece, double ratio)
        {
            if (piece == null || piece.m_resources == null)
                return;
            var resources = new Piece.Requirement[piece.m_resources.Length];
            for (int i = 0; i < resources.Length; i++)
            {
                Piece.Requirement r = piece.m_resources[i];
                resources[i] = new Piece.Requirement
                {
                    m_resItem = r.m_resItem,
                    m_amount = Math.Max(1, (int)Math.Round(r.m_amount * ratio, MidpointRounding.AwayFromZero)),
                    m_extraAmountOnlyOneIngredient = r.m_extraAmountOnlyOneIngredient,
                    m_amountPerLevel = r.m_amountPerLevel,
                    m_upgraderResource = r.m_upgraderResource,
                    m_recover = r.m_recover,
                };
            }
            piece.m_resources = resources;
        }

        public static void Place(IEnumerable<string> prefabs, params string[] after)
        {
            PieceTable table = PieceManager.Instance.GetPieceTable(PieceTables.Hammer);
            if (table == null)
                return;
            var moved = new List<GameObject>();
            foreach (string name in prefabs)
            {
                int i = table.m_pieces.FindIndex(p => p != null && p.name == name);
                if (i < 0)
                    continue;
                moved.Add(table.m_pieces[i]);
                table.m_pieces.RemoveAt(i);
            }
            int at = table.m_pieces.Count;
            foreach (string anchor in after)
            {
                int i = table.m_pieces.FindIndex(p => p != null && p.name == anchor);
                if (i < 0)
                    continue;
                at = i + 1;
                break;
            }
            table.m_pieces.InsertRange(at, moved);
        }

        public static Matrix4x4 ToRoot(Transform root, Transform part) => root.worldToLocalMatrix * part.localToWorldMatrix;

        public static void Straighten(WearNTear wnt)
        {
            if (TowerStones.TryGet(wnt.gameObject, out TowerStone _))
                TowerStones.Straighten(wnt.transform);
            else if (ArchStones.TryGet(wnt.gameObject, out ArchStone arch))
                ArchStones.Straighten(wnt.transform, arch);
        }
    }
}
