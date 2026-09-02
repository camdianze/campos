# 함께 싣는 글꼴

두 글꼴 모두 **SIL Open Font License 1.1**로 배포된다. 라이선스 본문은 [OFL.txt](OFL.txt)에 있다.

| 파일 | 글꼴 | 저작권 |
|---|---|---|
| `AbrilFatface-Regular.ttf` | Abril Fatface | Copyright (c) 2011, TypeTogether — 예약 이름 "Abril", "Abril Fatface" |
| `NotoSansKhmer-Regular.ttf`<br>`NotoSansKhmer-Bold.ttf` | Noto Sans Khmer | Copyright The Noto Project Authors (https://github.com/notofonts/khmer) |

## 왜 크메르 글꼴을 싣는가

윈도우에 크메르 글꼴이 **없는 PC가 흔하다.** 개발 PC를 조사했을 때 `C:\Windows\Fonts`에는 크메르 글자를 그릴 수 있는 글꼴이 없었고, `Noto Sans Khmer`는 계정별 폴더에만 설치돼 있었다. 약국 PC에 그것이 있으리라 기대할 수 없다.

없으면 크메르어가 **네모(두부)로 나온다.** 화면이든 영수증이든 마찬가지고, 조용히 그렇게 되기 때문에 만든 사람은 알아채지 못한다. [ThermalTextPrinter](../Services/ThermalTextPrinter.cs)의 글꼴 목록이 `Noto Sans Khmer`를 첫 번째로 부르고 있었지만 정작 그 글꼴을 싣지 않고 있었다.

## 배포 전 확인할 것

OFL은 글꼴과 함께 **해당 글꼴의 저작권 고지와 라이선스 본문**을 배포하도록 요구한다. 지금 `OFL.txt`는 Abril Fatface 배포본에서 온 것이라 첫머리의 저작권 줄이 Abril의 것이다. 라이선스 본문 자체는 두 글꼴에 동일하지만, **Noto 배포본에 딸린 `OFL.txt`를 받아 함께 넣어 두는 편이 분명하다.**
