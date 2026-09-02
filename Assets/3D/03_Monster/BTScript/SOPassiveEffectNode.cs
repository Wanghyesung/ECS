using UnityEngine;

/*///////////////////////////////////////////
           SOPassiveEffectNode

기능 : BlackBoard.ObjInfo.Effects[]를 매 프레임 순회해
       활성 상태 이상을 처리.
         - 만료된 효과 → 비트 제거
         - DoT 효과 (TickDamage > 0) → NextTickTime마다 HP 차감

       항상 Success 반환. SOParallelNode의 첫 번째 자식으로 배치.

GC Alloc: 0 — SO 자체에 상태 없음. 모든 데이터는 BlackBoard에.
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_PassiveEffectNode", menuName = "Game/Monster/ActionNode/PassiveEffectNode")]
public class SOPassiveEffectNode : SONode
{
    public override eNodeState Execute(BlackBoard _refBB)
    {
        ushort mask = _refBB.ObjInfo.CurrentEffects;
        if (mask == 0)
            return eNodeState.Success;

        float fNow   = Time.time;
        int iCount = (int)eStatusEffect.End;

        for (int i = 0; i < iCount; i++)
        {
            if ((mask & (1 << i)) == 0)
                continue;

            if (fNow >= _refBB.ObjInfo.Effects[i].EndTime)
            {
                _refBB.ObjInfo.CurrentEffects = (ushort)(_refBB.ObjInfo.CurrentEffects & ~(1 << i));
                mask = _refBB.ObjInfo.CurrentEffects;
                continue;
            }

            if (_refBB.ObjInfo.Effects[i].TickDamage > 0 &&
                fNow >= _refBB.ObjInfo.Effects[i].NextTickTime)
            {
                _refBB.ObjInfo.CurrentHP -= _refBB.ObjInfo.Effects[i].TickDamage;
                _refBB.ObjInfo.Effects[i].NextTickTime = fNow + _refBB.ObjInfo.Effects[i].TickInterval;
            }
        }

        return eNodeState.Success;
    }
}
