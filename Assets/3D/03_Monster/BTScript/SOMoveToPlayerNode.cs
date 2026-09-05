using UnityEngine;

/*///////////////////////////////////////////
            SOLookAtLoopNode
기능 : 몬스터의 스피드로 플레이어에게 따라가는 노드    
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_MoveToPlayerNode", menuName = "Game/Monster/ActionNode/MoveToPlayerNode")]
public class SOMoveToPlayerNode : SONode
{
    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (_refBB.TargetTr == null)
            return eNodeState.Failure;

        Transform refOwnerTr = _refBB.OwnerOffset == null ? _refBB.Owner.transform : _refBB.OwnerOffset;
        Vector3 vDir = _refBB.TargetTr.position - refOwnerTr.position;

        float fMonsterSpeed = _refBB.ObjInfo.Speed;
        refOwnerTr.position += (fMonsterSpeed * vDir.normalized * Time.deltaTime);

        return eNodeState.Success;
    }
}
