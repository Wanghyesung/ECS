using System.Collections.Generic;
using R3;
using UnityEngine;

/*///////////////////////////////////////////
                TimeScaleManager
목적 : Time.timeScale 의 유일한 소유자. 여러 시스템(레벨업 카드, 조커 카드, 보스 컷신, 핵폭탄 연출 등)이
       각자 timeScale 을 0/1 로 직접 덮어쓰면 겹칠 때 먼저 건 정지가 나중 해제에 씹히는 문제가 있어
       "누가 멈췄는지" 를 소유자 집합으로 들고, 한 명이라도 남아 있으면 0 을 유지한다.
 *///////////////////////////////////////////

public sealed class TimeScaleManager : MonoBehaviour
{
    public static TimeScaleManager m_Instance { get; private set; }

    // 정지를 요청한 주체들. 비어 있으면 timeScale 1, 하나라도 있으면 0
    private readonly HashSet<object> m_hashPauseOwners = new HashSet<object>();

    private readonly ReactiveProperty<bool> m_rpIsPaused = new ReactiveProperty<bool>(false);
    public ReadOnlyReactiveProperty<bool> IsPaused => m_rpIsPaused;

    private void Awake()
    {
        if (m_Instance != null && m_Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        m_Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (m_Instance != this)
            return;

        m_Instance = null;
        m_rpIsPaused.Dispose();

        // 에디터는 Play 종료 후에도 Time.timeScale 값이 남는다(Time Manager 설정) - 0 으로 끝나면 다음 Play 가 멈춘 채 시작되므로 복구
        Time.timeScale = 1.0f;
    }

    // _refOwner 는 보통 호출한 컴포넌트 자신(this). 같은 owner 의 중복 Pause 는 1회로 취급된다
    public void Pause(object _refOwner)
    {
        if (m_hashPauseOwners.Add(_refOwner))
            Apply();
    }

    public void Resume(object _refOwner)
    {
        if (m_hashPauseOwners.Remove(_refOwner))
            Apply();
    }

    // 씬 전환/게임오버 안전망 - 정지를 잡은 UI 가 씬과 함께 파괴돼 Resume 을 못 부르면 영구 정지가 되므로 통째로 비운다
    public void ClearAll()
    {
        if (m_hashPauseOwners.Count == 0)
            return;

        m_hashPauseOwners.Clear();
        Apply();
    }

    private void Apply()
    {
        bool bPaused = m_hashPauseOwners.Count > 0;
        Time.timeScale = bPaused ? 0.0f : 1.0f;
        m_rpIsPaused.Value = bPaused;
    }
}
