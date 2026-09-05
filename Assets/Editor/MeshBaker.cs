#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using VolumetricLines;

public class MeshBaker : Editor
{
    [MenuItem("Tools/Bake Volumetric Line Mesh")]
    public static void BakeMesh()
    {
       
            var target = Selection.activeGameObject;
            var vlb = target.GetComponent<VolumetricLineBehavior>();

            Vector3 startPos = vlb.StartPos;
            Vector3 endPos = vlb.EndPos;

            Vector3[] vertices = {
            startPos, startPos, startPos, startPos,
            endPos,   endPos,   endPos,   endPos,
        };
            Vector3[] normals = {
            endPos,   endPos,   endPos,   endPos,
            startPos, startPos, startPos, startPos,
        };

            Mesh mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = VolumetricLineVertexData.TexCoords;
            mesh.uv2 = VolumetricLineVertexData.VertexOffsets;
            mesh.SetIndices(VolumetricLineVertexData.Indices, MeshTopology.Triangles, 0);
            mesh.RecalculateBounds();

            // 에셋으로 저장
            AssetDatabase.CreateAsset(mesh, "Assets/BakedLineMesh.asset");
            AssetDatabase.SaveAssets();

            // MeshFilter에 바로 꽂기
            target.GetComponent<MeshFilter>().sharedMesh = mesh;

            // cs 제거
            DestroyImmediate(vlb);

            Debug.Log("완료! VolumetricLineBehavior 제거됨");
    }
}
#endif