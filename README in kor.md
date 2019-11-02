# Overview

PowerToys는 생산성을 높이기 위해 사용자가 윈도우 환경을 조정하고 능률화 할 수 있는 유틸리티 집합이다.  

이는 [Windows 95 era PowerToys project](https://en.wikipedia.org/wiki/Microsoft_PowerToys)에서 영감을 받았으며, 사용자에게 윈도우 10에서 높은 효율성을 이끌어내고 개별적인 작업환경을 사용자 지정할 수 있도록 제공한다. 윈도우 95 PowerToys의 개요를 보고 싶다면 아래 링크를 따라가면 된다. [링크](https://socket3.wordpress.com/2016/10/22/using-windows-95-powertoys/)

![logo](doc/images/Logo.jpg)

## 설치

_(참고: PowerToys를 실행하고 싶다면 윈도우 빌드버전이 17134 이상이어야 한다. 또한, 현재 윈도우 ARM 시스템은 지원하지 않는다.)_

최근 버전의 PowerToys를 다운로드할 수 있는 방법에는 여러가지가 있다. 우리가 현재 권장하는 방법은 GitHub를 이용하는 것이다.

### GitHub

[PowerToys GitHub 설치 페이지](https://github.com/Microsoft/powertoys/releases)로부터 설치가 가능하다. 해당 페이지에서 `Assets`을 클릭하여 설치 가능한 파일들을 확인하고 `PowerToysSetup.msi`를 클릭하여 설치관리자를 다운로드 할 수 있다. <br />
PDB 기호는 압축파일, `PDB symbols.zip`을 통해 따로 제공된다.

### Chocolatey (비공식)

[Chocolatey](https://chocolatey.org)라는 홈페이지에 들어가서 PowerToys를 검색해 다운로드하고 업그레이드를 할 수 있다. 

아래 명령을 PowerShell에서 실행하여 설치가 가능하다:

```powershell
choco install powertoys
```

아래 명령을 PowerShell에서 실행하여 업그레이드가 가능하다:

```powershell
choco upgrade powertoys
```

만약 설치나 업그레이드에서 문제가 있다면 [패키지 페이지](https://chocolatey.org/packages/powertoys)로 들어가서 [Chocolatey triage process](https://chocolatey.org/docs/package-triage-process)를 따르면 된다. 

## 빌드 상태

[![Build Status](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=master)](https://dev.azure.com/ms/PowerToys/_build?definitionId=35096)

## PowerToy 유틸리티

### FancyZones

[FancyZones](/src/modules/fancyzones/) - 이는 윈도우 관리자로, 복잡한 윈도우 레이아웃을 쉽게 만들고 신속하게 배치할 수 있다. FancyZones의 백 로그는 [여기](https://github.com/Microsoft/PowerToys/tree/master/doc/planning/FancyZonesBacklog.md)를 가면 된다.

### Shortcut

[윈도우 키 바로가기 가이드](/src/modules/shortcut_guide) -사용자가 윈도우 키를 1초 이상 누르고 있으면 사용 가능한 바로가기가 현재 화면에 보여 진다. Shortcut의 백 로그는 [여기](https://github.com/Microsoft/PowerToys/tree/master/doc/planning/ShortcutGuideBacklog.md)를 가면 된다.

### PowerRename

[PowerRename](/src/modules/powerrename) - 검색 및 교체 혹은 대량의 이름 바꾸기를 위한 윈도우 셸의 확장 기능이다. 이를 이용하여 간편하게 검색 및 교체가 가능하며 일정 표현의 매칭이 가능하다. 사용자가 검색과 교체 입력 부분에 입력하는 동안 미리보기 영역에 바뀔 항목의 이름이 보인다. 그런 다음에 윈도우 탐색기 파일 작업 엔진을 호출하여 이름을 바꾼다. 이는 PowerRename이 종료되어도 실행취소가 가능하다는 장점이 있다.

Chris Davis가 그의 [SmartRename tool](https://github.com/chrdavis/SmartRename)을 PowerToys에 공헌했다!

### Pipeline으로의 추가 유틸리티로

* 새로운 데스크톱 위젯 최대화 - 사용자가 MTND 위젯에서 사용자가 창을 최대화 혹은 복원 버튼 위로 마우스를 가져 가면 팝업 버튼이 표시된다. 클릭하면 새로운 데스크톱이 생성되고 해당 데스크톱으로 앱이 전송되며 새로운 데스크톱에서는 최대화가 된다.
* [프로세스 종료 도구](https://github.com/indierawk2k2/PowerToys-1/blob/master/specs/Terminate%20Spec.md)
* [애니메이션 GIF 화면 레코더](https://github.com/indierawk2k2/PowerToys-1/blob/master/specs/GIF%20Maker%20Spec.md)

### 백 로그

모든 백 로그는 [여기](https://github.com/Microsoft/PowerToys/tree/master/doc/planning/PowerToysBacklog.md)로 가면 된다.

## 무엇이 일어났는가

### 10월 업데이트

**업데이트** - 0.12버전이 나왔다! 이 버전은 버그 수정을 포함하며 문제들을 해결하고 많은 기능 제안들을 구현했다. 이 빌드와 설치는 Microsoft사에서 서명했다. 마지막으로 이 버전에 새로운 유틸리티가 추가되었다: Chris Davis가 그의 [SmartRename tool](https://github.com/chrdavis/SmartRename)을 PowerToys에 통합했다!

![SmartRename](https://github.com/microsoft/PowerToys/raw/master/src/modules/powerrename/images/PowerRenameDemo.gif)

## 개발자 안내

### 전제 조건 작성

* PowerToys를 빌드하고 실행하기 위해선 Windows 10 1803(빌드 10.0.17134.0) 이상
* C++를 이용한 데스크톱 개발 구성요소 및 Windows 10 SDK 버전 10.0.18362.0 이상이 포함된 Visual Studio 2019 Community Edition 이상

### 코드 작성

* Visual Studio에서 `powertoys.sln`를 열고 `Solutions Configuration` 메뉴에서 `Release`나 `Debug`를 선택하고 `Build` 메뉴에서 `Build Solution`을 선택한다.
* PowerToys의 바이너리는 `x64\Release` 저장소 안에 있다.
* 만약 `PowerToys.exe` 바이너리는 다른 장소로 복사하고 싶다면 `moduls`와 `svgs` 폴더 또한 같이 복사해야 한다.

### 설치 프로그램 빌드하기 위한 전제 조건

* [WiX Toolset Visual Studio 2019 Extension](https://marketplace.visualstudio.com/items?itemName=RobMensching.WiXToolset) 설치
* [WiX Toolset build tools](https://wixtoolset.org/releases/) 설치

### .msi 설치 프로그램 빌드

* 설치 프로그램 폴더로부터 `PowerToysSetup.sln`를 Visual Studio에서 열고 `Solutions Configuration` 메뉴에서 `Release`나 `Debug`를 선택하고 `Build`메뉴에서 `Build Solution`을 한다.
* 그 결과, `Installer\PowerToysSetup\x64\Release\` 폴더에서 `PowerToysSetup.msi` 설치 프로그램이 실행 가능하게 된다.

### 디버깅

다음 구성 문제는 오직 관리자 그룹의 구성원인 경우에만 해당한다.

현재 사용자가 관리자 그룹의 구성원일 경우 일부 PowerToys의 모듈이 최고 수준의 권한으로 실행해야 한다. 작업 관리자 같은 높은 응용 프로그램이 우위에 있거나 작업의 대상일 경우 일부 작업을 수행하기 위해서는 최고 수준의 권한이 필요하다. 높은 권한이 없다면 일부 PowerToys의 모듈이 작동은 하지만 몇 가지 제한이 걸린다:

* `FancyZones`에서 더 높은 영역의 창으로 이동이 불가능하다.
* 우위의 창이 높은 응용 프로그램에 포함된다면 `Shorcut Guide`가 나타나지 않는다.

사용자가 관리자 그룹의 구성원일 때 Visual Studio에서 PowerToys를 실행하고 디버그 하려면 상승된 권한으로 Visual Studio를 실행해야 한다. Visual Studio를 상승된 권한으로 실행하지 않고 위에 나온 제한을 무시하고 싶다면, 다음을 수행해야 한다: `runner` 프로젝트 속성을 열고 `Linker -> Manifest file` 설정으로 들어가 `UAC Execution Level`의 설정을 `highestAvailable`에서 `asInvoker`로 바꾸고 저장한다.

### 새로운 PowerToys 만들기

다음 링크 [how to install the PowerToys Module project template](tools/project_template)를 참조한다. <br />
[PowerToys 설정 API](doc/specs/PowerToys-settings.md) 사양

### 코딩 안내

코딩 표준 등에 대한 아래의 간단한 문서를 검토해야 한다.

> 👉 문서에서 빠진 것을 발견했다면 저장소의 어느 곳에서나 문서 파일에 자유롭게 컨트리뷰트한다(혹은 새롭게 만든 저장소에).

이 작업은 프로젝트에 효과적으로 기여하기 위해 사람들에게 무엇을 제공해야 하는지를 배우며 진행중인 작업이다. 

* [코딩 스타일](doc/coding/style.md)
* [코드 구성](doc/coding/organization.md)

## 기여

이 프로젝트는 기여와 제안을 환영하며 파워 유저 커뮤니티와 협력하여 윈도우를 최대한 활용하는 데 도움이 되는 도구 세트를 구축하게 되어 기쁘게 생각합니다.

우리는 **당신이 기여하고 싶은 기느에 대한 작업을 시작하기 전에**, [기여자 안내](contributing.md)를 읽는 것을 부탁드립니다. 우리는 기꺼이 최선의 접근법을 찾고, 기능 개발 전반에 걸쳐 지침과 멘토링을 제공하며, 낭비되거나 중복되는 노력을 피할 수 있도록 도와드릴 것입니다.

> ⚠ **참고**: PowerToys는 아직 초기 프로젝트이며 팀은 적극적으로 작업하고 있습니다. 우리는 코드를 이해하고 탐색하며 빌드하고 테스트하여 기여하기 쉽도록 코드를 주기적으로 재구성할 것이며 **정기적으로 코드 레이아웃이 크게 변경될 것으로 예상됩니다**.

> ⚠ **라이센스 정보**: 대부분의 기여를 하기 위해서는 기여를 사용할 권한을 실제로 부여할 권리가 있음을 선언하는 기여자 라이선스 계약에 동의해야 합니다. 자세한 내용은 https://cla.opensource.microsoft.com을 방문하십시오.

Pull request를 제출하게 되면, CLA-bot이 CLA 제공 여부를 자동으로 결정하고 PR을 적절하게 장식합니다. Bot이 제공하는 지침을 따라 주십시오. CLA를 사용하여 모든 저장소에서 한번만 수행하면 됩니다.

## 행동 강령

이 프로젝트는 [Microsoft 오픈 소스 행동 강령][conduct-code]을 채택했습니다. 자세한 내용은 [행동 강령 FAQ][conduct-FAQ]을 찾아보거나 질문이나 의견이 있으면 [opencode@microsoft.com][conduct-email]을 통해 문의하십시오.

[conduct-code]: https://opensource.microsoft.com/codeofconduct/ 
[conduct-FAQ]: https://opensource.microsoft.com/codeofconduct/faq/
[conduct-email]: mailto:opencode@microsoft.com

## 개인 정보 보호 정책

응용 프로그램은 기본 원격 측정을 기록합니다. 원격 측정 데이터 페이지에는 원격 분석의 트렌드가 있습니다. 자세한 내용은 [Microsoft 개인 정보 보호 정책](http://go.microsoft.com/fwlink/?LinkId=521839)을 읽어 보십시오.
