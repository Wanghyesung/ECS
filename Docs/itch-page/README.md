# 오비탈 itch.io 페이지 시안

ULTRAKILL Prelude의 **큰 타이틀 배너 → 짧은 소개 → 핵심 기능 → 우측 트레일러·스크린샷 → 다운로드** 흐름을 참고했다. 색과 이미지는 오비탈의 실제 우주 전투 및 카드 화면에 맞춰 새로 구성했다.

## 파일

| 파일 | 용도 |
|---|---|
| `preview.html` | 브라우저에서 여는 페이지 시안. 실제 itch.io 페이지가 아니라 구성 미리보기 |
| `cover.png` | 게임 목록 썸네일, 630×500 |
| `header.png` | 페이지 타이틀 배너, 960×300 |
| `background.jpg` | 페이지 배경, 1920×1080 |
| `description-ko.md` | 페이지 본문 문구 초안 |
| `generate_assets.py` | `Docs/Screenshots`의 실제 게임 화면으로 위 이미지 재생성 |

## itch.io에 적용

1. itch.io 계정에서 **Dashboard → Create new game**. 제목 `오비탈 | ORBITAL`, 분류 `Game`, 종류 `Downloadable`, 상태는 실제 빌드에 맞게 설정한다. 카드에 보일 짧은 설명은 `쏘고, 고르고, 다시 건다. 3D 슈팅 로그라이트`를 추천한다.
2. **Cover image**에 `cover.png`. 스크린샷은 `Docs/Screenshots/boss.png`, `levelup-pick3.png`, `joker-success.png`, `joker-fail.png` 순으로 4장 업로드한다. 트레일러가 생기면 공개 YouTube 또는 Vimeo URL을 추가한다.
3. Windows 빌드 폴더의 실행 파일, `_Data` 폴더 등 실행에 필요한 파일을 **함께 ZIP**으로 묶어 업로드하고 Windows 실행 파일로 표시한다. 압축을 푼 복사본에서 실제로 실행되는지 확인한다. Android 빌드는 실제 APK를 테스트하고 올릴 때만 Android를 표시한다.
4. 설명란에 `description-ko.md`의 본문을 넣고 제목은 itch.io 편집기의 **Header 2** 스타일로 바꾼다. 마지막 HTML 주석의 미확정 항목은 게시 전에 실제 값으로 채운다. 에디터가 Markdown 원문을 자동 변환하지 않으면 일반 텍스트로 붙인 뒤 서식을 적용한다.
5. 저장 후 게임 페이지에서 **Edit theme**: Header image=`header.png`, Background image=`background.jpg`, Layout/Screenshots=`Sidebar`. 권장 색상은 아래 표를 적용한다. 배경이 반복되거나 화면에 맞지 않으면 크기·정렬은 테마 편집기에서 미리 보며 조정한다.
6. 비공개로 저장해 로그아웃 상태 또는 시크릿 창에서 모바일과 데스크톱을 확인한다. 다운로드, 설명, 스크린샷, 실행 검증을 마친 뒤 공개로 전환한다.

| 테마 항목 | 색상 |
|---|---|
| BG | `#071426` |
| BG2 | `#06101E` |
| Text | `#EFFAFF` |
| Link / Buttons / Headers | `#63ECFF` |

배경이 텍스트 뒤에서 너무 밝으면 **BG2 Alpha**를 높인다. 기본 테마 편집기만으로 적용할 수 있는 구성이다. `preview.html`의 세부 CSS를 itch.io에 그대로 업로드하는 방식은 지원되지 않으며, 동일한 테두리·간격까지 재현하려면 계정별 **custom CSS 권한**이 필요하다.

## 게시 전 확정할 것

- 출시 상태와 가격/기부 여부
- Windows 빌드의 버전, 실행 파일 이름, 조작법 및 최소 사양
- 문의 연락처와 공식 링크
- 게임 화면과 소개 문구가 배포 빌드와 일치하는지

현재 디자인의 `다운로드 영역 예시`와 `트레일러 자리`는 미리보기용이며 실제 itch.io의 버튼이나 영상이 아니다.
