using UnityEngine;

// [학습용 비교 데모 - Before]
// Bullet.cs의 FixedUpdate() 구조를 그대로 재현: 오브젝트 수만큼 MonoBehaviour 콜백이 개별 호출된다.
// 빈 GameObject에 이 컴포넌트만 붙이고 Play하면 m_iSpawnCount개의 큐브가 생성되어 각자 Update()로 이동한다.
public class MonoMoverDemo : MonoBehaviour
{
    [SerializeField] private int m_iSpawnCount = 5000;
    [SerializeField] private float m_fSpeed = 5f;
    [SerializeField] private float m_fSpawnRange = 50f;

    private void Start()
    {
        for (int i = 0; i < m_iSpawnCount; ++i)
        {
            GameObject refCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            refCube.transform.position = new Vector3(
                Random.Range(-m_fSpawnRange, m_fSpawnRange),
                0f,
                Random.Range(-m_fSpawnRange, m_fSpawnRange));
            refCube.transform.localScale = Vector3.one * 0.3f;

            // 물리 오버헤드까지 같이 재는 걸 막기 위해 기본 콜라이더는 제거 (순수 이동 비용만 비교)
            Destroy(refCube.GetComponent<Collider>());

            refCube.AddComponent<MonoMover>().Init(m_fSpeed);
        }
    }
}

// Bullet.FixedUpdate()와 동일한 패턴: 총알(여기선 큐브) 개수만큼 이 Update()가 개별 호출됨
public class MonoMover : MonoBehaviour
{
    private float m_fSpeed;

    public void Init(float _fSpeed)
    {
        m_fSpeed = _fSpeed;
    }

    private void Update()
    {
        transform.position += transform.forward * m_fSpeed * Time.deltaTime;
    }
}
