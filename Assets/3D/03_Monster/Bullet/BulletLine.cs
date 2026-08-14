using UnityEngine;

/*///////////////////////////////////////////
                BulletLine
목적 : 독립적으로 풀링되는 볼렛 예고선(텔레그래프) 오브젝트.
       발사 시점에 Bullet이 넘겨주는 비주얼 메시를 그대로 물려받아
       시작점에서 방향벡터로 지정한 거리만큼 늘여서 표시한다
 *///////////////////////////////////////////

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(PoolObject))]
public sealed class BulletLine : MonoBehaviour
{
    [Header("에디터 테스트용 (런타임에는 Bullet이 SetLine을 직접 호출함)")]
    [SerializeField] private Vector3 m_vTestDirection = Vector3.forward;
    [SerializeField] private float m_fTestDistance = 5f;
    [SerializeField] private MeshFilter m_refTestSource;

    private MeshFilter m_refMeshFilter;

    private void Awake()
    {
        m_refMeshFilter = GetComponent<MeshFilter>();
    }

    // _refSource : 볼렛의 실제 비주얼 MeshFilter - 메시와 두께(X,Z)를 그대로 물려받기 위함
    public void SetLine(Vector3 _vStart, Vector3 _vDir, float _fDistance, MeshFilter _refSource)
    {
        if (_fDistance <= 0.0001f || _vDir.sqrMagnitude <= 0.0001f || _refSource == null)
            return;

        Vector3 vDir = _vDir.normalized;

        m_refMeshFilter.sharedMesh = _refSource.sharedMesh;

        transform.position = _vStart + vDir * (_fDistance * 0.5f);
        transform.rotation = Quaternion.FromToRotation(Vector3.up, vDir);

        // 독립적으로 풀링되어 스케일된 부모가 없으므로, 소스의 월드 스케일(lossyScale)을 두께로 그대로 사용
        Vector3 vSourceWorldScale = _refSource.transform.lossyScale;
        transform.localScale = new Vector3(vSourceWorldScale.x, _fDistance * 0.5f, vSourceWorldScale.z);
    }

    [ContextMenu("테스트 라인 생성")]
    private void TestBuildLine()
    {
        if (m_refMeshFilter == null)
            m_refMeshFilter = GetComponent<MeshFilter>();

        if (m_refTestSource == null)
        {
            Debug.Log("BulletLine 테스트 : m_refTestSource(소스 MeshFilter)를 먼저 지정하세요");
            return;
        }

        SetLine(transform.position, m_vTestDirection, m_fTestDistance, m_refTestSource);
    }

}
