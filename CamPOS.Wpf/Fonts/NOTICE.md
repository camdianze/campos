# 함께 싣는 글꼴

네 글꼴 모두 **SIL Open Font License 1.1**로 배포된다.

| 파일 | 글꼴 | 쓰이는 곳 | 저작권 / 라이선스 |
|---|---|---|---|
| `Inter-Regular.ttf`<br>`Inter-SemiBold.ttf`<br>`Inter-Bold.ttf` | Inter 4.1 | **화면 본문** (버튼·라벨·표) | Copyright (c) 2016 The Inter Project Authors · [OFL-Inter.txt](OFL-Inter.txt) |
| `Siemreap.ttf` | Siemreap | **화면의 크메르 글자**와 리엘 기호 ៛ | Copyright (c) 2010, Danh Hong — 예약 이름 "Siemreap" · [OFL-Siemreap.txt](OFL-Siemreap.txt) |
| `Battambang-Regular.ttf`<br>`Battambang-Bold.ttf` | Battambang | **인쇄** (영수증·복약안내·미리보기) | Copyright 2019 The Battambang Project Authors · [OFL-Battambang.txt](OFL-Battambang.txt) |
| `AbrilFatface-Regular.ttf` | Abril Fatface | 로그인 화면 제목, 앱 아이콘 | Copyright (c) 2011, TypeTogether · [OFL.txt](OFL.txt) |

Siemreap과 Battambang은 Danh Hong의 Khmer OS 계열이며, Google Fonts의 `ofl/battambang`·`ofl/siemreap` 배포본을 라이선스 파일과 함께 그대로 가져왔다. Inter는 `github.com/rsms/inter`의 v4.1 배포본에서 `extras/ttf`의 정적 파일을 가져왔다.

## Inter는 왜 세 파일인가

굵기마다 파일이 따로 있어야 한다. WPF는 `FontWeight="SemiBold"`를 만났을 때 그 굵기의 **파일이 없으면 Regular를 굵게 흉내 낸다**(가짜 볼드). 이 앱은 SemiBold를 쉰 군데 가까이 쓰므로 화면 대부분이 흉내 낸 굵기가 된다.

가변 글꼴(`Inter-Variable`)을 쓰지 않은 것도 같은 이유다. **WPF는 가변 축을 지원하지 않아** 기본 인스턴스인 Regular 한 벌만 나온다.

GDI+(System.Drawing)로 보면 `Inter-SemiBold.ttf`의 패밀리 이름이 `Inter SemiBold`라는 별개 가족으로 나오는데, 화면을 그리는 것은 WPF이고 WPF는 typographic 패밀리 이름을 읽어 셋을 `Inter` 하나로 묶는다. 확인해 둔 사실이라 둘이 다르다는 이유로 파일 이름을 고치지 않는다.

## 왜 화면과 인쇄를 갈랐는가

화면은 Siemreap, 종이는 Battambang이다. 같은 글꼴로 통일하지 않은 것은 두 매체의 조건이 다르기 때문이다 — 감열지는 해상도가 낮고 잉크가 번져서, 화면에서 좋은 글꼴이 종이에서도 좋으리라는 보장이 없다. 어느 쪽이 읽기 좋은지는 현지에서 판단할 일이라 각각 바꿀 수 있게 두었다.

바꾸려면 두 자리만 고치면 된다:

| 매체 | 자리 |
|---|---|
| 화면 | [MainWindow.xaml](../MainWindow.xaml)의 `FontFamily`, [App.xaml](../App.xaml)의 `KhmerFont` |
| 인쇄 | [ThermalTextPrinter](../Services/ThermalTextPrinter.cs)의 `FontFamilyList`, [AdminDashboardView.xaml](../Views/AdminDashboardView.xaml)의 미리보기·입력칸 |

화면 쪽 목록은 `#Inter, Segoe UI, #Siemreap` 순서다. 앞에서부터 그 글자를 가진 글꼴이 그린다 — Inter에 없는 `✕`(U+2715)와 `＋`(U+FF0B)는 Segoe UI가, 크메르 글자와 리엘 기호 `៛`(U+17DB)는 Siemreap이 받는다. **Inter 하나만 적으면 금액 옆의 ៛가 PC에 깔린 아무 글꼴로 떨어진다.**

영수증 설정 화면의 입력 칸과 미리보기는 **인쇄 쪽을 따른다.** 치는 동안 보이는 모양과 종이에 나오는 모양이 달라서는 안 된다.

## 왜 싣는가

윈도우에 크메르 글꼴이 **없는 PC가 흔하다.** 개발 PC를 조사했을 때 `C:\Windows\Fonts`에는 크메르 글자를 그릴 수 있는 글꼴이 없었다. 약국 PC에 있으리라 기대할 수 없다.

## 이름만 적으면 실어 놓고도 못 쓴다

WPF는 글꼴 **이름**만 주면 PC에 설치된 글꼴에서만 찾는다. exe 안에 실은 글꼴을 쓰려면 `pack://application:,,,/Fonts/#Battambang`처럼 주소를 함께 줘야 한다.

전에 `Noto Sans Khmer`를 싣고도 이름만 적어 두어서, 실제 인쇄는 Windows 동봉 글꼴로 떨어지고 있었다. **오류가 나지 않아 알아채기 어렵다** — 글자는 멀쩡히 나오고, 다만 의도한 글꼴이 아니다. 개발 PC에는 그 글꼴이 계정별로 설치돼 있어 더 눈에 띄지 않았다.
