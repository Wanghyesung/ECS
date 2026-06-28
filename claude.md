# Project
- Unity 6 URP 기반 3D 우주선 슈팅 게임
- 특징: 2D 종스크롤/횡스크롤 슈팅 게임의 직관적인 조작감과 규칙을 3D 그래픽 환경으로 구현 

# Architecture & Systems
- 플레이어 FSM (Finite State Machine): 플레이어 상태(Idle, Move, Dead) 
- 몬스터 Behavior Tree (BT): Sequence, Selector, Action 노드로 구성된 트리 구조
- Scriptable Object (SO) 기반 Action: 각 Action 로직은 SO로 작성되어 에디터에서 인스펙터로 할당 및 교체 가능해야 함
- Blackboard System: 몬스터 간의 상태, 타겟 정보, 동적 변수(int, float, bool 등)를 공유하고 전달하기 위한 블랙보드 컴포넌트 필수 사용
- 데이터 분리 규칙: SO는 '데이터와 에디터 세팅'만 가져야 하며, 런타임에 인스턴스별로 동적으로 변하는 상태값은 반드시 'Blackboard'나 런타임 노드 인스턴스에 저장할 것 (SO 데이터 오염 방지)4

- Object Pool: 자주 생성/삭제되는 미사일(Bullet), 이펙트(FX), 에너미(Enemy)에 필수 적용
- Event System: 점수 갱신, 플레이어 피격, 게임 오버 등 UI와 시스템 간 느슨한 결합(Observer 패턴) 유지

# Coding Style & Conventions
- 필드 네이밍: 멤버 변수는 `m_` 접두사 사용 (예: `m_vMoveSpeed`)
- 매개변수는 '_' 사용 예시 ('Function(_fSpeed)))
- 변수 앞에 접두사 float(f), int (i), Vector2,3 (v), List (list), Queue (que), dobudle (d), string (str), Dictionary (hash)
- 클래스/메서드 네이밍: PascalCase 사용 (예: `PlayerController`)

- 최적화: 
  - Update 사용 최소화 (이벤트 기반 전환)
  - GC Alloc 0 추구 (매 프레임 `new` 연산자 사용 금지, 문자열 연산 자제)
  - 인스펙터 노출이 필요한 필드는 `[SerializeField]` 적극 활용
  - 공유 변수는 스크립터블 오브젝트를 사용해서 메모리 최적화
  

# Rules for Claude Code
- 코드를 수정하거나 리팩토링할 때 기존 구조를 최대한 깨뜨리지 않고 유지할 것.
- 만약 성능적으로 더 좋은 방법이 있다면 기존 구조를 깨뜨려도 될 것.
- 새로운 기능을 추가하거나 수정할 때, 그렇게 설계한 이유를 명확히 설명할 것.
- 성능 저하(매 프레임 Alloc 발생 등)를 유발하는 구현은 지양하고 대안을 제시할 것.