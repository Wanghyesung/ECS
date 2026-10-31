using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
            BenchmarkBulletPresentation
목적 : 기본탄의 공유 메시/재질을 재사용하고, 생성 때 등록한 콜라이더만 기즈모로 표시한다.
       게임용 Bullet/Pool/ColliderManager를 실행하지 않아 씬별 판정 비교를 유지한다.
 *///////////////////////////////////////////
public sealed class BenchmarkBulletPresentation
{
    private struct tShape
    {
        public Transform Transform;
        public Vector3 HalfExtent;
        public float Radius;
    }

    private readonly List<tShape> m_listShapes = new List<tShape>(4200);
    private Mesh m_refMesh;
    private Material[] m_arrMaterials;
    private Vector3 m_vScale;
    private Vector3 m_vPosition;
    private Quaternion m_tRotation;
    private UnityEngine.Rendering.ShadowCastingMode m_eShadows;
    private bool m_bReceiveShadows;
    private BenchmarkMeshVisual[] m_arrObstacleVisuals;
    private BenchmarkMeshVisual[] m_arrMonsterVisuals;
    private readonly Vector3[] m_arrHitPositions = new Vector3[64];
    private readonly float[] m_arrHitExpiry = new float[64];
    private int m_iHitCount;
    private int m_iDisplayedHits = -1;
    private string m_strHitLabel;

    public void RegisterHit(Vector3 _vPosition)
    {
        int iSlot = m_iHitCount % m_arrHitPositions.Length;
        m_arrHitPositions[iSlot] = _vPosition;
        m_arrHitExpiry[iSlot] = Time.unscaledTime + 0.35f;
        m_iHitCount++;
    }

    public void DrawHitCounter()
    {
        // 기존 벤치마크 OnGUI에 표시한다. 이동/판정 콜백에서는 문자열을 만들지 않는다.
        if (m_iDisplayedHits != m_iHitCount)
        {
            m_iDisplayedHits = m_iHitCount;
            m_strHitLabel = "Hits: " + m_iHitCount + " (red markers = impact, bullets recycled)";
        }
        GUI.Label(new Rect(20f, 55f, 520f, 20f), m_strHitLabel);
    }

    public void InitializeActors(GameObject[] _arrObstacles, GameObject[] _arrMonsters)
    {
        m_arrObstacleVisuals = CreateVisuals(_arrObstacles);
        m_arrMonsterVisuals = CreateVisuals(_arrMonsters);
    }

    public bool AttachObstacle(Transform _refParent, int _iIndex, Vector3 _vHalfExtent)
    {
        if (m_arrObstacleVisuals.Length == 0)
            return false;
        m_arrObstacleVisuals[_iIndex % m_arrObstacleVisuals.Length].AttachBox(_refParent, _vHalfExtent);
        return true;
    }

    public bool AttachMonster(Transform _refParent, int _iIndex, float _fRadius)
    {
        if (m_arrMonsterVisuals.Length == 0)
            return false;
        m_arrMonsterVisuals[_iIndex % m_arrMonsterVisuals.Length].AttachSphere(_refParent, _fRadius);
        return true;
    }

    private static BenchmarkMeshVisual[] CreateVisuals(GameObject[] _arrPrefabs)
    {
        if (_arrPrefabs == null)
            return System.Array.Empty<BenchmarkMeshVisual>();
        var arrVisuals = new BenchmarkMeshVisual[_arrPrefabs.Length];
        for (int i = 0; i < arrVisuals.Length; i++)
            arrVisuals[i] = new BenchmarkMeshVisual(_arrPrefabs[i]);
        return arrVisuals;
    }

    public void Initialize(GameObject _refPrefab)
    {
        m_listShapes.Clear();
        m_iHitCount = 0;
        m_iDisplayedHits = -1;
        System.Array.Clear(m_arrHitExpiry, 0, m_arrHitExpiry.Length);
        if (_refPrefab == null)
            return;

        // BaseBullet의 단일 MeshRenderer를 사용한다. 원본 컴포넌트는 인스턴스화하지 않는다.
        MeshFilter refFilter = _refPrefab.GetComponentInChildren<MeshFilter>(true);
        MeshRenderer refRenderer = refFilter.GetComponent<MeshRenderer>();
        m_refMesh = refFilter.sharedMesh;
        m_arrMaterials = refRenderer.sharedMaterials;
        m_eShadows = refRenderer.shadowCastingMode;
        m_bReceiveShadows = refRenderer.receiveShadows;
        m_vScale = Vector3.Scale(_refPrefab.transform.localScale, refFilter.transform.localScale);
        m_tRotation = refFilter.transform.localRotation;
        // 벤치마크 루트가 판정 중심이므로 원본의 피벗 오프셋만큼 외형을 뒤로 이동한다.
        m_vPosition = Vector3.Scale(_refPrefab.transform.localScale, refFilter.transform.localPosition)
            - _refPrefab.GetComponent<CircleCollider>().Offset;
    }

    public void AttachBullet(Transform _refParent)
    {
        GameObject refVisual = new GameObject("BaseBullet Visual", typeof(MeshFilter), typeof(MeshRenderer));
        refVisual.layer = _refParent.gameObject.layer;
        refVisual.transform.SetParent(_refParent, false);
        refVisual.transform.localPosition = m_vPosition;
        refVisual.transform.localRotation = m_tRotation;
        refVisual.transform.localScale = m_vScale;
        refVisual.GetComponent<MeshFilter>().sharedMesh = m_refMesh;
        MeshRenderer refRenderer = refVisual.GetComponent<MeshRenderer>();
        refRenderer.sharedMaterials = m_arrMaterials;
        refRenderer.shadowCastingMode = m_eShadows;
        refRenderer.receiveShadows = m_bReceiveShadows;
    }

    public void AddSphere(Transform _refTransform, float _fRadius)
        => m_listShapes.Add(new tShape { Transform = _refTransform, Radius = _fRadius });

    public void AddBox(Transform _refTransform, Vector3 _vHalfExtent)
        => m_listShapes.Add(new tShape { Transform = _refTransform, HalfExtent = _vHalfExtent });

    public static void DrawSceneSphere(Transform _refTransform, float _fRadius)
    {
        Matrix4x4 tMatrix = Gizmos.matrix;
        Color tColor = Gizmos.color;
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(_refTransform.position, _fRadius);
        Gizmos.matrix = tMatrix;
        Gizmos.color = tColor;
    }

    public static void DrawSceneBox(Transform _refTransform, Vector3 _vHalfExtent)
    {
        Matrix4x4 tMatrix = Gizmos.matrix;
        Color tColor = Gizmos.color;
        Gizmos.matrix = Matrix4x4.TRS(_refTransform.position, _refTransform.rotation, Vector3.one);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.zero, _vHalfExtent * 2f);
        Gizmos.matrix = tMatrix;
        Gizmos.color = tColor;
    }

    public void DrawGizmos()
    {
        Matrix4x4 tPreviousMatrix = Gizmos.matrix;
        Color tPreviousColor = Gizmos.color;
        for (int i = 0; i < m_listShapes.Count; i++)
        {
            tShape tShape = m_listShapes[i];
            if (tShape.Transform == null || tShape.Transform.gameObject.activeInHierarchy == false)
                continue;
            if (tShape.Radius > 0f)
            {
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(tShape.Transform.position, tShape.Radius);
            }
            else
            {
                Gizmos.matrix = Matrix4x4.TRS(tShape.Transform.position, tShape.Transform.rotation, Vector3.one);
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(Vector3.zero, tShape.HalfExtent * 2f);
            }
        }
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = Color.red;
        for (int i = 0; i < m_arrHitPositions.Length; i++)
        {
            if (m_arrHitExpiry[i] > Time.unscaledTime)
                Gizmos.DrawWireSphere(m_arrHitPositions[i], 1.2f);
        }
        Gizmos.matrix = tPreviousMatrix;
        Gizmos.color = tPreviousColor;
    }
}
