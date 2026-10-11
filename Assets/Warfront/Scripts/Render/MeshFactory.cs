using System.Collections.Generic;
using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>
    /// Procedural mesh generation. WARFRONT's environment art is generated from source: no imported
    /// models, no licensing questions, and the geometry is deliberately low poly for mobile GPUs.
    /// Every mesh is cached by key so a level build creates each shape once.
    /// </summary>
    public static class MeshFactory
    {
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>(32);

        public static void ClearCache()
        {
            Cache.Clear();
        }

        public static Mesh Box()
        {
            return Cached("box", BuildBox);
        }

        public static Mesh Ramp()
        {
            return Cached("ramp", BuildRamp);
        }

        public static Mesh Cylinder(int segments)
        {
            int clamped = Mathf.Clamp(segments, 6, 24);
            return Cached("cylinder_" + clamped, () => BuildCylinder(clamped));
        }

        public static Mesh Cone(int segments)
        {
            int clamped = Mathf.Clamp(segments, 6, 24);
            return Cached("cone_" + clamped, () => BuildCone(clamped));
        }

        public static Mesh Rock(int seed)
        {
            int variant = Mathf.Abs(seed) % 4;
            return Cached("rock_" + variant, () => BuildRock(2000 + variant));
        }

        public static Mesh WallPanel()
        {
            return Cached("wall_panel", BuildWallPanel);
        }

        private static Mesh Cached(string key, System.Func<Mesh> factory)
        {
            Mesh existing;
            if (Cache.TryGetValue(key, out existing) && existing != null) return existing;
            Mesh mesh = factory();
            Cache[key] = mesh;
            return mesh;
        }

        private static Mesh BuildBox()
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(temp);
            return mesh;
        }

        private static Mesh BuildRamp()
        {
            // Unit sized ramp: 1 wide, 1 long, 1 high (scaled by the caller).
            Vector3[] vertices =
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f),
                new Vector3(-0.5f, 1f, -0.5f),
                new Vector3(0.5f, 1f, -0.5f)
            };

            int[] triangles =
            {
                0, 4, 1, 1, 4, 5,       // back face
                0, 1, 2, 2, 1, 3,       // bottom
                2, 3, 4, 4, 3, 5,       // slope
                0, 2, 4,                // left
                1, 5, 3                 // right
            };

            Vector2[] uvs =
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f),
                new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f)
            };

            Mesh mesh = new Mesh();
            mesh.name = "WarfrontRamp";
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildCylinder(int segments)
        {
            List<Vector3> vertices = new List<Vector3>(segments * 4 + 2);
            List<int> triangles = new List<int>(segments * 12);
            List<Vector2> uvs = new List<Vector2>(segments * 4 + 2);

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = t * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * 0.5f;
                float z = Mathf.Sin(angle) * 0.5f;
                vertices.Add(new Vector3(x, -0.5f, z));
                uvs.Add(new Vector2(t, 0f));
                vertices.Add(new Vector3(x, 0.5f, z));
                uvs.Add(new Vector2(t, 1f));
            }

            for (int i = 0; i < segments; i++)
            {
                int baseIndex = i * 2;
                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 3);
            }

            int bottomCenter = vertices.Count;
            vertices.Add(new Vector3(0f, -0.5f, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));
            int topCenter = vertices.Count;
            vertices.Add(new Vector3(0f, 0.5f, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));

            for (int i = 0; i < segments; i++)
            {
                int baseIndex = i * 2;
                int next = ((i + 1) % segments) * 2;
                triangles.Add(bottomCenter);
                triangles.Add(next);
                triangles.Add(baseIndex);
                triangles.Add(topCenter);
                triangles.Add(baseIndex + 1);
                triangles.Add(next + 1);
            }

            Mesh mesh = new Mesh();
            mesh.name = "WarfrontCylinder";
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildCone(int segments)
        {
            List<Vector3> vertices = new List<Vector3>(segments + 2);
            List<int> triangles = new List<int>(segments * 3);
            List<Vector2> uvs = new List<Vector2>(segments + 2);

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = t * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * 0.5f, 0f, Mathf.Sin(angle) * 0.5f));
                uvs.Add(new Vector2(t, 0f));
            }
            vertices.Add(new Vector3(0f, 1f, 0f));
            uvs.Add(new Vector2(0.5f, 1f));

            for (int i = 0; i < segments; i++)
            {
                triangles.Add(i);
                triangles.Add(segments + 1);
                triangles.Add(i + 1);
            }

            Mesh mesh = new Mesh();
            mesh.name = "WarfrontCone";
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildRock(int seed)
        {
            // A displaced low poly sphere reads as a boulder and costs almost nothing on mobile.
            int rings = 4;
            int segments = 8;
            List<Vector3> vertices = new List<Vector3>((rings + 1) * (segments + 1));
            List<int> triangles = new List<int>(rings * segments * 6);
            List<Vector2> uvs = new List<Vector2>((rings + 1) * (segments + 1));
            WfRandom rng = new WfRandom(seed);

            for (int ring = 0; ring <= rings; ring++)
            {
                float v = (float)ring / rings;
                float phi = v * Mathf.PI;
                for (int segment = 0; segment <= segments; segment++)
                {
                    float u = (float)segment / segments;
                    float theta = u * Mathf.PI * 2f;
                    float radius = 0.5f * (0.78f + rng.Range(0f, 0.42f));
                    float x = Mathf.Sin(phi) * Mathf.Cos(theta) * radius;
                    float y = Mathf.Cos(phi) * radius * 0.7f;
                    float z = Mathf.Sin(phi) * Mathf.Sin(theta) * radius;
                    vertices.Add(new Vector3(x, y + 0.35f, z));
                    uvs.Add(new Vector2(u, v));
                }
            }

            for (int ring = 0; ring < rings; ring++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    int a = ring * (segments + 1) + segment;
                    int b = a + segments + 1;
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(a + 1);
                    triangles.Add(a + 1);
                    triangles.Add(b);
                    triangles.Add(b + 1);
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "WarfrontRock";
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildWallPanel()
        {
            // Slightly bevelled panel used for building facades and containers.
            float b = 0.06f;
            Vector3[] vertices =
            {
                new Vector3(-0.5f + b, -0.5f, -0.5f), new Vector3(0.5f - b, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f + b, -0.5f), new Vector3(0.5f, 0.5f - b, -0.5f),
                new Vector3(0.5f - b, 0.5f, -0.5f), new Vector3(-0.5f + b, 0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f - b, -0.5f), new Vector3(-0.5f, -0.5f + b, -0.5f),
                new Vector3(-0.5f + b, -0.5f, 0.5f), new Vector3(0.5f - b, -0.5f, 0.5f),
                new Vector3(0.5f, -0.5f + b, 0.5f), new Vector3(0.5f, 0.5f - b, 0.5f),
                new Vector3(0.5f - b, 0.5f, 0.5f), new Vector3(-0.5f + b, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f - b, 0.5f), new Vector3(-0.5f, -0.5f + b, 0.5f)
            };

            int[] front = { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5, 0, 5, 6, 0, 6, 7 };
            int[] back = { 8, 10, 9, 8, 11, 10, 8, 12, 11, 8, 13, 12, 8, 14, 13, 8, 15, 14 };
            List<int> triangles = new List<int>(front.Length + back.Length);
            triangles.AddRange(front);
            triangles.AddRange(back);

            Vector2[] uvs = new Vector2[vertices.Length];
            for (int i = 0; i < uvs.Length; i++)
            {
                uvs[i] = new Vector2(vertices[i].x + 0.5f, vertices[i].y + 0.5f);
            }

            Mesh mesh = new Mesh();
            mesh.name = "WarfrontWallPanel";
            mesh.vertices = vertices;
            mesh.SetTriangles(triangles, 0);
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
