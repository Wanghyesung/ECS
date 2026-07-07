using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.ShaderGraph.Internal.KeywordDependentCollection;

/*///////////////////////////////////////////
               TargetManager
목적 : 현재 공격중인 몬스터의 정보를 UI로 표현
 *///////////////////////////////////////////
public class TargetManager : MonoBehaviour
{
    public static TargetManager m_Instance = null;
    private Monster m_refTarget = null;

    //[SerializeField] private 
    private void Awake()
    {
        if (m_Instance != null)
            Destroy(this);

        m_Instance = this;
        DontDestroyOnLoad(this);
    }

    public void OnTarget()
    {

    }


}
