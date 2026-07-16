using UnityEngine;

/*///////////////////////////////////////////
                BulletAction
목적 : 총알에서 발생하는 이벤트(도착/명중 등)에 실행할 로직을 SO로 분리.
       Bullet은 이벤트별로 배열을 따로 들고 있고(Arrive/Hit), 어느 배열에 넣느냐로 실행 시점이 정해짐.
       SO는 설정값(에디터 세팅)만 갖고, 총알별로 달라지는 동적 데이터는 Execute의 Bullet 인자를 통해 참조.
 *///////////////////////////////////////////
public abstract class SOBulletAction : ScriptableObject
{
    public abstract void Execute(IAttackObject _refOwner);
}
