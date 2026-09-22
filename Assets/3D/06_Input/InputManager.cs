using R3;
using UnityEngine;
using UnityEngine.InputSystem;

/*///////////////////////////////////////////
                InputManager
기능 : PlayerAction.inputactions 에서 생성된 PlayerAction 클래스를 소유하고 입력을 노출한다.
       - 연속 입력(이동/마우스)은 매 프레임 읽어 tInputInfo 로 캐싱해서 노출
       - 불연속 입력(버튼)은 캐싱하지 않고 눌린 순간에만 Observable 로 알린다
         (소비자가 매 프레임 폴링할 필요가 없어지고, 키가 늘어도 Update 가 무거워지지 않는다)
       입력에 대한 반응(이동 계산, 발사 등)은 각 System 이 담당하고 여기엔 두지 않는다.
 *///////////////////////////////////////////

public struct tInputInfo
{
    public Vector2 MoveDir;
    public Vector2 Delta;
}

public sealed class InputManager : MonoBehaviour
{
    public static InputManager m_Instance { get; private set; }

    // MoveButton(Space)이 눌린 순간 한 번 발행. 누르고 있어도 반복되지 않는다
    // 구독은 Awake 가 아니라 Start 에서 — Awake 순서는 보장되지 않아 m_Instance 가 아직 null 일 수 있다.

    private readonly Subject<Unit> m_subjectMoveButton = new();
    public Observable<Unit> OnMoveButtonPressed => m_subjectMoveButton;

    // 좌클릭 차지샷 - 누름/뗌을 각각 한 번씩만 발행 (PlayerChargeController가 구독)
    private readonly Subject<Unit> m_subjectChargeStarted = new();
    private readonly Subject<Unit> m_subjectChargeReleased = new();
    private readonly Subject<Unit> m_subjectChargeCanceled = new();
    public Observable<Unit> OnChargeButtonStarted => m_subjectChargeStarted;
    public Observable<Unit> OnChargeButtonReleased => m_subjectChargeReleased;
    public Observable<Unit> OnChargeButtonCanceled => m_subjectChargeCanceled;

    private PlayerAction m_refActions;
    private tInputInfo m_tInputInfo;

    // 마우스 Delta 는 에디터/빌드 진입 직후 커서 워프로 큰 값이 한 번 들어온다.
    // 첫 유효 델타 한 번을 0으로 버려서 시작하자마자 시점이 튀는 것을 막는다.
    private bool m_bDeltaInitialized;

    public tInputInfo InputInfo => m_tInputInfo;

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

    // PlayerAction 생성을 Awake 가 아니라 여기서 하는 이유:
    // (1) Play 도중 스크립트 재컴파일(도메인 리로드)이 일어나면 Awake 는 다시 호출되지 않지만
    //     OnEnable 은 다시 호출된다. m_refActions 는 직렬화 대상이 아니라 그때 null 로 리셋되므로
    //     Awake 에서만 만들면 리로드 후 입력이 영구히 죽는다.
    // (2) Awake 가드에 걸려 파괴 예약된 중복 인스턴스도 그 프레임 끝까지 OnEnable/Update 가 더 돈다.
    private void OnEnable()
    {
        if (m_refActions == null)
            m_refActions = new PlayerAction();

        m_refActions.MoveAction.Enable();
        m_refActions.MoveAction.MoveButton.performed += OnMoveButtonPerformed;
        m_refActions.MoveAction.ChargeButton.started += OnChargeButtonStartedPerformed;
        m_refActions.MoveAction.ChargeButton.canceled += OnChargeButtonCanceledPerformed;
    }

    private void OnDisable()
    {
        m_subjectChargeCanceled.OnNext(Unit.Default);
        if (m_refActions == null)
            return;
        m_refActions.MoveAction.MoveButton.performed -= OnMoveButtonPerformed;
        m_refActions.MoveAction.ChargeButton.started -= OnChargeButtonStartedPerformed;
        m_refActions.MoveAction.ChargeButton.canceled -= OnChargeButtonCanceledPerformed;
        m_refActions.MoveAction.Disable();
    }

    private void OnDestroy()
    {
        if (m_Instance == this)
            m_Instance = null;

        if (m_refActions != null)
        {
            m_refActions.Dispose();
            m_refActions = null;
        }
    }

    private void Update()
    {
        PlayerAction.MoveActionActions tMap = m_refActions.MoveAction;

        m_tInputInfo.MoveDir = tMap.Move.ReadValue<Vector2>().normalized;

        Vector2 vDelta = tMap.Delta.ReadValue<Vector2>();
        if (m_bDeltaInitialized == false && vDelta.sqrMagnitude > 0.0f)
        {
            m_bDeltaInitialized = true;
            vDelta = Vector2.zero;
        }

        m_tInputInfo.Delta = vDelta;
    }

    private void OnMoveButtonPerformed(InputAction.CallbackContext _tContext)
    {
        m_subjectMoveButton.OnNext(Unit.Default);
    }

    private void OnChargeButtonStartedPerformed(InputAction.CallbackContext _tContext) => m_subjectChargeStarted.OnNext(Unit.Default);
    private void OnChargeButtonCanceledPerformed(InputAction.CallbackContext _tContext)
    {
        if (!Application.isFocused || Time.timeScale <= 0f)
            m_subjectChargeCanceled.OnNext(Unit.Default);
        else
            m_subjectChargeReleased.OnNext(Unit.Default);
    }

    private void OnApplicationFocus(bool _bHasFocus)
    {
        if (!_bHasFocus)
            m_subjectChargeCanceled.OnNext(Unit.Default);
    }
}
