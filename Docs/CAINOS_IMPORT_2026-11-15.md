# 2026-11-15 Cainos 원본 에셋 설치

## 작업 범위

- 사용자가 승인한 누락 원본 패키지 설치만 수행한다.
- 게임 코드, 원본 맵/씬, ProjectSettings, Packages는 변경하지 않는다. 예제 씬·스크립트·렌더 파이프라인 패치는 설치하지 않는다.
- Unity 자동 조작 연결이 작동하지 않아 제작자의 공식 무료 배포에서 Unity 패키지를 받고, 원본 파일과 meta를 그대로 프로젝트의 지정 경로에 가져왔다.

## 원본

- Cainos, Pixel Art Top Down - Basic v1.2.3.
- 공식 배포: https://cainos.itch.io/pixel-art-top-down-basic
- Asset Store: https://assetstore.unity.com/packages/2d/environments/pixel-art-top-down-basic-187605
- 제작자의 무료 다운로드 절차를 사용했다. 배포 ZIP 안의 원본 unitypackage를 사용했으며 PNG와 meta를 다시 만들지 않았다.
- 다운로드 ZIP SHA256: `1307DDF193B5D6B1F0FF93C78DCE2D793B4FA471D805EAA2164D072CE449AFDE`.
- 원본 에셋 재배포 금지 조건에 따라 기존 Git 제외 규칙을 유지한다.

## 가져온 파일

- 설치 폴더: `Assets/Cainos/Pixel Art Top Down - Basic/`.
- Texture: PNG 9개. 원래 sprite slice, GUID, 픽셀 설정을 함께 가져왔다.
- Material: 원본 재질 4개.
- Shader: 원본 셰이더 2개.
- Tile Palette/TP Grass: 프로젝트가 직접 참조하는 잔디/꽃/돌길 타일 24개.
- 원본 에셋 파일 총 39개와 폴더를 포함한 meta 46개.
- 누락됐던 MT Tileset, MT Shadow, TX Tileset Grass의 GUID가 기존 씬 참조와 일치함을 확인했다.

## 검증

- 진행 중: 최신 소스의 별도 Unity 복사본에서 실제 에셋 가져오기, 셰이더 검사, Town/Field/BossArena/NetworkGame 재질 참조, Town 월드 렌더링을 검사한다.
- 원본 프로젝트의 기존 미커밋 meta 23개는 해시를 기록하고 보존한다.
- 원본 파일, 가져온 경로 목록과 검증 기록은 `Library/Validation/CainosImport-20261115/`에 보관한다.
- 원본 프로젝트의 에디터 화면을 직접 조작하거나, 원격 서버에 접속하거나, 게임 빌드·배포를 수행하지 않는다.
- 에셋 설치와 확인에 한정된 작업이므로 위임하지 않고 직접 수행한다.
