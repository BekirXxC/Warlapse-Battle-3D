# GitHub ve Workshop yayın dosyaları

Bu depo, modun GitHub üzerinden indirilmesi ve Workshop sayfasının tanıtım ile indirme bağlantısı olarak kullanılması için düzenlenmiştir.

## İlk yayının içeriği

- GitHub deposu: https://github.com/BekirXxC/Warlapse-Battle-3D
- Sürüm etiketi: `v7.6.1`
- Oyuncu paketi: `Warlapse-Battle-3D-7.6.1-Windows.zip`
- Sürüm açıklaması: [RELEASE-NOTES.md](RELEASE-NOTES.md)
- Workshop Türkçe metni: [WORKSHOP-TR.bbcode.txt](WORKSHOP-TR.bbcode.txt)
- Workshop İngilizce metni: [WORKSHOP-EN.bbcode.txt](WORKSHOP-EN.bbcode.txt)
- Oyuna aktarılabilir boş duyuru ZIP'i: `Warlapse-Battle-3D-Workshop-Duyuru-1.0.0.zip`; kimliği `battle3d-github-info`, görünen adı **Warlapse Battle 3D — Download Notice**.
- Duyuru paketini içe aktarma ve yayınlama: [WORKSHOP-ANNOUNCEMENT.md](WORKSHOP-ANNOUNCEMENT.md).

Oyuncuları Windows ZIP paketine yönlendirin. GitHub'ın otomatik oluşturduğu **Source code** arşivleri kaynakları da içerir; kurulum bağlantısı Windows ZIP'e gider. README'deki doğrudan bağlantı `releases/latest/download/Warlapse-Battle-3D-7.6.1-Windows.zip` biçimindedir. Bu bağlantı için 7.6.1 yayınının taslak olmayan normal bir sürüm olması gerekir. İleride yeni bir sürüm yayımlarken bağlantıdaki dosya adını da güncelleyin.

## Dil ve bağlantı düzeni

GitHub README İngilizce başlar; üstteki English / Türkçe bağlantıları ilgili bölümlere gider. Mevcut `#indir-ve-dene` bağlantısı Türkçe kurulum bölümüne yönelmeye devam eder. `#english` ve `#turkce` yer imlerini koruyun. Mod varsayılan olarak ENG açılır; ana menüde ve 3D arayüzünde TR / ENG seçimi vardır. 3D seçim için Ctrl basılı tutulur ve tercih sonraki açılış için kaydedilir. Modun dil ayarı oyununkinden ayrıdır.

Her iki Workshop metninin başında GitHub proje sayfası ve Releases indirme bağlantısı bulunur.

## Workshop'a eklenecekler

1. `Warlapse-Battle-3D-Workshop-Duyuru-1.0.0.zip` dosyasını oyunun **ModImports** klasörüne koyup **Mods → Import zips** ile aktarın. Bu ZIP gerçek 3D moddan ayrıdır; yalnızca `mod.json`, gönderilen logo olan `preview.png` ve `README.txt` içerir.
   Duyuru klasörü zaten kuruluysa tekrar aktarmak yerine **Mods → Refresh / Yenile** kullanın.
2. **Mods → MOD WORKSHOP** içinde **Warlapse Battle 3D — Download Notice** öğesini seçin. Başka bir mod veya gerçek 3D kurulum klasörünü seçmeyin; yayıncı seçilen mod klasörünü gönderir.
3. Ayrıntılı iki dilli açıklama duyurunun `mod.json` dosyasına eklenmiştir. Ayrı kopyalar için `WORKSHOP-EN.bbcode.txt` ve `WORKSHOP-TR.bbcode.txt` dosyalarını kullanın. Açıklama, paketin boş bir duyuru olduğunu ve abone olmanın 3D modu kurmadığını açıkça belirtir.
4. Workshop logosu `assets/workshop-logo.png` dosyasıdır; kullanıcı görseli değiştirilmeden kopyalanmıştır. Duyuru ZIP'indeki `preview.png` aynı dosyadır. Gerçek modun `assets/logo.png` dosyası ayrı tutulur.
5. İsteğe bağlı ek görsel olarak `assets/workshop-cover.png` tanıtım çizimini ve `assets/screenshot-malazgirt.png` / `assets/screenshot-berlin.png` gerçek oyun görüntülerini ekleyin. Bu görüntüler GitHub'dan indirilen 3D modu gösterir; boş duyuru paketini göstermez.
6. **Publish to Workshop** işleminden önce başlık, açıklama, logo ve GitHub indirmesini kontrol edin. Steam sözleşme isterse kullanıcı kendisi inceleyip onaylar. Yerel yayın ön kontrolünün geçmesi Steam'e yükleme yapıldığı anlamına gelmez.

Workshop metni, gerçek modun GitHub'dan indirileceğini açıkça söyler. Duyuru, desteklenen açık `visuals` kategorisiyle logo/önizleme olarak sınıflandırılmıştır; asker, harita, DLL veya oynanış değişikliği içermez. `Warlapse-Battle-3D-7.6.1-Workshop.zip` açıklama/ek görsel arşividir; oyuna aktarılabilir duyuru ZIP'iyle karıştırmayın. Tam yönergeler [WORKSHOP-ANNOUNCEMENT.md](WORKSHOP-ANNOUNCEMENT.md) içindedir.

## Sonraki sürümleri hazırlama

`VERSION.txt`, `CHANGELOG.md`, `OKU.txt`, README indirme dosya adı, Workshop metinleri ve sürüm notlarını aynı sürüm numarasına getirin. Oynanış veya uyumluluk değiştiyse desteklenen derleme bilgisini ve gerekli kontrolleri de güncelleyin.

Derleme için `tools/Build.ps1`, oyuncu paketi için `tools/Package.ps1` kullanılır. Örnek:

```powershell
.\tools\Build.ps1 -GameFolder 'D:\SteamLibrary\steamapps\common\Warlapse'
.\tools\Package.ps1
```

Paketleme aracı `dist/` içine Windows ZIP'ini ve `.zip.sha256` özetini yazar; özel çıktı klasörü için `-OutputDirectory` kabul eder. `workshop/battle3d-github-info` kaynak klasörü bulunduğunda ayrıca `Warlapse-Battle-3D-Workshop-Duyuru-1.0.0.zip` ve onun `.sha256` özetini üretir. Bu, gerçek 3D kurucudan ayrı üç dosyalı duyuru ZIP'idir. Derleme, yerel Steam oyununun Unity DLL başvurularını ister. Oyunun `Assembly-CSharp.dll`, Unity DLL'leri, çalıştırılabilir dosyaları veya oyun veri klasörleri bu depoya ve Windows ZIP'e eklenmez.

Etiketlemeden önce paket içeriğini ve SHA-256 listesini kontrol edin. Windows ZIP'i ayrı bir klasöre çıkarıp kurulum ile başlatmayı deneyin. Mevcut modlu hedefin üzerine yazmayın; temiz bir hedef kullanın. Özel hedef ile kurulum yaptıysanız `Oyunu-Ac.cmd` dosyasının doğru kopyayı açtığını kontrol edin.

GitHub sürümüne oyuncu ZIP'ini ve oluşturulan SHA-256 özet dosyasını ekleyin; sürüm metni için `RELEASE-NOTES.md` içeriğini kullanın. GitHub Actions yayın akışı `.github/workflows/release.yml` içinde tutulur. İş akışı, oyunun kendisini indirmez veya dağıtmaz.

## Lisans ve görsel kayıtları

Mono.Cecil lisansı her pakette `Mono.Cecil-LICENSE.txt` olarak bulunmalıdır. Modun kaynakları için ayrı bir açık kaynak lisansı seçilmemiştir; bu yayın bir lisans seçimi yapmaz. Bileşenler `THIRD-PARTY-NOTICES.md`, görsel üretim aracı ve istemleri `docs/IMAGE-PROMPTS.md` içinde kayıtlıdır.
