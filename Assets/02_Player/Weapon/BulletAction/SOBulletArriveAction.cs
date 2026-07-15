using UnityEngine;

/*///////////////////////////////////////////
                BulletArriveAction
목적 : 총알이 타겟에 명중하거나 AliveTime 만료로 풀에 반납될 때(=도착) 실행할 로직을 SO로 분리.
       Bullet 프리팹마다 인스펙터에서 원하는 Action들을 배열로 조합/교체할 수 있음.
       SO는 설정값(에디터 세팅)만 갖고, 총알별로 달라지는 동적 데이터는 Execute의 Bullet 인자를 통해 참조.
 *///////////////////////////////////////////
public abstract class SOBulletArriveAction : ScriptableObject
{
    public abstract void Execute(Bullet _refOwner);
}
