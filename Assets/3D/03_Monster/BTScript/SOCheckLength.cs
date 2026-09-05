using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/*///////////////////////////////////////////
            SOCheckLength
기능 : 플레이어와 몬스터의 거리가 일정범위 안, 밖에 있는지 체크
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_CheckLength", menuName = "Game/Monster/ActionNode/CheckLength")]

public class SOCheckLength : SONode
{
    [Tooltip("해당 범위 안을 체크할지 밖을 체크할지")]
    [SerializeField] private bool m_bIsIn;
    [SerializeField] private float m_fCheckLength = 20.0f;
    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (_refBB.TargetTr == null)
            return eNodeState.Failure;

        Transform refOwnerTr = _refBB.OwnerOffset == null ? _refBB.Owner.transform : _refBB.OwnerOffset;
        Vector3 vLength = _refBB.TargetTr.position - refOwnerTr.position;
        float fLength = vLength.magnitude;

        if(m_bIsIn == true)
        {
            if (m_fCheckLength >= fLength)
                return eNodeState.Success;
            return eNodeState.Failure;
        }
        else
        {
            if (m_fCheckLength <= fLength)
                return eNodeState.Success;
            return eNodeState.Failure;
        }
    }
}

