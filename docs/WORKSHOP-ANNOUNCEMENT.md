# Workshop announcement package / Duyuru paketi

This is a separate **empty announcement and external-download notice**, version **1.0.0**, for the playable Warlapse Battle 3D **7.6.1** mod. Subscribing to this notice does **not** install or activate the 3D mod. The real Windows installer is distributed separately through [GitHub Releases](https://github.com/BekirXxC/Warlapse-Battle-3D/releases).

Bu, oynanabilir Warlapse Battle 3D **7.6.1** modundan ayrı, **1.0.0** sürümlü boş duyuru ve harici indirme paketidir. Abone olmak 3D modu kurmaz veya açmaz. Gerçek Windows kurulum paketi GitHub Releases üzerinden ayrıca indirilir.

## Native package contents

Importable ZIP: `Warlapse-Battle-3D-Workshop-Duyuru-1.0.0.zip`.

```text
battle3d-github-info/
  mod.json
  preview.png
  README.txt
```

The package ID is `battle3d-github-info`; its displayed name is **Warlapse Battle 3D — Download Notice**. The same source package is kept in [`workshop/battle3d-github-info`](../workshop/battle3d-github-info). `mod.json` holds the exact full bilingual announcement description (4,518 UTF-8 bytes) and the supported explicit `visuals` category for its branding/preview. The supplied Workshop logo is used unchanged as `preview.png`. It contains no DLL, executable, installer, units, structures, maps, campaigns, sounds, menu replacement or gameplay scripts. Enabling it does not enable 3D or change gameplay.

Paket kimliği `battle3d-github-info`, görünen adı **Warlapse Battle 3D — Download Notice** olur. `mod.json`, ayrıntılı İngilizce/Türkçe açıklamayı ve logo/önizleme için desteklenen açık `visuals` kategorisini içerir. Gönderilen logo değiştirilmeden `preview.png` olarak kullanılır. DLL, çalıştırılabilir dosya, kurucu, asker, yapı, harita, sefer, ses, menü değişikliği veya oynanış kodu içermez.

## Import and publish in the game / Oyuna aktar ve yayımla

1. Start Warlapse from Steam. Use the game's **Open folder** action to locate its data/modding folders.
2. Put `Warlapse-Battle-3D-Workshop-Duyuru-1.0.0.zip` into **ModImports**, then use **Mods → Import zips**. Alternatively, extract the ZIP so `battle3d-github-info/mod.json` is directly under the game's **Mods** folder.
   If that folder is already installed, use **Mods → Refresh** instead of importing it again.
3. Open **Mods → MOD WORKSHOP**, then select **Warlapse Battle 3D — Download Notice**. Select this notice, not a different mod or the real 3D game's installation folder.
4. Check the title, supplied logo and full bilingual description; then use **Publish to Workshop**. The description is already in `mod.json`; separate language copies are [English](WORKSHOP-EN.bbcode.txt) and [Türkçe](WORKSHOP-TR.bbcode.txt).
5. If Steam asks for its Workshop agreement, review and accept it yourself on the item page. The package has only passed local import/validation/publish preparation; this does not mean it has already been uploaded to Steam.

Türkçe kısa yol: ZIP'i **ModImports** klasörüne koy → **Modlar → Import zips** ile içe aktar → **MOD WORKSHOP → Warlapse Battle 3D — Download Notice → Publish to Workshop**. İstersen ZIP içindeki `battle3d-github-info` klasörünü doğrudan **Mods** içine çıkar. Zaten kuruluysa yeniden içe aktarmak yerine **Mods → Refresh / Yenile** kullan. Başka bir modu seçme; yayıncı seçilen modun klasörünü gönderir.

The publisher uploads the selected notice folder plus its title, description and preview. In this package, that means the three files above. The separate publishing-materials ZIP (`Warlapse-Battle-3D-7.6.1-Workshop.zip`) contains copy/paste descriptions and additional images; it is **not** the ZIP to import as a mod.

Yayıncı, seçilen duyuru klasörünü ve başlık/açıklama/önizleme bilgilerini gönderir. Bu pakette klasör içeriği yalnızca yukarıdaki üç dosyadır. Açıklama ve ek görselleri içeren `Warlapse-Battle-3D-7.6.1-Workshop.zip` ise yayın hazırlık arşividir; oyuna aktarılacak mod ZIP'i değildir.

## Preview and extra images / Logo ve ek görseller

- Workshop preview: `assets/workshop-logo.png`, copied exactly from the user-supplied image. The native announcement ZIP includes the same bytes as `preview.png`.
- Optional promotional artwork: `assets/workshop-cover.png`, labelled as promotional artwork.
- Actual gameplay: `assets/screenshot-malazgirt.png` and `assets/screenshot-berlin.png`. These show the separately downloaded 3D mod, not this notice.

## Online publication status

Preparing/importing this notice does not publish the real Windows download. Before telling players that they can download and play, verify that the Windows ZIP is actually listed on [Releases](https://github.com/BekirXxC/Warlapse-Battle-3D/releases). If no Windows release is visible, the description correctly states that the download has not yet been published.

Duyuru paketini hazırlamak veya içe aktarmak gerçek Windows indirmesini yayımlamaz. Oyunculara indirme çağrısı yapmadan önce Windows ZIP'inin Releases sayfasında göründüğünü kontrol et. Görünmüyorsa açıklamada belirtildiği gibi indirme henüz yayımlanmamıştır.
