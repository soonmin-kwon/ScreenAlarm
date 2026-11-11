# Screen Alarm GitHub 배포 가이드

이 문서는 개발 경험이 없어도 GitHub에 새 버전을 올릴 수 있도록 작성되었습니다.

## 권장 배포 형태

- 소스 코드: GitHub 저장소
- 사용자용 파일: GitHub Releases의 `ScreenAlarm-win-x64.zip`
- 버전 이름: `v1.0.0`, `v1.0.1`, `v1.1.0` 형식

사용자가 .NET을 별도로 설치하지 않아도 되도록 Windows x64 self-contained 배포를 사용합니다.

## 1. 배포 파일 만들기

프로젝트 폴더에서 PowerShell을 열고 다음 명령을 실행합니다.

```powershell
dotnet publish ScreenAlarm.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish\ScreenAlarm-win-x64
```

완료되면 `publish\ScreenAlarm-win-x64` 폴더에 다음 파일이 있는지 확인합니다.

- `ScreenAlarm.exe`
- `USER_GUIDE.md`

폴더 전체를 `ScreenAlarm-win-x64.zip`으로 압축합니다. EXE만 따로 압축하지 마세요.

## 2. GitHub 저장소 만들기

1. GitHub에 로그인합니다.
2. 오른쪽 위 `+` 버튼을 누르고 `New repository`를 선택합니다.
3. 저장소 이름을 `ScreenAlarm`으로 입력합니다.
4. 공개 배포라면 `Public`을 선택합니다.
5. `Create repository`를 누릅니다.

소스 업로드가 익숙하지 않다면 GitHub Desktop을 설치한 뒤 프로젝트 폴더를 저장소로 추가하고
`Publish repository`를 사용하는 방법이 가장 간단합니다.

## 3. 첫 Release 올리기

1. GitHub 저장소 페이지 오른쪽의 `Releases`를 선택합니다.
2. `Create a new release` 또는 `Draft a new release`를 누릅니다.
3. `Choose a tag`에서 `v1.0.0`을 입력하고 새 태그를 만듭니다.
4. Release 제목에 `Screen Alarm v1.0.0`을 입력합니다.
5. 변경 사항과 간단한 사용법을 본문에 적습니다.
6. `ScreenAlarm-win-x64.zip`을 첨부 영역에 끌어 놓습니다.
7. `Publish release`를 누릅니다.

이후 사용자에게 GitHub 저장소의 `Releases` 주소를 전달하면 됩니다.

## 4. 새 버전 배포하기

1. 프로그램을 수정합니다.
2. 위의 `dotnet publish` 명령을 다시 실행합니다.
3. 새 ZIP을 만듭니다.
4. 새 Release를 만들고 버전을 올립니다.
   - 작은 버그 수정: `v1.0.0` → `v1.0.1`
   - 기능 추가: `v1.0.1` → `v1.1.0`
5. 이전 ZIP을 수정하지 말고 새 Release에 새 ZIP을 첨부합니다.

## 배포 전 확인 목록

- 다른 PC에서도 EXE가 실행되는가
- 외부 GIF 없이 내장 시계와 카운트다운이 표시되는가
- 우클릭 메뉴와 리소스 설정 창이 열리는가
- 저장·취소 버튼이 보이는가
- 재실행 후 위치와 크기가 유지되는가
- 알람 GIF와 사운드가 정상 동작하는가
- ZIP에 사용자 안내서가 포함됐는가

## Windows 경고 안내

코드 서명을 하지 않은 개인 제작 EXE는 Windows에서 알 수 없는 게시자 경고가 표시될 수
있습니다. 사용자에게 경고를 무조건 무시하라고 안내하지 말고, 공식 GitHub Release 주소와
파일 이름을 확인하도록 안내하세요. 이용자가 많아지면 정식 코드 서명 인증서를 사용하는 것을
권장합니다.
