# CLAUDE.md - AI 어시스턴트를 위한 프로젝트 컨텍스트

> `generate-claude-md.sh`에 의해 2026-08-13에 자동 생성됨, 이후 기존 프로젝트 컨벤션을 반영해 수동으로 보강함.
> 이 파일은 가볍게 유지합니다 — 세부 내용은 `.claude/docs/`와 `.claude/rules/`를 참고하세요.

---

## 프로젝트 개요

| 속성 | 값 |
|------|-----|
| **Unity 버전** | 2022.3.62f2 |
| **렌더 파이프라인** | URP |
| **감지된 패키지** | Addressables, AI Navigation, Cinemachine, Recorder, 2D Sprite, Visual Scripting, Input System, TextMeshPro |

**게임 기획:** 3D 슈팅 · 로그라이트. 자세한 내용(컨셉, 게임 루프, 조커 카드 시스템 등)은 `.claude/docs/game-design.md` 참고.

---

## Claude Code 작업 규칙

- 코드를 수정하거나 리팩토링할 때 기존 구조를 최대한 깨뜨리지 않고 유지할 것.
- 만약 성능적으로 더 좋은 방법이 있다면 기존 구조를 깨뜨려도 됨.
- 새로운 기능을 추가하거나 수정할 때, 그렇게 설계한 이유를 명확히 설명할 것.
- 성능 저하(매 프레임 Alloc 발생 등)를 유발하는 구현은 지양하고 대안을 제시할 것.
- 콘솔/로그에서 오류를 발견하면 "오류가 있다"고만 보고하지 말 것. 반드시 (1) 왜 그 오류가 발생하는지 근본 원인을 코드/씬 구조까지 추적해서 분석하고, (2) 무엇이 실제 문제인지 구체적으로 짚고, (3) 어떻게 고쳐야 하는지 해결 방법까지 제시할 것. 진단만 하고 아직 고치지 않은 문제는 아래 "커스텀 노트"에 기록해 둘 것.

---

## 아키텍처 (엔진 감지 정보)

### 어셈블리 정의 (Assembly Definitions)

- `UniTask.Editor` (Assets/Plugins/UniTask/Editor/UniTask.Editor.asmdef)
- `UniTask.Addressables` — 의존성: com.unity.addressables, com.unity.addressables.cn (Assets/Plugins/UniTask/Runtime/External/Addressables/UniTask.Addressables.asmdef)
- `UniTask.DOTween` — 의존성: com.demigiant.dotween (Assets/Plugins/UniTask/Runtime/External/DOTween/UniTask.DOTween.asmdef)
- `UniTask.TextMeshPro` — 의존성: com.unity.textmeshpro, com.unity.ugui (Assets/Plugins/UniTask/Runtime/External/TextMeshPro/UniTask.TextMeshPro.asmdef)
- `UniTask.Linq` (Assets/Plugins/UniTask/Runtime/Linq/UniTask.Linq.asmdef)
- `UniTask` — 의존성: com.unity.modules.assetbundle, com.unity.modules.physics, com.unity.modules.physics2d, com.unity.modules.particlesystem, com.unity.ugui, com.unity.modules.unitywebrequest (Assets/Plugins/UniTask/Runtime/UniTask.asmdef)

게임 시스템 아키텍처(Behavior Tree, ScriptableObject Action, Blackboard, Object Pool, Event System 등)와 코드 레이어링(MVS) 규칙은 `.claude/rules/architecture.md` 참고.

---

### 빌드에 포함된 씬

_EditorBuildSettings에서 씬을 찾을 수 없습니다._

---

## 빌드 타겟

- **주 타겟:** Windows
- **부 타겟:** Android (최적화 시)
- **CI:** _여기에 CI 설정을 설명하세요_

---

## 컨벤션

- `.claude/rules/` 아래의 규칙 파일에 정의된 코딩 표준을 반드시 따르세요.
- C# 네이밍/최적화 컨벤션(이 프로젝트는 `m_` 접두사 + 타입별 헝가리안 표기를 사용하는 고유 컨벤션임)은 `.claude/rules/csharp-unity.md` 참고.
- 컴포넌트 참조는 `Awake()`/`Start()`에서 캐싱하고, 핫 루프에서는 절대 `GetComponent`를 호출하지 마세요.
- 컴파일 속도를 빠르게 유지하기 위해 어셈블리 정의를 사용하세요.
- 모든 직렬화된 에셋은 Unity YAML(Force Text) 직렬화를 사용해야 합니다.

---

## 로드할 스킬

감지된 패키지를 기반으로, 다음 Claude 스킬/컨텍스트 파일을 로드하는 것을 고려하세요:

- `unitask` — 감지된 패키지 목록엔 없지만 `Assets/Plugins/UniTask`로 프로젝트 전역에 쓰임(코루틴 대체 필수 컨벤션, `.claude/rules/unity-specifics.md` 참고)
- `addressables` — Addressables 패키지 감지됨
- `dotween` — `UniTask.DOTween` asmdef로 확인됨(com.demigiant.dotween 의존)
- `input-system` — Input System 패키지 감지됨(New Input System 필수 컨벤션, `.claude/rules/unity-specifics.md` 참고)
- `object-pooling` — 이 프로젝트 전용 풀링 구조. 자주 생성/삭제되는 Bullet/FX/Enemy에 필수 적용(`.claude/rules/architecture.md` 참고)

---

## 커스텀 노트

<!-- AI 어시스턴트를 위한 프로젝트별 노트, 주의사항, 컨텍스트를 여기에 추가하세요. -->

세션 중 발견했지만 아직 고치지 않은 문제(진단 완료, 수정 미적용)는 `.claude/docs/known-issues.md`에 기록합니다. 작업 시작 전에 한 번 확인해서 이미 알려진 문제인지 체크하세요.
