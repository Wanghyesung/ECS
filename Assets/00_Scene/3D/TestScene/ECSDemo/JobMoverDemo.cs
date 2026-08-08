using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

// [학습용 비교 데모 - After]
// MonoMoverDemo와 완전히 같은 이동 로직을, 개별 MonoBehaviour 콜백 없이
// TransformAccessArray + IJobParallelForTransform으로 병렬 처리한다.
// 큐브 개수/속도를 MonoMoverDemo와 동일하게 맞추고 Profiler로 비교해볼 것.
public class JobMoverDemo : MonoBehaviour
{
    [SerializeField] private int m_iSpawnCount = 5000;
    [SerializeField] private float m_fSpeed = 5f;
    [SerializeField] private float m_fSpawnRange = 50f;

    private TransformAccessArray m_transformArray;
    private NativeArray<float> m_arrSpeed;
    private JobHandle m_moveHandle;

    private void Start()
    {
        m_transformArray = new TransformAccessArray(m_iSpawnCount);
        m_arrSpeed = new NativeArray<float>(m_iSpawnCount, Allocator.Persistent);

        for (int i = 0; i < m_iSpawnCount; ++i)
        {
            GameObject refCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            refCube.transform.position = new Vector3(
                Random.Range(-m_fSpawnRange, m_fSpawnRange),
                0f,
                Random.Range(-m_fSpawnRange, m_fSpawnRange));
            refCube.transform.localScale = Vector3.one * 0.3f;
            Destroy(refCube.GetComponent<Collider>());

            m_transformArray.Add(refCube.transform);
            m_arrSpeed[i] = m_fSpeed;
        }
    }

    private void Update()
    {
        var job = new MoveJob
        {
            ArrSpeed = m_arrSpeed,
            FDeltaTime = Time.deltaTime
        };
        // 예약만 하고 바로 반환 - 실제 계산은 워커 스레드에서 병렬로 진행됨
        m_moveHandle = job.Schedule(m_transformArray);
    }

    private void LateUpdate()
    {
        // 이번 프레임 Job이 끝날 때까지 대기 (다음 프레임 로직/렌더링이 결과를 안전하게 읽도록 보장)
        m_moveHandle.Complete();
    }

    private void OnDestroy()    
    {
        if (m_transformArray.isCreated)
            m_transformArray.Dispose();
        if (m_arrSpeed.IsCreated)
            m_arrSpeed.Dispose();
    }

    // NativeArray만 참조하는 순수 struct - class/List 등 관리 객체는 여기 들어올 수 없음
    [BurstCompile]
    private struct MoveJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<float> ArrSpeed;
        public float FDeltaTime;

        public void Execute(int index, TransformAccess _transform)
        {
            Vector3 vForward = _transform.rotation * Vector3.forward;
            _transform.position += vForward * ArrSpeed[index] * FDeltaTime;
        }
    }
}
