using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

/*///////////////////////////////////////////
              BulletMoveManager
목적 : 직선으로만 움직이는 Bullet(플레인 총알)의 이동을 한곳에 모아 Job(Burst)으로
       일괄 계산한다. 별도의 JobBullet 서브클래스 없이 Bullet이 직접 이 매니저에 등록됨.

       TransformAccessArray를 써서 Job이 Transform에 직접 쓴다. 프리팹에 Rigidbody/Collider가
       이미 없어서(PhysX 트리거를 CircleCollider로 대체) Physics.SyncColliderTransform 비용이
       발생하지 않으므로, 메인 스레드에서 결과를 리스트 인덱싱+함수 호출로 다시 적용하는
       과정(ApplyMove) 없이 Job이 끝나는 즉시(Complete) 위치가 이미 반영돼 있음.

       Update()에서 Schedule만 하고, Complete는 LateUpdate에서 한다 - 그 사이(다른 모든
       스크립트의 Update 구간)만큼 워커 스레드에서 실제로 겹쳐 돌 시간을 벌기 위함.
       ColliderManager.LateUpdate()가 최신 위치로 판정해야 하므로, ColliderManager에는
       [DefaultExecutionOrder]로 이 매니저보다 나중에 돌도록 순서를 고정해뒀다.

       [DefaultExecutionOrder(500)]: Unity는 서로 다른 스크립트의 Update() 순서를 보장하지
       않는다. 총알을 쏘는 Weapon 쪽 Update()가 이 매니저의 Update()보다 먼저 끝나야
       Activate()가 "이미 Schedule되어 아직 Complete 안 된" NativeList를 건드리는 경합이
       안 생긴다 - 그래서 기본 순서(0)보다 확실히 뒤로 미뤄둠 (ColliderManager의 1000보다는 앞)
 *///////////////////////////////////////////

[DefaultExecutionOrder(500)]
public class BulletMoveManager : MonoBehaviour
{
    public static BulletMoveManager m_Instance = null;

    private TransformAccessArray m_transformArray;
    private NativeList<float> m_listSpeed;
    private NativeList<bool> m_listActive;

    [SerializeField] private int m_iInitialCapacity = 5120;

    private JobHandle m_tHandle;
    private bool m_bScheduled = false;
    private bool m_bDisposed = false;

    private int COUNT = 0;
    private int MAX = 0;
    private void Awake()
    {
        if (m_Instance != null)
        {
            Destroy(this);
            return;
        }

        m_Instance = this;
        DontDestroyOnLoad(this);

        m_transformArray = new TransformAccessArray(m_iInitialCapacity);
        m_listSpeed = new NativeList<float>(m_iInitialCapacity, Allocator.Persistent);
        m_listActive = new NativeList<bool>(m_iInitialCapacity, Allocator.Persistent);
    }

    // Bullet.Awake()에서 총알 생애주기 중 딱 한 번만 호출
    public int RegisterPermanent(Bullet _refOwner)
    {
        int iIndex = m_listSpeed.Length;

        m_transformArray.Add(_refOwner.transform);
        m_listSpeed.Add(0f);
        m_listActive.Add(false);

       
        return iIndex;
    }

    // Bullet.SetAttack()(발사 시점)에서 호출
    public void Activate(int _iIndex, float _fSpeed)
    {
        if (m_bDisposed)
            return;

        ++COUNT;
        if (COUNT > MAX)
        {
            MAX = COUNT;
            Debug.Log(MAX);
        }

        m_listSpeed[_iIndex] = _fSpeed;
        m_listActive[_iIndex] = true;
    }

    // Bullet.OnDisable()(풀 반납 시점)에서 호출. 플레이모드 종료/씬 전환 시 다른 오브젝트의
    // OnDisable이 이 매니저의 OnDestroy(NativeList Dispose) 이후에 불릴 수 있어서 가드 필요
    public void Deactivate(int _iIndex)
    {
        if (m_bDisposed)
            return;

        --COUNT;
        m_listActive[_iIndex] = false;
    }

    private void Update()
    {
        if (m_listSpeed.Length == 0)
            return;

        var job = new MoveJob
        {
            ArrSpeed = m_listSpeed.AsArray(),
            ArrActive = m_listActive.AsArray(),
            FDeltaTime = Time.deltaTime
        };

        m_tHandle = job.Schedule(m_transformArray);
        m_bScheduled = true;
    }

    // Job이 TransformAccessArray로 Transform에 직접 썼으므로 Complete만 하면 끝
    private void LateUpdate()
    {
        if (!m_bScheduled)
            return;

        m_tHandle.Complete();
        m_bScheduled = false;
    }

    private void OnDestroy()
    {
        m_bDisposed = true;

        if (m_bScheduled)
            m_tHandle.Complete();

        if (m_transformArray.isCreated)
            m_transformArray.Dispose();
        if (m_listSpeed.IsCreated)
            m_listSpeed.Dispose();
        if (m_listActive.IsCreated)
            m_listActive.Dispose();
    }

    // NativeArray/TransformAccess만 참조하는 순수 struct - class/List 등 관리 객체는 여기 들어올 수 없음
    [BurstCompile]
    private struct MoveJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<float> ArrSpeed;
        [ReadOnly] public NativeArray<bool> ArrActive;
        public float FDeltaTime;

        public void Execute(int index, TransformAccess _transform)
        {
            if (!ArrActive[index])
                return;

            Vector3 vForward = _transform.rotation * Vector3.forward;
            _transform.position += vForward * ArrSpeed[index] * FDeltaTime;
        }
    }
}
