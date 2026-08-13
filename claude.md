# CLAUDE.md - AI 어시스턴트를 위한 프로젝트 컨텍스트

> `generate-claude-md.sh`에 의해 2026-08-13에 자동 생성됨.
> 이 파일을 검토하고 프로젝트의 세부 사항에 맞게 수정하세요.

---

## 프로젝트 개요

| 속성 | 값 |
|------|-----|
| **Unity 버전** | 2022.3.62f2 |
| **렌더 파이프라인** | URP |
| **감지된 패키지** | Addressables, AI Navigation, Cinemachine, Recorder, 2D Sprite, Visual Scripting, Input System, TextMeshPro |

---

## 아키텍처

### 어셈블리 정의 (Assembly Definitions)

- `UniTask.Editor` (Assets/Plugins/UniTask/Editor/UniTask.Editor.asmdef)
- `UniTask.Addressables` — 의존성: com.unity.addressables, com.unity.addressables.cn (Assets/Plugins/UniTask/Runtime/External/Addressables/UniTask.Addressables.asmdef)
- `UniTask.DOTween` — 의존성: com.demigiant.dotween (Assets/Plugins/UniTask/Runtime/External/DOTween/UniTask.DOTween.asmdef)
- `UniTask.TextMeshPro` — 의존성: com.unity.textmeshpro, com.unity.ugui (Assets/Plugins/UniTask/Runtime/External/TextMeshPro/UniTask.TextMeshPro.asmdef)
- `UniTask.Linq` (Assets/Plugins/UniTask/Runtime/Linq/UniTask.Linq.asmdef)
- `UniTask` — 의존성: com.unity.modules.assetbundle, com.unity.modules.physics, com.unity.modules.physics2d, com.unity.modules.particlesystem, com.unity.ugui, com.unity.modules.unitywebrequest (Assets/Plugins/UniTask/Runtime/UniTask.asmdef)

### 빌드에 포함된 씬

_EditorBuildSettings에서 씬을 찾을 수 없습니다._

---

## 빌드 타겟

<!-- 실제 타겟에 맞게 수정하세요 -->
- **주 타겟:** PC / Mac Standalone
- **부 타겟:** Android / iOS
- **CI:** _여기에 CI 설정을 설명하세요_

---

## 컨벤션

- `.claude/rules/` 아래의 규칙 파일에 정의된 코딩 표준을 따르세요.
- public 멤버에는 PascalCase, private 필드에는 camelCase(언더스코어 접두사 포함)를 사용하세요.
- `== "tag"`보다 `CompareTag()`를 우선 사용하세요.
- 컴포넌트 참조는 `Awake()`/`Start()`에서 캐싱하고, 핫 루프에서는 절대 `GetComponent`를 호출하지 마세요.
- 컴파일 속도를 빠르게 유지하기 위해 어셈블리 정의를 사용하세요.
- 모든 직렬화된 에셋은 Unity YAML(Force Text) 직렬화를 사용해야 합니다.

---

## 로드할 스킬

감지된 패키지를 기반으로, 다음 Claude 스킬/컨텍스트 파일을 로드하는 것을 고려하세요:

- `unity-general`
- `unity-addressables`
- `unity-cinemachine`
- `unity-input-system`

---

## 커스텀 노트

<!-- AI 어시스턴트를 위한 프로젝트별 노트, 주의사항, 컨텍스트를 여기에 추가하세요. -->
