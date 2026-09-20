# if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class MeshCountTool
{
    [MenuItem("Tools/Check Selected Mesh Count")]
    static void CheckMeshCount()
    {
        GameObject root = Selection.activeGameObject;

        if (root == null)
        {
            Debug.Log("GameObject를 선택하세요.");
            return;
        }

        long vertices = 0;
        long triangles = 0;

        MeshFilter[] meshFilters =
            root.GetComponentsInChildren<MeshFilter>(true);

        foreach (MeshFilter filter in meshFilters)
        {
            Mesh mesh = filter.sharedMesh;

            if (mesh == null)
                continue;

            vertices += mesh.vertexCount;
            triangles += mesh.triangles.Length / 3;
        }

        SkinnedMeshRenderer[] skinnedMeshes =
            root.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        foreach (SkinnedMeshRenderer renderer in skinnedMeshes)
        {
            Mesh mesh = renderer.sharedMesh;

            if (mesh == null)
                continue;

            vertices += mesh.vertexCount;
            triangles += mesh.triangles.Length / 3;
        }

        Debug.Log(
            $"{root.name}\n" +
            $"Vertices : {vertices:N0}\n" +
            $"Triangles: {triangles:N0}"
        );
    }
}
#endif