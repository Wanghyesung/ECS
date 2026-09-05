---
name: unity-skillify
description: "누적된 세션 학습 내용으로부터 새로운 스킬을 생성합니다. 패턴을 분석하고, SKILL.md 초안을 작성하며, 구조를 검증합니다."
user-invocable: true
args: topic_or_options
---

# /unity-skillify — 학습 내용으로부터 스킬 생성

누적된 세션 데이터로부터 새로운 스킬을 생성합니다: **$ARGUMENTS**

`$ARGUMENTS`를 파싱합니다:
- `--install`로 시작하면, 생성된 스킬이 올바른 디렉터리에 자동으로 배치됩니다
- 나머지 텍스트는 스킬을 생성할 주제입니다

## 작업 흐름

### 1단계: 학습 내용 로드

`.claude/state/learnings.jsonl`을 읽습니다 (v1.3.0 이전 프로젝트를 위한 대체 경로로 `.claude/learnings.jsonl`도 확인).

파일이 존재하지 않거나 비어 있으면, 사용 가능한 학습 내용이 없다고 보고하고 `UNITY_HOOK_PROFILE=strict`로 세션을 실행하여 데이터 수집을 시작하도록 제안합니다.

### 2단계: 주제별 필터링

다음 조건에 해당하는 학습 항목을 필터링합니다:
- `category` 필드가 주제와 일치함
- `patterns` 내 파일 경로가 주제와 관련됨
- `branch` 이름에 주제 키워드가 포함됨
- 도구 사용 패턴이 해당 주제 영역을 암시함

### 3단계: 기존 스킬 교차 참조

`.claude/skills/`의 기존 스킬을 모두 읽어 중복 생성을 피합니다. 이미 이 주제를 다루는 스킬이 있으면 이를 보고하고 새로 만들기보다 기존 스킬을 업데이트하도록 제안합니다.

### 4단계: 스킬 합성

다음을 포함한 완전한 SKILL.md를 생성합니다:

```yaml
---
name: [topic-kebab-case]
description: "[synthesized from learning patterns]"
globs: ["[derived from file patterns in learnings]"]
---
```

콘텐츠 섹션:
- **Overview** — 이 스킬이 다루는 내용 (학습 패턴에서 도출)
- **Patterns** — 여러 세션에서 관찰된 코드 패턴 (C# 예제 포함)
- **Common Mistakes** — 훅 경고를 유발했던 문제들 (경고 데이터에서 도출)
- **Best Practices** — 성공적인 세션에서 나타난 패턴들

### 5단계: 출력

`--install` 플래그가 있는 경우:
1. 주제를 기반으로 카테고리(core/gameplay/systems/platform/genre/third-party)를 결정합니다
2. 디렉터리 `.claude/skills/[category]/[name]/`를 생성합니다
3. 그 위치에 SKILL.md를 씁니다
4. 다음과 같이 보고합니다: "Skill installed at .claude/skills/[category]/[name]/SKILL.md"

`--install` 플래그가 없는 경우:
1. 완전한 SKILL.md 콘텐츠를 출력합니다
2. 다음과 같이 보고합니다: "Draft skill generated. To install, run: /unity-skillify --install [topic]"

## 규칙

- 생성된 스킬은 반드시 유효한 YAML 프론트매터를 가져야 합니다
- 생성된 스킬은 반드시 최소 하나의 펜스 처리된(fenced) 코드 블록 예제를 포함해야 합니다
- 생성된 스킬은 반드시 "Common Mistakes" 또는 이에 준하는 섹션을 포함해야 합니다
- 기존 스킬의 범위를 중복하는 스킬을 생성하지 마세요
- 생성된 스킬은 초점을 유지하고 구체적으로 작성합니다 — 스킬 하나당 주제 하나
