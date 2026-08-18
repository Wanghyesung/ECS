using UnityEngine;
using ProceduralForceField;

/*///////////////////////////////////////////
              MapBoundaryProximityFollower
목적 : Procedural Force-Field 에셋의 ProceduralForceFieldHit은 원래 "피격 시 한 번 반짝이고
       사그라드는" 용도. 이걸 매 프레임 타겟(플레이어) 위치로 계속 재발동시켜서
       "타겟이 가까운 부분만 사그라들지 않고 계속 밝은" 근접 표시로 재활용한다.
 *///////////////////////////////////////////
[ExecuteAlways]
public sealed class MapBoundaryProximityFollower : MonoBehaviour
{
    [SerializeField] private ProceduralForceFieldHit m_refForceFieldHit;
    [SerializeField] private Transform m_refProximityTarget;

    private void Update()
    {
        if (m_refForceFieldHit == null || m_refProximityTarget == null) return;

        m_refForceFieldHit.TriggerHit(m_refProximityTarget.position);
    }
}
