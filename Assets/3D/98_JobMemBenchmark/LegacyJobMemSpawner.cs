using UnityEngine;

/*///////////////////////////////////////////
            LegacyJobMemSpawner
목적 : "이동을 Job으로 넘긴 것"과 "풀 수명 관리를 PriorityQueue로 옮긴 것"이 각각
       얼마나 이득이었는지를 같은 조건에서 재현/측정하기 위한 벤치마크 스포너.
       씬에는 이 컴포넌트 하나만 놓으면 나머지 매니저는 런타임에 스스로 만든다
       (99_ColliderBenchmark의 LegacyBruteForceSpawner와 같은 방식).

       벤치마크 씬 두 개가 같은 부하 조건(4000발 / 반경 300 / 속도 200 / 수명 1초 / 시드 12345)으로
       맞춰져 있고, 모드만 다르다 :
         UpdateTimeTestScen : UpdateDirect + SelfCountdown  (이전 방식)
         JobPQTestScene     : BurstJob     + PriorityQueue  (현재 방식)

       두 축을 인스펙터에서 독립적으로 끄고 켤 수 있다. 한 번에 하나만 바꿔가며
       재야 서로의 비용이 섞이지 않는다.

         MoveMode : None / UpdateDirect(총알마다 Update에서 직접 이동)
                         / BurstJob(TransformAccessArray + IJobParallelForTransform)
         LifeMode : None / SelfCountdown(오브젝트마다 Update에서 시간 감산 - 1748abc 이전 방식)
                         / PriorityQueue(매니저가 큐 맨 앞 만료 시각만 확인 - 현재 방식)

       콜라이더/판정은 전혀 붙이지 않는다 - 순수하게 이동과 수명 관리 비용만 본다.
       총알은 부모 없이 루트에 스폰한다 : TransformAccessArray는 계층이 얽히면
       병렬 분할이 깨져서, 부모 밑에 넣으면 BurstJob 쪽이 실제보다 느리게 나온다.
 *///////////////////////////////////////////
public sealed class LegacyJobMemSpawner : MonoBehaviour
{
    public enum eMoveMode
    {
        None,
        UpdateDirect,
        BurstJob,
    }

    public enum eLifeMode
    {
        None,
        SelfCountdown,
        PriorityQueue,
    }

    [Header("측정 대상")]
    [SerializeField] private eMoveMode m_eMoveMode = eMoveMode.UpdateDirect;
    [SerializeField] private eLifeMode m_eLifeMode = eLifeMode.SelfCountdown;

    [Header("부하 조건")]
    [SerializeField] private int m_iBulletCount = 3500;
    [SerializeField] private float m_fBoundsRadius = 300f;
    [SerializeField] private float m_fSpeed = 20f;
    [SerializeField] private float m_fAliveTime = 1.0f;
    [SerializeField] private int m_iRandomSeed = 12345;

    [Header("비주얼 (끄면 드로우콜 0 - 순수 CPU 비용만)")]
    [SerializeField] private bool m_bShowVisuals = true;
    [SerializeField] private float m_fBulletSize = 0.6f;

    private LegacyJobMoveManager m_refJobMove;
    private LegacyPQExpireManager m_refPQExpire;

    private Transform[] m_arrBullet;
    private Vector3[] m_arrVelocity;
    private Material m_matBullet;
    private Mesh m_meshBullet;

    private void Start()
    {
        Random.InitState(m_iRandomSeed);

        if (m_eMoveMode == eMoveMode.BurstJob)
            m_refJobMove = new GameObject("LegacyJobMoveManager").AddComponent<LegacyJobMoveManager>();

        if (m_eLifeMode == eLifeMode.PriorityQueue)
            m_refPQExpire = new GameObject("LegacyPQExpireManager").AddComponent<LegacyPQExpireManager>();

        if (m_bShowVisuals == true)
        {
            Shader tShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (tShader == null)
                tShader = Shader.Find("Unlit/Color");
            m_matBullet = new Material(tShader) { color = new Color(0.35f, 0.85f, 1f) };

            // CreatePrimitive를 총알마다 부르면 SphereCollider가 3500개 생겼다 지워지면서
            // 시작 프레임에 정적 콜라이더 트리가 통째로 재구성된다 - 메시만 한 번 빼서 공유한다
            GameObject refTemp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            m_meshBullet = refTemp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(refTemp);
        }

        SpawnBullets();
    }

    private void SpawnBullets()
    {
        m_arrBullet = new Transform[m_iBulletCount];
        m_arrVelocity = new Vector3[m_iBulletCount];

        for (int i = 0; i < m_iBulletCount; ++i)
        {
            // 컴포넌트 붙이는 도중 OnEnable이 초기화보다 먼저 도는 걸 막으려고
            // 비활성 상태로 만들고 세팅이 끝난 뒤에 켠다(콜라이더 벤치에서 겪은 함정)
            GameObject refObj = new GameObject("Bullet_" + i);
            refObj.SetActive(false);

            Transform refTr = refObj.transform;
            refTr.position = RandomPointInBounds();
            refTr.rotation = Random.rotationUniform;

            m_arrBullet[i] = refTr;
            m_arrVelocity[i] = refTr.forward * m_fSpeed;

            if (m_eMoveMode == eMoveMode.UpdateDirect)
            {
                LegacyUpdateMover refMover = refObj.AddComponent<LegacyUpdateMover>();
                refMover.Init(m_arrVelocity[i], m_fBoundsRadius);
            }

            if (m_eLifeMode == eLifeMode.SelfCountdown)
            {
                LegacySelfTimerObject refTimer = refObj.AddComponent<LegacySelfTimerObject>();
                refTimer.Init(this, i, m_fAliveTime);
            }

            if (m_bShowVisuals == true)
                AttachVisual(refTr);

            refObj.SetActive(true);

            // 활성화 이후에 등록해야 Job 배열과 실제 활성 상태가 어긋나지 않는다
            if (m_eMoveMode == eMoveMode.BurstJob)
                m_refJobMove.Register(refTr, m_fSpeed, m_fBoundsRadius);

            // 만료 시각을 골고루 흩어 놓는다 - 안 그러면 첫 만료가 한 프레임에 몰려
            // 큐 방식이 실제보다 불리하게 찍힌다
            if (m_eLifeMode == eLifeMode.PriorityQueue)
                m_refPQExpire.Schedule(this, i, Random.Range(0.01f, m_fAliveTime));
        }
    }

    // 콜라이더 없이 렌더러만 - 판정 비용이 섞이면 안 된다
    private void AttachVisual(Transform _refParent)
    {
        GameObject refVisual = new GameObject("Visual");
        refVisual.transform.SetParent(_refParent, false);
        refVisual.transform.localScale = Vector3.one * m_fBulletSize;
        refVisual.AddComponent<MeshFilter>().sharedMesh = m_meshBullet;
        refVisual.AddComponent<MeshRenderer>().sharedMaterial = m_matBullet;
    }

    private Vector3 RandomPointInBounds()
    {
        return Random.insideUnitSphere * m_fBoundsRadius;
    }

    // 두 만료 방식이 공통으로 부르는 지점. 풀에 넣었다 빼는 대신 자리만 옮겨
    // 활성 개수를 항상 일정하게 유지한다 - 측정 대상은 "만료를 어떻게 확인하는가"이지
    // SetActive 토글 비용이 아니기 때문
    public void Respawn(int _iIndex)
    {
        Transform refTr = m_arrBullet[_iIndex];
        if (refTr == null)
            return;

        refTr.position = RandomPointInBounds();
        refTr.rotation = Random.rotationUniform;
        m_arrVelocity[_iIndex] = refTr.forward * m_fSpeed;

        if (m_eMoveMode == eMoveMode.UpdateDirect)
        {
            LegacyUpdateMover refMover = refTr.GetComponent<LegacyUpdateMover>();
            if (refMover != null)
                refMover.Init(m_arrVelocity[_iIndex], m_fBoundsRadius);
        }

        if (m_eLifeMode == eLifeMode.PriorityQueue)
            m_refPQExpire.Schedule(this, _iIndex, m_fAliveTime);
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(20, 15, 520, 20),
            "이동: " + m_eMoveMode + "   수명: " + m_eLifeMode + "   총알 " + m_iBulletCount);
        GUI.Label(new Rect(20, 35, 520, 20),
            "AliveTime " + m_fAliveTime.ToString("0.0") + "s  →  초당 만료 "
            + Mathf.RoundToInt(m_iBulletCount / Mathf.Max(m_fAliveTime, 0.0001f)) + "건");
    }
}
