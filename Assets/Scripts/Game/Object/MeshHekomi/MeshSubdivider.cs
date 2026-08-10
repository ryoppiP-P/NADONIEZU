using System.Collections.Generic;
using UnityEngine;

public static class MeshSubdivider {
    public static Mesh Subdivide(Mesh source, int level) {
        Mesh mesh = Object.Instantiate(source);
        for (int i = 0; i < level; i++) {
            SubdivideOnce(mesh);
        }
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void SubdivideOnce(Mesh mesh) {
        Vector3[] oldVerts = mesh.vertices;
        Vector3[] oldNormals = mesh.normals;
        Vector2[] oldUV = mesh.uv;
        int subMeshCount = mesh.subMeshCount;

        List<Vector3> newVerts = new List<Vector3>(oldVerts);
        List<Vector3> newNormals = oldNormals != null && oldNormals.Length == oldVerts.Length
            ? new List<Vector3>(oldNormals) : null;
        List<Vector2> newUV = oldUV != null && oldUV.Length == oldVerts.Length
            ? new List<Vector2>(oldUV) : null;

        Dictionary<long, int> midCache = new Dictionary<long, int>();

        // SubmeshÇ≤Ç∆ÇÃêVtrianglesîzóÒÇçÏÇÈ
        int[][] newSubTris = new int[subMeshCount][];

        for (int s = 0; s < subMeshCount; s++) {
            int[] oldTris = mesh.GetTriangles(s);
            List<int> subNewTris = new List<int>();

            for (int i = 0; i < oldTris.Length; i += 3) {
                int a = oldTris[i];
                int b = oldTris[i + 1];
                int c = oldTris[i + 2];

                int ab = GetMidpoint(a, b, newVerts, newNormals, newUV, midCache);
                int bc = GetMidpoint(b, c, newVerts, newNormals, newUV, midCache);
                int ca = GetMidpoint(c, a, newVerts, newNormals, newUV, midCache);

                subNewTris.Add(a); subNewTris.Add(ab); subNewTris.Add(ca);
                subNewTris.Add(b); subNewTris.Add(bc); subNewTris.Add(ab);
                subNewTris.Add(c); subNewTris.Add(ca); subNewTris.Add(bc);
                subNewTris.Add(ab); subNewTris.Add(bc); subNewTris.Add(ca);
            }

            newSubTris[s] = subNewTris.ToArray();
        }

        // 65535í∏ì_í¥Ç¶ÇΩÇÁ32bit indexÇ…êÿÇËë÷Ç¶ÇÈ
        if (newVerts.Count > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.Clear();
        mesh.vertices = newVerts.ToArray();
        if (newNormals != null) mesh.normals = newNormals.ToArray();
        if (newUV != null) mesh.uv = newUV.ToArray();

        mesh.subMeshCount = subMeshCount;
        for (int s = 0; s < subMeshCount; s++) {
            mesh.SetTriangles(newSubTris[s], s);
        }
    }

    static int GetMidpoint(int i1, int i2, List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, Dictionary<long, int> cache) {
        long key = i1 < i2 ? ((long)i1 << 32) | (uint)i2 : ((long)i2 << 32) | (uint)i1;
        if (cache.TryGetValue(key, out int idx)) return idx;

        Vector3 mid = (verts[i1] + verts[i2]) * 0.5f;
        verts.Add(mid);

        if (normals != null)
            normals.Add(((normals[i1] + normals[i2]) * 0.5f).normalized);
        if (uvs != null)
            uvs.Add((uvs[i1] + uvs[i2]) * 0.5f);

        int newIdx = verts.Count - 1;
        cache[key] = newIdx;
        return newIdx;
    }
}
