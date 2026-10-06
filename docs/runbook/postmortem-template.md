# Postmortem Sablonu

Kullanim: suclama yok (blameless). Bilinmeyeni "bilinmiyor" diye yaz, tahmin yazma. Olculen/okunani cikarimdan ayir. Her aksiyonun sahibi ve tarihi olsun. Bu dosyayi kopyalayip `docs/runbook/postmortems/YYYY-AA-GG-kisa-ad.md` olarak doldur.

---

# Postmortem: <baslik> (<YYYY-AA-GG>)

## Durum ve seviye
- Durum: Acik / Kapandi
- Seviye: SEV1 / SEV2 / SEV3 (`incident-response.md` bolum 2)
- Sure: <baslangic UTC> - <bitis UTC> (bilinmiyorsa "bilinmiyor")
- Sahip: <kisi>

## Ozet
3-4 cumle: ne oldu, etkisi neydi, nasil kapandi.

## Etki
- Kim / ne etkilendi?
- Veri ya da para kaybi var mi? (kanitla; yoksa "iz bulunmadi" ile "olmadi"yi ayir)

## Zaman cizelgesi (UTC)

| Zaman | Olay | Kaynak |
|---|---|---|
| | | |

## Tespit
Olay nasil fark edildi? Otomatik mi, insan mi? Ne kadar gec?

## Kok neden
Neden-neden zinciri; her halka icin kanit ya da "cikarim" notu.

## Neler iyi gitti / Neler kotu gitti / Sans
- Iyi:
- Kotu:
- Sans:

## Aksiyonlar

| Aksiyon | Sahip | Tarih | Durum |
|---|---|---|---|
| | | | Acik / Tamam |

Acik aksiyonlar `docs/backlog/technical-debt.md`'ye de yazilir.

## Olculenler / Olculmeyenler
- Olculen ya da dogrudan okunan:
- Olculmeyen / bilinmeyen:

## Ekler
Log parcalari, commit'ler, ADR'ler, komut ciktilari (sir degeri icermeden).
