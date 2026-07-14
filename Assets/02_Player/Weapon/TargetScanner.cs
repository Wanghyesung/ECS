using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetScanner : MonoBehaviour
{
    private Transform m_refNearTargetTr;
    public Transform Target => m_refNearTargetTr;

    [SerializeField] private float m_fFindTime; //몇초 주기로 타겟을 찾을지
    private WaitForSeconds m_refWaitSecond = null;

    private Coroutine m_COFindTarget = null;

    private Collider[] m_arrNearCollider = new Collider[30];
    [SerializeField] private float m_fFindRadius;

    [SerializeField] LayerMask m_tFindLayer;

    private void Awake()
    {
        m_refWaitSecond = new WaitForSeconds(m_fFindTime);
    }
    private void OnEnable()
    {
        m_COFindTarget = StartCoroutine(CoFindTarget());
    }

    private void OnDisable()
    {
        System.Array.Clear(m_arrNearCollider, 0, m_arrNearCollider.Length);
    }

    private IEnumerator CoFindTarget()
    {
        while(true)
        {
            Physics.OverlapSphereNonAlloc(transform.position, m_fFindRadius, m_arrNearCollider, m_tFindLayer);

            Transform refTarget = null;
            float fBestDist = float.MaxValue;
            Vector3 vPos = transform.position;

            foreach (var refMon in m_arrNearCollider)
            {
                if (refMon == null)
                    continue;

                float fDist = Vector3.SqrMagnitude(refMon.transform.position - vPos);
                if (fDist < fBestDist)
                {
                    fBestDist = fDist;
                    refTarget = refMon.transform;
                }
            }

            m_refNearTargetTr = refTarget;
            yield return m_refWaitSecond;
        }
    }

}
