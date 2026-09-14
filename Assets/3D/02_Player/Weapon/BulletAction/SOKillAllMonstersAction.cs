using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                SOKillAllMonstersAction
기능 : 현재 스테이지의 활성 몬스터 전부에게 owner의 AttackInfo.Damage를 가한다 (핵폭탄 착탄용).
       범위 콜라이더가 아니라 스폰 담당 DungeonManager가 든 몬스터 목록을 쓰는 이유:
       BoxColliderGrid는 빌드 시점 최대 반지름으로 셀 크기를 고정하는 정적 그리드라
       맵 크기 콜라이더는 판정이 샌다.
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_KillAllMonstersAction", menuName = "Game/Weapon/BulletAction/KillAllMonsters")]
public class SOKillAllMonstersAction : SOBulletAction
{
    // SO 에셋에 런타임 상태를 두지 않기 위해 static 스크래치 버퍼 - Execute는 메인 스레드 순차 실행이라 공유해도 안전
    private static readonly List<Monster> s_listBuffer = new List<Monster>();

    public override void Execute(IAttackObject _refOwner)
    {
        if (DungeonManager.m_Instance == null)
            return;

        s_listBuffer.Clear();
        DungeonManager.m_Instance.CollectAliveMonsters(s_listBuffer);

        tShotInfo tShot = new tShotInfo();
        for (int i = 0; i < s_listBuffer.Count; ++i)
        {
            tShot.HitPosition = s_listBuffer[i].transform.position;
            s_listBuffer[i].TakeDamage(_refOwner.AttackInfo, tShot);
        }
    }
}
