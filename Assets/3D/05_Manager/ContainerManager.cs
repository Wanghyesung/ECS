using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ContainerManager : MonoBehaviour
{
    public static ContainerManager m_Instance = null;

    [SerializeField] private List<Container> m_listContianer; 

    private void Awake()
    {
        if (m_Instance != null && m_Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        m_Instance = this;
        DontDestroyOnLoad(this);
    }



    private void Start()
    {
        for(int i = 0; i<m_listContianer.Count; ++i)
            m_listContianer[i].Init();
    }
}
