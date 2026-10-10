using System.Collections.Generic;
using Fysik.Structure;
using UnityEngine;

namespace Fysik.Game
{
    internal struct Obb
    {
        public Vector3 Center;
        public Quaternion Rotation;
        public Vector3 Size;

        public float Volume => Size.x * Size.y * Size.z;

        public Vector3 ClosestPoint(Vector3 point)
        {
            Vector3 local = Quaternion.Inverse(Rotation) * (point - Center);
            Vector3 half = Size * 0.5f;
            local.x = Mathf.Clamp(local.x, -half.x, half.x);
            local.y = Mathf.Clamp(local.y, -half.y, half.y);
            local.z = Mathf.Clamp(local.z, -half.z, half.z);
            return Center + Rotation * local;
        }

        public OrientedBox Grown(float margin) => new OrientedBox
        {
            Center = PieceGeometry.ToVec(Center),
            AxisX = PieceGeometry.ToVec(Rotation * Vector3.right),
            AxisY = PieceGeometry.ToVec(Rotation * Vector3.up),
            AxisZ = PieceGeometry.ToVec(Rotation * Vector3.forward),
            Half = PieceGeometry.ToVec(Size * 0.5f + Vector3.one * margin),
        };

        public static Obb Of(Collider collider)
        {
            Transform t = collider.transform;
            switch (collider)
            {
                case BoxCollider box:
                    return new Obb
                    {
                        Center = t.TransformPoint(box.center),
                        Rotation = t.rotation,
                        Size = Abs(Vector3.Scale(box.size, t.lossyScale)),
                    };
                case CapsuleCollider capsule:
                {
                    Vector3 s = Abs(t.lossyScale);
                    int axis = Mathf.Clamp(capsule.direction, 0, 2);
                    float radius = capsule.radius * Mathf.Max(s[(axis + 1) % 3], s[(axis + 2) % 3]);
                    Vector3 size = Vector3.one * (2 * radius);
                    size[axis] = Mathf.Max(capsule.height * s[axis], 2 * radius);
                    return new Obb { Center = t.TransformPoint(capsule.center), Rotation = t.rotation, Size = size };
                }
                case SphereCollider sphere:
                {
                    Vector3 s = Abs(t.lossyScale);
                    float diameter = 2 * sphere.radius * Mathf.Max(s.x, Mathf.Max(s.y, s.z));
                    return new Obb { Center = t.TransformPoint(sphere.center), Rotation = t.rotation, Size = Vector3.one * diameter };
                }
                case MeshCollider mesh when mesh.sharedMesh != null:
                    Bounds b = mesh.sharedMesh.bounds;
                    return new Obb
                    {
                        Center = t.TransformPoint(b.center),
                        Rotation = t.rotation,
                        Size = Abs(Vector3.Scale(b.size, t.lossyScale)),
                    };
                default:
                    return new Obb { Center = collider.bounds.center, Rotation = Quaternion.identity, Size = collider.bounds.size };
            }
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }

    internal struct Recipe
    {
        public static readonly string[] WoodItems =
            { "Wood", "FineWood", "RoundLog", "ElderBark", "YggdrasilWood", "Blackwood", "Frostwood" };
        public static readonly string[] StoneItems = { "Stone", "Grausten", "BlackMarble", "Obsidian" };
        public static readonly string[] MetalBars =
            { "Iron", "Copper", "Bronze", "Tin", "Silver", "BlackMetal", "Flametal", "FlametalNew", "Gold" };
        public static readonly string[] Nails = { "IronNails", "BronzeNails" };

        private static readonly HashSet<string> s_wood = new HashSet<string>(WoodItems);
        private static readonly HashSet<string> s_stone = new HashSet<string>(StoneItems);
        private static readonly HashSet<string> s_metal = new HashSet<string>(MetalBars);
        private static readonly HashSet<string> s_nails = new HashSet<string>(Nails);

        public double Wood, Stone, Metal, Nail, ItemWeight, Stored;

        public bool Pile;

        public double Mass => Pile
            ? Stored * FysikConfig.ContentsWeight.Value
            : Wood * FysikConfig.WoodWeight.Value + Stone * FysikConfig.StoneWeight.Value +
              Metal * FysikConfig.MetalBarWeight.Value + Nail * FysikConfig.NailWeight.Value +
              ItemWeight * FysikConfig.KilogramsPerItemWeight.Value;

        public static Recipe Of(Piece piece)
        {
            var recipe = new Recipe();
            if (piece == null || piece.m_resources == null)
                return recipe;
            recipe.Pile = (piece.m_usage & Piece.UsageTagFlags.Stacks) != 0;
            foreach (Piece.Requirement r in piece.m_resources)
            {
                if (r == null || r.m_resItem == null)
                    continue;
                if (r.m_resItem.m_itemData?.m_shared != null)
                    recipe.Stored += r.m_resItem.m_itemData.m_shared.m_weight * r.m_amount;
                string name = r.m_resItem.gameObject.name;
                if (s_wood.Contains(name))
                    recipe.Wood += r.m_amount;
                else if (s_stone.Contains(name))
                    recipe.Stone += r.m_amount;
                else if (s_metal.Contains(name))
                    recipe.Metal += r.m_amount;
                else if (s_nails.Contains(name))
                    recipe.Nail += r.m_amount;
                else if (r.m_resItem.m_itemData?.m_shared != null)
                    recipe.ItemWeight += r.m_resItem.m_itemData.m_shared.m_weight * r.m_amount;
            }
            return recipe;
        }
    }

    internal sealed class PieceGeometry
    {
        private const float MinExtent = 0.05f;

        private const float MeshFill = 0.6f;

        private const Piece.UsageTagFlags StructureTags =
            Piece.UsageTagFlags.Building | Piece.UsageTagFlags.Floor | Piece.UsageTagFlags.Wall | Piece.UsageTagFlags.Roof |
            Piece.UsageTagFlags.Architecture | Piece.UsageTagFlags.Stairs | Piece.UsageTagFlags.Doors;

        private const Piece.UsageTagFlags ObjectTags =
            Piece.UsageTagFlags.Crafting | Piece.UsageTagFlags.Furniture | Piece.UsageTagFlags.Lighting | Piece.UsageTagFlags.Decor |
            Piece.UsageTagFlags.Storage | Piece.UsageTagFlags.Transport | Piece.UsageTagFlags.Food | Piece.UsageTagFlags.Meads |
            Piece.UsageTagFlags.Feasts | Piece.UsageTagFlags.Defense | Piece.UsageTagFlags.Seasonal;

        public readonly List<Collider> Colliders = new List<Collider>();
        public readonly List<Obb> Boxes = new List<Obb>();
        public Obb Main;
        public float Volume;
        public bool WeighsRecipe;
        public Recipe Recipe;
        public double SnowArea;
        public string Summary;

        public double MaterialsMass => WeighsRecipe ? Recipe.Mass : 0;

        public bool IsValid => Boxes.Count > 0;

        private static readonly List<Collider> s_temp = new List<Collider>();
        private static readonly List<Obb> s_solid = new List<Obb>();

        public static PieceGeometry Of(WearNTear wnt)
        {
            var g = new PieceGeometry();
            s_temp.Clear();
            wnt.GetComponentsInChildren(false, s_temp);
            float largest = 0;
            int largestIndex = -1;
            int boxes = 0, capsules = 0, spheres = 0, meshes = 0, others = 0;
            s_solid.Clear();

            bool hasBoxes = false;
            foreach (Collider c in s_temp)
                hasBoxes |= c is BoxCollider && c.enabled && !c.isTrigger && c.attachedRigidbody == null;

            foreach (Collider c in s_temp)
            {
                if (!c.enabled || c.isTrigger || c.attachedRigidbody != null)
                    continue;
                Obb box = Obb.Of(c);
                float volume = hasBoxes && c is MeshCollider ? 0f : SolidVolume(c, box);
                switch (c)
                {
                    case BoxCollider _: boxes++; break;
                    case CapsuleCollider _: capsules++; break;
                    case SphereCollider _: spheres++; break;
                    case MeshCollider _: meshes++; break;
                    default: others++; break;
                }
                g.Colliders.Add(c);
                g.Boxes.Add(box);
                g.Volume += volume;
                if (volume > 0)
                    s_solid.Add(box);
                if (volume > largest)
                {
                    largest = volume;
                    largestIndex = g.Boxes.Count - 1;
                }
            }
            if (g.Boxes.Count == 0 || s_solid.Count == 0)
                return new PieceGeometry();

            if (largest >= 0.6f * g.Volume)
                g.Main = g.Boxes[largestIndex];
            else
                g.Main = Enclose(s_solid, wnt.transform.rotation);

            g.Main.Size = Vector3.Max(g.Main.Size, Vector3.one * MinExtent);
            g.SnowArea = Loads.SnowArea(ToVec(g.Main.Rotation * Vector3.right), ToVec(g.Main.Rotation * Vector3.up),
                                        ToVec(g.Main.Rotation * Vector3.forward), ToVec(g.Main.Size));
            Piece piece = wnt.GetComponent<Piece>();
            g.WeighsRecipe = piece != null && IsObject(piece);
            if (g.WeighsRecipe)
                g.Recipe = Recipe.Of(piece);
            g.Volume = Mathf.Max(g.Volume, g.Main.Volume * 0.05f);
            g.Summary = $"colliders box {boxes}, capsule {capsules}, sphere {spheres}, mesh {meshes}, other {others}; " +
                        $"main box {g.Main.Size.x:0.00} x {g.Main.Size.y:0.00} x {g.Main.Size.z:0.00} m; volume {g.Volume:0.000} m³";
            return g;
        }

        public static List<Obb> VanillaSupportBoxes(Collider[] colliders)
        {
            var boxes = new List<Obb>();
            foreach (Collider c in colliders)
            {
                if (!c.enabled || !c.gameObject.activeInHierarchy || c.isTrigger || c.attachedRigidbody != null)
                    continue;
                Obb box = c is BoxCollider
                    ? Obb.Of(c)
                    : new Obb { Center = c.bounds.center, Rotation = Quaternion.identity, Size = c.bounds.size };
                box.Size += Vector3.one * 0.3f;
                boxes.Add(box);
            }
            return boxes;
        }

        private static float SolidVolume(Collider c, Obb box)
        {
            Vector3 s = box.Size;
            switch (c)
            {
                case BoxCollider _:
                    return box.Volume;
                case CapsuleCollider _:
                {
                    float length = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
                    float r = Mathf.Min(s.x, Mathf.Min(s.y, s.z)) * 0.5f;
                    return Mathf.PI * r * r * Mathf.Max(0, length - 2 * r) + 4f / 3f * Mathf.PI * r * r * r;
                }
                case SphereCollider _:
                    return Mathf.PI / 6f * s.x * s.x * s.x;
                default:
                    return box.Volume * MeshFill;
            }
        }

        private static Obb Enclose(List<Obb> boxes, Quaternion frame)
        {
            Quaternion inverse = Quaternion.Inverse(frame);
            Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
            foreach (Obb b in boxes)
            {
                Vector3 h = b.Size * 0.5f;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
                    Vector3 local = inverse * (b.Center + b.Rotation * corner);
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }
            }
            return new Obb { Center = frame * ((min + max) * 0.5f), Rotation = frame, Size = max - min };
        }

        public double MassFor(MaterialProps material) => MaterialsMass > 0 ? MaterialsMass : material.Density * Volume;

        public Body ToBody(MaterialProps material) => new Body
        {
            Center = ToVec(Main.Center),
            AxisX = ToVec(Main.Rotation * Vector3.right),
            AxisY = ToVec(Main.Rotation * Vector3.up),
            AxisZ = ToVec(Main.Rotation * Vector3.forward),
            Size = ToVec(Main.Size),
            Mass = MassFor(material),
            Material = material,
        };

        private static bool IsObject(Piece piece)
        {
            if ((piece.m_usage & Piece.UsageTagFlags.Stacks) != 0)
                return true;
            if ((piece.m_usage & StructureTags) != 0)
                return false;
            if ((piece.m_usage & ObjectTags) != 0)
                return true;
            switch (piece.m_category)
            {
                case Piece.PieceCategory.Crafting:
                case Piece.PieceCategory.Furniture:
                case Piece.PieceCategory.Feasts:
                case Piece.PieceCategory.Food:
                case Piece.PieceCategory.Meads:
                    return true;
                default:
                    return false;
            }
        }

        public static Vec3 ToVec(Vector3 v) => new Vec3(v.x, v.y, v.z);
    }
}
