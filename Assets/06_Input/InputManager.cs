using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    private static InputManager m_Instance = null;


    private void Awake()
    {
        if(m_Instance != this)
        {
            Destroy(this);
            return;
        }

        m_Instance = this;
        DontDestroyOnLoad(m_Instance);
    }


    private void Update()
    {
        
    }

}
