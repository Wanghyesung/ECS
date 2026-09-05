using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

/*///////////////////////////////////////////
           LegacyPQExpireManager
목적 : 현재 ObjectPool의 PriorityQueue 만료 예약(커밋 1748abc)을 벤치마크 씬에서
       독립적으로 돌리기 위한 축소판. LegacySelfTimerObject와의 비교군.

       오브젝트마다 매 프레임 시간을 감산하는 대신 "이 시각에 반납"만 예약해두고,
       매 프레임 큐 맨 앞(가장 이른 만료 시각)만 확인한다. 그래서 비용이 활성 개수가
       아니라 그 프레임에 실제로 만료된 개수에 비례한다.

       실제 ObjectPool은 이 루프를 UniTask(UpdateExpireQueue)로 돌리지만, 여기서는
       비교군(LegacySelfTimerObject)과 같은 Update 구간에 두어야 공정하므로 Update에
       둔다 - 하는 일과 비용은 동일하다.

       Generation은 실제 구현과 같은 이유로 남겨뒀다 : 예약 이후 중간에 자리가 바뀌면
       낡은 예약이 새 생애를 잘못 만료시키므로, 번호가 다르면 그 예약을 버린다.
 *///////////////////////////////////////////
public sealed class LegacyPQExpireManager : MonoBehaviour
{
    private static readonly ProfilerMarker s_tMarkerExpire = new ProfilerMarker("JobMem.PQExpireCheck");

    private struct tTimeData
    {
        public float fPushTime;
        public int iIndex;
        public int iGeneration;

        public tTimeData(float _fExpireTime, int _iIndex, int _iGeneration)
        {
            fPushTime = _fExpireTime;
            iIndex = _iIndex;
            iGeneration = _iGeneration;
        }
    }

    private struct tExpireTimeComparer : IComparer<tTimeData>
    {
        public int Compare(tTimeData x, tTimeData y)
        {
            return x.fPushTime.CompareTo(y.fPushTime);
        }
    }

    private PriorityQueue<tTimeData> m_PQTimer;
    private Dictionary<int, int> m_hashGeneration;
    private LegacyJobMemSpawner m_refOwner;

    private void Awake()
    {
        m_PQTimer = new PriorityQueue<tTimeData>(new tExpireTimeComparer());
        m_hashGeneration = new Dictionary<int, int>(8192);
    }

    public void Schedule(LegacyJobMemSpawner _refOwner, int _iIndex, float _fAliveTime)
    {
        m_refOwner = _refOwner;

        m_hashGeneration.TryGetValue(_iIndex, out int iGeneration);
        ++iGeneration;
        m_hashGeneration[_iIndex] = iGeneration;

        m_PQTimer.Enqueue(new tTimeData(Time.time + _fAliveTime, _iIndex, iGeneration));
    }

    // 매 프레임 큐 맨 앞만 본다 - 만료된 게 없으면 비교 한 번으로 끝난다
    private void Update()
    {
        using (s_tMarkerExpire.Auto())
        {
            while (m_PQTimer.Count > 0)
            {
                tTimeData tData = m_PQTimer.Peek();
                if (tData.fPushTime - Time.time > 0.0f)
                    break;

                m_PQTimer.Dequeue();

                // 낡은 예약이면 버린다
                if (m_hashGeneration.TryGetValue(tData.iIndex, out int iGeneration) == false
                    || iGeneration != tData.iGeneration)
                    continue;

                if (m_refOwner != null)
                    m_refOwner.Respawn(tData.iIndex);
            }
        }
    }
}
