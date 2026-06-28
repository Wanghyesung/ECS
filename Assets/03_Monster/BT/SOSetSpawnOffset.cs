using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
             SetSpawnOffset
기능 : 소환한 오브젝트를 나의 오프셋 위치 만큼 밀어주기
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_SetSpawnOffset", menuName = "Game/Monster/ActionNode/SetSpawnOffset")]
public class SOSetSpawnOffset : SONode
{
    public override eNodeState Execute(BlackBoard _refBB)
    {
        int iCurAttackIdx = _refBB.CurrentAttackIdx;
        if (iCurAttackIdx == -1)
            return eNodeState.Failure;

        SpawnInfo refSpawnInfo = _refBB.Owner.ListAttackObject[iCurAttackIdx];
        if (refSpawnInfo == null)
            return eNodeState.Failure;

        float fSpawnOffset = refSpawnInfo.SpawnOffset;
        Vector3 vOwnerPos = _refBB.Owner.transform.position;
        foreach (GameObject refObj in _refBB.ListCurAttackObject)
        {
            Vector3 vObjRot = refObj.transform.rotation.eulerAngles;
            refObj.transform.position = vOwnerPos + (vObjRot * fSpawnOffset);
        }

        return eNodeState.Success;
    }
}

