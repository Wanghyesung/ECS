---
name: textmeshpro
description: "TextMeshPro 텍스트 렌더링 — 폰트 에셋 생성, 머티리얼 프리셋, 리치 텍스트 태그, 동적 폰트 폴백, 텍스트 내 스프라이트 에셋. Unity의 모든 텍스트 렌더링에 사용할 것."
globs: ["**/TMP_*.cs", "**/TextMesh*.cs", "**/*Text*.cs", "**/*.asset"]
---

# TextMeshPro — Unity의 고급 텍스트 렌더링

TextMeshPro(TMP)는 레거시 Text 컴포넌트를 대체하는 Unity의 표준 텍스트 솔루션이다. SDF(Signed Distance Field) 렌더링을 사용해 해상도에 무관하게 선명한 텍스트를 제공하며, 리치 텍스트, 외곽선, 그림자, 인라인 스프라이트를 지원한다.

## 컴포넌트 종류

| 컴포넌트 | 사용 사례 | 네임스페이스 |
|-----------|----------|-------------|
| `TextMeshProUGUI` | Canvas UI 텍스트 (가장 흔한 경우) | `TMPro` |
| `TextMeshPro` | 월드 스페이스 3D 텍스트 (표지판, 네임 플레이트) | `TMPro` |

```csharp
using TMPro;

[SerializeField] private TextMeshProUGUI m_refUiText;     // Canvas UI 텍스트
[SerializeField] private TextMeshPro m_refWorldText;       // 3D 월드 텍스트
```

## 폰트 에셋 생성

폰트 에셋은 Font Asset Creator 창을 통해 TTF/OTF 폰트 파일로부터 생성된다.

### 단계

1. **Window > TextMeshPro > Font Asset Creator**
2. **Source Font File**을 사용할 TTF/OTF 폰트로 설정
3. 설정 구성:
   - **Sampling Point Size**: Auto 또는 특정 값(예: 64)
   - **Padding**: 5~9 (값이 클수록 외곽선/그림자 품질은 좋아지지만 아틀라스가 커짐)
   - **Packing Method**: Optimum
   - **Atlas Resolution**: 작은 문자 집합은 512x512, CJK는 2048x2048 이상
   - **Character Set**: 영어는 ASCII, 특정 집합은 Unicode Range, 정확한 글리프가 필요하면 Custom Characters
   - **Render Mode**: SDFAA (기본값, 최고 품질)
4. **Generate Font Atlas**를 클릭한 뒤 **Save**

### SDF 렌더링 모드

| 모드 | 품질 | 사용 사례 |
|------|---------|----------|
| SDFAA | 최고 | 대부분의 폰트에 대한 기본값 |
| SDF | 좋음 | 약간 더 빠른 렌더링 |
| SDFAA_HINTED | 최고 + 힌팅 | 작은 텍스트 크기, 픽셀 퍼펙트 |
| Raster | 비트맵 | 픽셀 아트 폰트 전용 |

## 머티리얼 프리셋

머티리얼 프리셋은 기본 폰트 에셋을 수정하지 않고도 시각 효과(외곽선, 그림자, 글로우)를 추가한다.

### 머티리얼 프리셋 생성하기

1. Project 창에서 폰트 에셋을 선택
2. 인스펙터에서 머티리얼 필드를 클릭해 베이스 머티리얼을 확인
3. 폰트 에셋을 우클릭 > **Create > Material Preset**
4. 새 머티리얼에서 속성을 조정:
   - **Face**: 색상, 소프트니스, 확장(dilate)
   - **Outline**: 색상, 두께
   - **Underlay** (그림자): 색상, 오프셋, 확장, 소프트니스
   - **Lighting**: 3D 효과를 위한 베벨
   - **Glow**: 내부/외부 글로우

### 코드에서 머티리얼 프리셋 적용하기

```csharp
[SerializeField] private Material m_refDamageMaterial;  // 빨간 외곽선 프리셋
[SerializeField] private Material m_refDefaultMaterial;

public void ShowDamageText()
{
    m_refUiText.fontSharedMaterial = m_refDamageMaterial;
}

public void ResetTextAppearance()
{
    m_refUiText.fontSharedMaterial = m_refDefaultMaterial;
}
```

**중요**: 인스턴스를 생성해 할당이 발생하는 `fontMaterial` 대신, 공유되며 할당이 없는 `fontSharedMaterial`을 사용하라.

## 리치 텍스트 태그

TMP는 텍스트 문자열 안에서 광범위한 리치 텍스트 마크업을 지원한다.

### 기본 서식

```
<b>굵게</b>
<i>기울임</i>
<u>밑줄</u>
<s>취소선</s>
<mark=#FFFF0044>하이라이트</mark>
<sup>위 첨자</sup>
<sub>아래 첨자</sub>
```

### 색상과 크기

```
<color=#FF0000>빨강</color>
<color="red">이름으로 지정한 빨강</color>
<color=#FF000088>반투명 빨강</color>
<size=24>더 큰 텍스트</size>
<size=+10>상대적으로 더 크게</size>
<size=-4>상대적으로 더 작게</size>
```

### 정렬과 간격

```
<align="center">가운데 정렬</align>
<align="left">왼쪽 정렬</align>
<align="right">오른쪽 정렬</align>
<cspace=5>자간</cspace>
<line-height=150%>더 높은 줄 높이</line-height>
<indent=20>들여쓰기된 텍스트</indent>
<margin=10>여백이 있는 텍스트</margin>
<mspace=0.5em>고정폭</mspace>
```

### 인라인 스프라이트

```
코인: <sprite name="coin"> x 100
체력: <sprite name="heart" tint=1>
<sprite index=0>  (스프라이트 에셋 내 인덱스로 지정)
```

### 링크

```
상점을 열려면 <link="store">여기</link>를 클릭하세요.
<link="https://example.com">우리 사이트</link>를 방문하세요.
```

### 기타 태그

```
<font="OtherFontAsset">다른 폰트</font>
<gradient="GradientPreset">그라디언트 텍스트</gradient>
<rotate=15>회전</rotate>
<voffset=10>수직 오프셋</voffset>
<width=50%>너비 제한</width>
<nobr>여기서는 줄바꿈하지 않음</nobr>
<page>페이지 나눔 (다중 페이지용)</page>
```

## 동적 폰트 폴백

메인 폰트에 없는 글자가 다른 폰트로 대체되도록 폰트 에셋을 체인으로 연결하라. 다국어 지원에 필수적이다.

### 설정

1. 메인으로 쓸 폰트 에셋을 선택
2. 인스펙터에서 **Fallback Font Asset List**를 펼침
3. 우선순위 순서로 폴백 폰트를 추가:
   - 기본: LatinFont (영어, 유럽어)
   - 폴백 1: CJKFont (중국어, 일본어, 한국어)
   - 폴백 2: ArabicFont
   - 폴백 3: EmojiFont

TMP는 메인 폰트에서 글리프를 찾지 못하면 자동으로 폴백 폰트를 검색한다.

### 전역 폴백 (TMP Settings)

1. **Edit > Project Settings > TextMesh Pro > Settings**
2. **Fallback Font Assets** 목록에 폰트를 추가
3. 이는 최후의 수단으로 모든 TMP 텍스트 컴포넌트에 적용된다

## 스프라이트 에셋

아이콘, 이모지, 커스텀 이미지를 텍스트와 함께 인라인으로 삽입한다.

### 스프라이트 에셋 생성하기

1. 스프라이트 시트 텍스처(아이콘 아틀라스)를 만든다
2. **텍스처를 우클릭 > Create > TextMeshPro > Sprite Asset**
3. Sprite Asset 인스펙터에서 스프라이트 영역을 정의한다
4. 쉽게 참조할 수 있도록 각 스프라이트에 이름을 붙인다

### 사용법

```csharp
// 텍스트 문자열 안에서
m_refUiText.text = "Gold: <sprite name=\"coin\"> 500";

// 틴트 적용 (텍스트 색상을 상속함)
m_refUiText.text = "<sprite name=\"heart\" tint=1>";
```

## 코드 접근 패턴

### 효율적으로 텍스트 설정하기

```csharp
[SerializeField] private TextMeshProUGUI m_refScoreText;
[SerializeField] private TextMeshProUGUI m_refTimerText;

// 좋음 — 포맷 인자를 쓰는 SetText는 문자열 할당을 피한다
m_refScoreText.SetText("Score: {0}", score);
m_refTimerText.SetText("{0}:{1:00}", minutes, seconds);

// 좋음 — float 포맷팅을 사용하는 SetText
m_refScoreText.SetText("DPS: {0:2}", damagePerSecond); // 소수점 2자리

// 자주 갱신하지 않는다면 괜찮음
m_refScoreText.text = $"Score: {score}";

// 자주 갱신할 때는 나쁨 — 매 프레임 할당이 발생함
private void Update()
{
    m_refScoreText.text = "FPS: " + (1f / Time.deltaTime).ToString("F1"); // 할당 발생
}

// 자주 갱신할 때 좋음
private void Update()
{
    m_refScoreText.SetText("FPS: {0:1}", 1f / Time.deltaTime); // 무할당
}
```

### SetText 포맷 지정자

```csharp
m_refText.SetText("{0}", intValue);          // 정수
m_refText.SetText("{0:1}", floatValue);      // 소수점 1자리
m_refText.SetText("{0:2}", floatValue);      // 소수점 2자리
m_refText.SetText("{0:00}", intValue);       // 0으로 패딩 (이 경우엔 string.Format을 사용하라)
```

참고: `SetText`의 포맷은 `string.Format`과 동일하지 않다. `:` 뒤의 숫자는 포맷 지정자가 아니라 소수점 자릿수다.

### 텍스트 속성 접근하기

```csharp
// 폰트와 스타일
m_refText.font = myFontAsset;
m_refText.fontSize = 36;
m_refText.fontStyle = FontStyles.Bold | FontStyles.Italic;
m_refText.characterSpacing = 2f;
m_refText.lineSpacing = 10f;
m_refText.wordSpacing = 5f;

// 색상
m_refText.color = Color.white;
m_refText.faceColor = new Color32(255, 255, 255, 200); // 알파를 포함한 표면 색상
m_refText.outlineColor = Color.black;
m_refText.outlineWidth = 0.2f;

// 정렬
m_refText.alignment = TextAlignmentOptions.Center;
m_refText.alignment = TextAlignmentOptions.TopLeft;

// 오버플로
m_refText.overflowMode = TextOverflowModes.Ellipsis;
m_refText.overflowMode = TextOverflowModes.Truncate;
m_refText.enableWordWrapping = true;

// 크기 조절
m_refText.enableAutoSizing = true;
m_refText.fontSizeMin = 12;
m_refText.fontSizeMax = 48;
```

## 링크 처리

`<link>` 태그 클릭을 감지하고 반응하라.

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class LinkHandler : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TextMeshProUGUI m_refText;

    public void OnPointerClick(PointerEventData _eventData)
    {
        int iLinkIndex = TMP_TextUtilities.FindIntersectingLink(
            m_refText, _eventData.position, null // null = 메인 카메라
        );

        if (iLinkIndex >= 0)
        {
            TMP_LinkInfo linkInfo = m_refText.textInfo.linkInfo[iLinkIndex];
            string strLinkId = linkInfo.GetLinkID();
            string strLinkText = linkInfo.GetLinkText();

            Debug.Log($"Clicked link: {strLinkId} ({strLinkText})");
            HandleLink(strLinkId);
        }
    }

    private void HandleLink(string _strLinkId)
    {
        switch (_strLinkId)
        {
            case "store":
                OpenStore();
                break;
            default:
                if (_strLinkId.StartsWith("http"))
                    Application.OpenURL(_strLinkId);
                break;
        }
    }
}
```

## 성능 모범 사례

### Raycast Target 비활성화

Raycast Target이 켜진 모든 TMP 컴포넌트는 UI 레이캐스팅에 참여한다. 상호작용이 필요 없는 텍스트라면 비활성화하라.

```csharp
// 인스펙터에서: 상호작용이 없는 텍스트의 "Raycast Target" 체크 해제
// 또는 코드에서:
m_refText.raycastTarget = false;
```

### 캔버스 최적화

- 전체 UI 배치가 재구축되는 것을 피하려면, 자주 갱신되는 텍스트를 **별도의 Canvas**에 배치하라
- 텍스트 갱신을 위한 `Canvas.willRenderCanvases` 콜백은 아껴서 사용하라
- 정적 텍스트는 한데 묶고, 동적 텍스트는 다른 Canvas에 두어라

### 매 프레임 text 속성 할당을 피하라

```csharp
// 나쁜 예 — 값이 바뀌지 않았어도 매 프레임 메시 재구축을 유발한다
private void Update()
{
    m_refScoreText.text = $"Score: {m_iScore}";
}

// 좋은 예 — 값이 바뀌었을 때만 갱신한다
private int m_iLastDisplayedScore = -1;
private void Update()
{
    if (m_iScore != m_iLastDisplayedScore)
    {
        m_iLastDisplayedScore = m_iScore;
        m_refScoreText.SetText("Score: {0}", m_iScore);
    }
}
```

### 텍스트 정보 접근

```csharp
// 텍스트가 설정된 후 문자/단어/줄 정보에 접근한다
m_refText.ForceMeshUpdate(); // 텍스트 정보가 최신 상태인지 보장한다

TMP_TextInfo textInfo = m_refText.textInfo;
int iCharCount = textInfo.characterCount;
int iWordCount = textInfo.wordCount;
int iLineCount = textInfo.lineCount;

// 개별 문자 정보에 접근한다
TMP_CharacterInfo charInfo = textInfo.characterInfo[0];
Vector3 vBottomLeft = charInfo.bottomLeft;
Vector3 vTopRight = charInfo.topRight;
bool bIsVisible = charInfo.isVisible;
```

## 로컬라이제이션 고려사항

- 사용자에게 노출되는 텍스트는 절대 하드코딩하지 말고, 문자열 키와 로컬라이제이션 시스템을 사용하라
- 대상 언어를 모두 커버하도록 폰트 폴백 체인을 구성하라
- CJK 폰트는 큰 아틀라스가 필요하다(4096x4096 또는 동적 아틀라스)
- 오른쪽에서 왼쪽으로 쓰는 언어(아랍어, 히브리어)는 TMP의 RTL 지원을 활성화해야 한다
- 가장 긴 번역 문자열로 오버플로와 오토 사이징을 테스트하라
- 일부 언어는 영어 대비 30~50% 더 길어진다 — 유연한 레이아웃으로 UI를 설계하라

## 흔한 문제들

### 문자가 안 보임 (네모 박스)

- 폰트 아틀라스에 해당 문자가 없는 경우다. 문자 집합에 추가하거나 폴백 폰트를 사용하라.

### 텍스트가 흐릿함

- 아틀라스 해상도가 너무 낮다. 더 높은 해상도로 다시 생성하라.
- 사용 중인 외곽선/그림자에 비해 패딩이 너무 낮다.
- Canvas Scaler의 참조 해상도가 맞지 않는다.

### 텍스트가 갱신되지 않음

- 텍스트 설정 직후 textInfo를 읽는다면 `ForceMeshUpdate()`를 호출하라.
- 컴포넌트가 활성화되어 있고 Canvas가 활성 상태인지 확인하라.
