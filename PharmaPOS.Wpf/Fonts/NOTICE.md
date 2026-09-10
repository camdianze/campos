# 함께 싣는 글꼴

세 글꼴 모두 **SIL Open Font License 1.1**로 배포된다.

| 파일 | 글꼴 | 쓰이는 곳 | 저작권 / 라이선스 |
|---|---|---|---|
| `Siemreap.ttf` | Siemreap | **화면** (버튼·라벨) | Copyright (c) 2010, Danh Hong — 예약 이름 "Siemreap" · [OFL-Siemreap.txt](OFL-Siemreap.txt) |
| `Battambang-Regular.ttf`<br>`Battambang-Bold.ttf` | Battambang | **인쇄** (영수증·복약안내·미리보기) | Copyright 2019 The Battambang Project Authors · [OFL-Battambang.txt](OFL-Battambang.txt) |
| `AbrilFatface-Regular.ttf` | Abril Fatface | 로그인 화면 제목 | Copyright (c) 2011, TypeTogether · [OFL.txt](OFL.txt) |

둘 다 Danh Hong의 Khmer OS 계열이며, Google Fonts의 `ofl/battambang`·`ofl/siemreap` 배포본을 라이선스 파일과 함께 그대로 가져왔다.

## 왜 화면과 인쇄를 갈랐는가

화면은 Siemreap, 종이는 Battambang이다. 같은 글꼴로 통일하지 않은 것은 두 매체의 조건이 다르기 때문이다 — 감열지는 해상도가 낮고 잉크가 번져서, 화면에서 좋은 글꼴이 종이에서도 좋으리라는 보장이 없다. 어느 쪽이 읽기 좋은지는 현지에서 판단할 일이라 각각 바꿀 수 있게 두었다.

바꾸려면 두 자리만 고치면 된다:

| 매체 | 자리 |
|---|---|
| 화면 | [MainWindow.xaml](../MainWindow.xaml)의 `FontFamily`, [App.xaml](../App.xaml)의 `KhmerFont` |
| 인쇄 | [ThermalTextPrinter](../Services/ThermalTextPrinter.cs)의 `FontFamilyList`, [AdminDashboardView.xaml](../Views/AdminDashboardView.xaml)의 미리보기·입력칸 |

영수증 설정 화면의 입력 칸과 미리보기는 **인쇄 쪽을 따른다.** 치는 동안 보이는 모양과 종이에 나오는 모양이 달라서는 안 된다.

## 왜 싣는가

윈도우에 크메르 글꼴이 **없는 PC가 흔하다.** 개발 PC를 조사했을 때 `C:\Windows\Fonts`에는 크메르 글자를 그릴 수 있는 글꼴이 없었다. 약국 PC에 있으리라 기대할 수 없다.

## 이름만 적으면 실어 놓고도 못 쓴다

WPF는 글꼴 **이름**만 주면 PC에 설치된 글꼴에서만 찾는다. exe 안에 실은 글꼴을 쓰려면 `pack://application:,,,/Fonts/#Battambang`처럼 주소를 함께 줘야 한다.

전에 `Noto Sans Khmer`를 싣고도 이름만 적어 두어서, 실제 인쇄는 Windows 동봉 글꼴로 떨어지고 있었다. **오류가 나지 않아 알아채기 어렵다** — 글자는 멀쩡히 나오고, 다만 의도한 글꼴이 아니다. 개발 PC에는 그 글꼴이 계정별로 설치돼 있어 더 눈에 띄지 않았다.
