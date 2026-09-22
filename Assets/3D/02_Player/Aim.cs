using R3;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;



/*///////////////////////////////////////////
                   Aim
기능 : ray, claude플레이어가 쏠 방향을 제공하는 클래스
 *///////////////////////////////////////////

public class Aim : MonoBehaviour
{
    [SerializeField] private LayerMask m_tLayerMask;
    [SerializeField] private float m_fMaxLength;

  
    private Vector3 m_tTargetPosition = Vector3.zero;
    public Vector3 TargetPosition => m_tTargetPosition;

    private readonly Subject<Unit> m_subOnTarget = new();
    private readonly Subject<Unit> m_subUnTarget = new();
    public Observable<Unit> OnTarget => m_subOnTarget;
    public Observable<Unit> UnTarget => m_subUnTarget;

    private bool m_bPreState = false;
    private void Update()
    {
        m_tTargetPosition = RayCast();
    }

    public Vector3 RayCast()
    {
        Ray tRay = Camera.main.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0));

        Vector3 vReturnPos = Vector3.zero;
        bool bCurState = false;
        if (ColliderManager.m_Instance.RaycastMask(tRay.origin, tRay.direction, m_fMaxLength, m_tLayerMask, out CircleCollider refHit))
        {
            bCurState = true;
            vReturnPos = refHit.transform.position;
        }
        else
        {
            bCurState = false;
            vReturnPos =  tRay.origin + tRay.direction * m_fMaxLength;
        }


        if(bCurState != m_bPreState)
        {
            m_bPreState = bCurState;
            if (m_bPreState == true)
                m_subOnTarget.OnNext(Unit.Default);
            else
                m_subUnTarget.OnNext(Unit.Default);
        }

        return vReturnPos;
    }


   
}
