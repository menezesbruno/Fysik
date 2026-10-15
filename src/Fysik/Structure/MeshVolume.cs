using System.Collections.Generic;

namespace Fysik.Structure
{
    public static class MeshVolume
    {
        public static double Of(IReadOnlyList<Vec3> vertices, IReadOnlyList<int> triangles)
        {
            double volume = 0;
            for (int t = 0; t + 2 < triangles.Count; t += 3)
            {
                Vec3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                volume += Vec3.Dot(a, Vec3.Cross(b, c)) / 6;
            }
            return volume < 0 ? -volume : volume;
        }
    }
}
