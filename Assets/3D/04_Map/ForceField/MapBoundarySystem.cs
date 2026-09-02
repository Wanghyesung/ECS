using UnityEngine;

/*///////////////////////////////////////////
              MapBoundarySystem
목적 : Force Field 경계 셰이더에 타겟(카메라/플레이어)의 월드 좌표를 매 프레임 전달.
       "경계면 중 타겟과 가까운 지점만 국소적으로 밝아지는" 계산은 셰이더가 정점 단위로 처리함
 *///////////////////////////////////////////
[ExecuteAlways]
public sealed class MapBoundarySystem : MonoBehaviour
{
    [SerializeField] private Transform m_refProximityTarget;

    private Renderer m_refRenderer;
    private MaterialPropertyBlock m_propBlock;

    private static readonly int TargetWorldPosId = Shader.PropertyToID("_TargetWorldPos");

    private void Awake()
    {
        m_refRenderer = GetComponent<Renderer>();
        m_propBlock = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (m_refProximityTarget == null) return;

        // ExecuteAlways는 에디터 리로드 후 Awake가 Update보다 늦게(또는 안) 불릴 때가 있어 매 프레임 방어
        if (m_refRenderer == null) m_refRenderer = GetComponent<Renderer>();
        if (m_propBlock == null) m_propBlock = new MaterialPropertyBlock();
        if (m_refRenderer == null) return;

        Vector3 vTargetPos = m_refProximityTarget.position;

        m_refRenderer.GetPropertyBlock(m_propBlock);
        m_propBlock.SetVector(TargetWorldPosId, new Vector4(vTargetPos.x, vTargetPos.y, vTargetPos.z, 0f));
        m_refRenderer.SetPropertyBlock(m_propBlock);
    }
}
