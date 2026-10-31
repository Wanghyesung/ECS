using System;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                BenchmarkMeshVisual
목적 : 프리팹의 정적 메시와 공유 재질만 복사한다. AI/무기/물리는 실행하지 않고,
       모델 비율을 보존하여 기존 벤치마크 콜라이더 안에 표시한다.
 *///////////////////////////////////////////
public sealed class BenchmarkMeshVisual
{
    private struct tPart
    {
        public Mesh Mesh;
        public Material[] Materials;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public UnityEngine.Rendering.ShadowCastingMode Shadows;
        public bool ReceiveShadows;
    }

    private readonly tPart[] m_arrParts;
    private readonly Bounds m_tBounds;
    private readonly string m_strName;

    public BenchmarkMeshVisual(GameObject _refPrefab)
    {
        m_strName = _refPrefab.name + " Visual";
        var listParts = new List<tPart>();
        Bounds tBounds = default;
        bool bHasBounds = false;
        MeshFilter[] arrFilters = _refPrefab.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < arrFilters.Length; i++)
        {
            MeshFilter refFilter = arrFilters[i];
            MeshRenderer refRenderer = refFilter.GetComponent<MeshRenderer>();
            if (refFilter.sharedMesh == null || refRenderer == null)
                continue;
            if (refRenderer.enabled == false || IsVisible(refFilter.transform, _refPrefab.transform) == false)
                continue;
            Matrix4x4 tLocal = _refPrefab.transform.worldToLocalMatrix * refFilter.transform.localToWorldMatrix;
            listParts.Add(new tPart
            {
                Mesh = refFilter.sharedMesh,
                Materials = refRenderer.sharedMaterials,
                Position = tLocal.GetColumn(3),
                Rotation = tLocal.rotation,
                Scale = tLocal.lossyScale,
                Shadows = refRenderer.shadowCastingMode,
                ReceiveShadows = refRenderer.receiveShadows
            });
            Bounds tMeshBounds = refFilter.sharedMesh.bounds;
            for (int j = 0; j < 8; j++)
            {
                Vector3 vSign = new Vector3((j & 1) == 0 ? -1f : 1f,
                    (j & 2) == 0 ? -1f : 1f, (j & 4) == 0 ? -1f : 1f);
                Vector3 vCorner = tLocal.MultiplyPoint3x4(tMeshBounds.center + Vector3.Scale(tMeshBounds.extents, vSign));
                if (bHasBounds)
                    tBounds.Encapsulate(vCorner);
                else
                {
                    tBounds = new Bounds(vCorner, Vector3.zero);
                    bHasBounds = true;
                }
            }
        }
        if (listParts.Count == 0)
            throw new InvalidOperationException("No visible MeshRenderer in " + _refPrefab.name);
        m_arrParts = listParts.ToArray();
        m_tBounds = tBounds;
    }

    public void AttachSphere(Transform _refParent, float _fRadius)
        => Attach(_refParent, _fRadius / Mathf.Max(m_tBounds.extents.magnitude, 0.0001f));

    public void AttachBox(Transform _refParent, Vector3 _vHalfExtent)
    {
        Vector3 vExtent = m_tBounds.extents;
        float fScale = Mathf.Min(_vHalfExtent.x / Mathf.Max(vExtent.x, 0.0001f),
            Mathf.Min(_vHalfExtent.y / Mathf.Max(vExtent.y, 0.0001f),
                _vHalfExtent.z / Mathf.Max(vExtent.z, 0.0001f)));
        Attach(_refParent, fScale);
    }

    private void Attach(Transform _refParent, float _fScale)
    {
        for (int i = 0; i < m_arrParts.Length; i++)
        {
            tPart tPart = m_arrParts[i];
            var refVisual = new GameObject(m_strName, typeof(MeshFilter), typeof(MeshRenderer));
            refVisual.layer = _refParent.gameObject.layer;
            refVisual.transform.SetParent(_refParent, false);
            refVisual.transform.localPosition = (tPart.Position - m_tBounds.center) * _fScale;
            refVisual.transform.localRotation = tPart.Rotation;
            refVisual.transform.localScale = tPart.Scale * _fScale;
            refVisual.GetComponent<MeshFilter>().sharedMesh = tPart.Mesh;
            MeshRenderer refRenderer = refVisual.GetComponent<MeshRenderer>();
            refRenderer.sharedMaterials = tPart.Materials;
            refRenderer.shadowCastingMode = tPart.Shadows;
            refRenderer.receiveShadows = tPart.ReceiveShadows;
        }
    }

    private static bool IsVisible(Transform _refTransform, Transform _refRoot)
    {
        for (Transform refCurrent = _refTransform; refCurrent != _refRoot; refCurrent = refCurrent.parent)
        {
            if (refCurrent.gameObject.activeSelf == false)
                return false;
        }
        return true;
    }
}
