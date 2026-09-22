using NUnit.Framework;
using UnityEngine.InputSystem;

/*///////////////////////////////////////////
            InputManagerTests
목적 : InputManager가 소유하는 생성 클래스(PlayerAction)가 PlayerAction.inputactions 에셋과
       올바르게 배선되어 있는지 검증한다. 에셋에서 액션이 사라지거나 이름/바인딩이 바뀌면
       InputManager.Update()는 에러 없이 기본값만 읽게 되므로, 그 조용한 회귀를 여기서 잡는다.

       키 입력 시뮬레이션은 하지 않는다 - InputSystem.AddDevice/Update는 EditMode에서
       에디터의 실제 입력 상태를 건드리고(Destroy in edit mode 에러), 격리 도구인
       InputTestFixture는 asmdef를 요구하는데 asmdef에서는 predefined 어셈블리의
       InputManager를 참조할 수 없다. 실제 값 흐름은 플레이 모드 스모크로 확인한다.
 *///////////////////////////////////////////
public class InputManagerTests
{
    private PlayerAction m_refActions;

    [SetUp]
    public void SetUp()
    {
        m_refActions = new PlayerAction();
    }

    // 생성 클래스의 Dispose()는 Object.Destroy(asset)을 호출하는데(PlayerAction.cs:253)
    // 에디터에서는 금지된 호출이라 에러 로그가 찍히고 테스트가 실패한다.
    // 런타임(InputManager.OnDestroy)에서는 Dispose()가 정상이므로 테스트에서만 즉시 파괴한다.
    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(m_refActions.asset);
        m_refActions = null;
    }

    // 액션의 바인딩 경로 중 하나라도 _strExpectedPath를 포함하는지
    private static bool HasBindingPath(InputAction _refAction, string _strExpectedPath)
    {
        for (int i = 0; i < _refAction.bindings.Count; ++i)
        {
            if (_refAction.bindings[i].path.Contains(_strExpectedPath))
                return true;
        }

        return false;
    }

    // 액션의 존재와 이름은 여기서 검증하지 않는다 - 에셋에서 액션이 사라지거나 이름이 바뀌면
    // PlayerAction.cs 가 재생성되면서 InputManager 가 컴파일 에러로 먼저 터지고,
    // 생성자의 FindAction(throwIfNotFound: true) 이 SetUp 단계에서 예외를 던진다.
    // 컴파일러가 못 잡는 것(바인딩 키 변경, 컨트롤 타입 불일치)만 아래에서 검증한다.

    // InputManager.Update가 ReadValue<Vector2>()로 읽으므로 타입이 어긋나면 값이 죽은 채로 흐른다
    [Test]
    public void 이동_계열_액션은_Vector2_값_타입이다()
    {
        Assert.AreEqual(InputActionType.Value, m_refActions.MoveAction.Move.type);

        Assert.AreEqual("Vector2", m_refActions.MoveAction.Move.expectedControlType);
        Assert.AreEqual("Vector2", m_refActions.MoveAction.MoveScreen.expectedControlType);
        Assert.AreEqual("Vector2", m_refActions.MoveAction.Delta.expectedControlType);
    }

    // IsPressed()는 Button 타입에서만 의미가 있다
    [Test]
    public void MoveButton은_Button_타입이고_Space에_바인딩되어_있다()
    {
        Assert.AreEqual(InputActionType.Button, m_refActions.MoveAction.MoveButton.type);
        Assert.IsTrue(HasBindingPath(m_refActions.MoveAction.MoveButton, "Keyboard>/space"),
            "MoveButton이 Space에 바인딩되어 있지 않음");
    }

    [Test]
    public void Move는_2D_Vector_컴포짓으로_WASD에_바인딩되어_있다()
    {
        Assert.IsTrue(HasBindingPath(m_refActions.MoveAction.Move, "2DVector"), "Move가 2D Vector 컴포짓이 아님");
        Assert.IsTrue(HasBindingPath(m_refActions.MoveAction.Move, "Keyboard>/w"), "Move up이 W에 바인딩되어 있지 않음");
        Assert.IsTrue(HasBindingPath(m_refActions.MoveAction.Move, "Keyboard>/s"), "Move down이 S에 바인딩되어 있지 않음");
    }

    [Test]
    public void 마우스_계열_액션이_각각_position과_delta에_바인딩되어_있다()
    {
        Assert.IsTrue(HasBindingPath(m_refActions.MoveAction.MoveScreen, "position"), "MoveScreen이 position에 바인딩되어 있지 않음");
        Assert.IsTrue(HasBindingPath(m_refActions.MoveAction.Delta, "Mouse>/delta"), "Delta가 마우스 delta에 바인딩되어 있지 않음");
    }

    // Enable/Disable이 맵 단위로 동작하는지 - InputManager가 OnEnable/OnDisable에서 이 API를 쓴다
    [Test]
    public void 맵_Enable_Disable이_액션_전체에_적용된다()
    {
        Assert.IsFalse(m_refActions.MoveAction.Move.enabled, "생성 직후에는 비활성이어야 함");

        m_refActions.MoveAction.Enable();
        Assert.IsTrue(m_refActions.MoveAction.Move.enabled);
        Assert.IsTrue(m_refActions.MoveAction.MoveButton.enabled);

        m_refActions.MoveAction.Disable();
        Assert.IsFalse(m_refActions.MoveAction.Move.enabled);
        Assert.IsFalse(m_refActions.MoveAction.MoveButton.enabled);
    }
}
