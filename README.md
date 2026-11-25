# Project Setup

**One window to start a new Unity 6 project**: folder structure, Atthree and Unity packages, project
settings, `.gitignore` / `.gitattributes` and starter scenes. Tick what you want, click **Run Setup**.

[English](#english) · [Türkçe](#türkçe)

---

<a name="english"></a>
# English

- **Window > Atthree > Project Setup**, with five tabs: **Project**, **Folders**, **Packages**,
  **Settings**, **Git & Scenes**.
- **Run Setup (all steps)** does everything. Each tab also has its own button.
- **Safe to run again**: existing folders, scenes and git files are kept and reported as skipped.
- **Your choices are remembered** (EditorPrefs), so in your next project they are already ticked.
- Opens **by itself once** in a project that doesn't have the root folder yet (you can turn this off).
- **Editor only**: nothing from this package ends up in your build.

**Requirements:** Unity 6 (6000.0+). For *Git Init*, git must be installed.

## Step-by-step

### 1. Add the package

**Window > Package Manager** → **+** → **Install package from git URL…** →

```
https://github.com/Atthree/ProjectSetup.git
```

To lock a version: `https://github.com/Atthree/ProjectSetup.git#v0.1.2`

### 2. Open the window

**Window > Atthree > Project Setup**. In a new project it opens by itself.

### 3. Go through the tabs

| Tab | What it does |
|---|---|
| **Project** | Company and product name. **Application id** (`com.company.product`) for Standalone, Android and iOS. **Root namespace**: scripts made with *Create > C# Script* get it (default `Company.Product`). |
| **Folders** | Your folders go under `Assets/_Project/`, so they stay apart from Asset Store packs. Edit the list (drag to reorder, + / −). **Add .gitkeep** puts an empty file in empty folders, because git doesn't store empty folders. |
| **Packages** | **Atthree packages** (GitHub, locked to a tagged version, or the newest with *Atthree: Newest Version*) and common **Unity packages** (Input System, Cinemachine, ProBuilder, AI Navigation, Splines, Timeline…). Installed ones show **✓ installed**. |
| **Settings** | Linear color space, Force Text serialization (readable diffs), Visible Meta Files, **Active Input Handling** (default *Both*: old `Input.GetKey` code and the Input System both work). |
| **Git & Scenes** | Writes `.gitignore` and `.gitattributes` (optionally with **Git LFS**), runs `git init`. **Extra .gitignore Lines** for things you may not publish, e.g. paid packs (`/Assets/Synty/`). Creates starter scenes (**MainMenu**, **Game**) with a camera and a light, and adds them to the build list. |

### 4. Run Setup

Click **Run Setup (all steps)**. The list at the bottom shows what happened:
**✓** done, **–** skipped (already there), **!** warning.

- Package installation runs in the background and comes last. Unity recompiles when it's done.
- If **Active Input Handling** changed, click **Restart Unity Now** (Unity needs a restart for it).
- If a scene has unsaved changes, Unity asks to save it before the starter scenes are created.

## Use it from code

Every step is a public method, so you can make your own menu item or run it in CI:

```csharp
using Atthree.ProjectSetup;
using UnityEditor;

public static class MyStudioSetup
{
    [MenuItem("Tools/My Studio Setup")]
    static void Run()
    {
        var profile = SetupProfile.CreateDefault();
        profile.companyName = "My Studio";
        profile.rootFolder = "_Game";
        profile.folders.Add("Art/Shaders");
        foreach (var p in profile.packages) p.selected = p.id is "com.unity.inputsystem" or "com.atthree.event-bus";

        var report = new SetupReport();
        SetupSteps.ApplyProjectInfo(profile, report);
        SetupSteps.ApplySettings(profile, report);
        SetupSteps.CreateFolders(profile, report);
        SetupSteps.WriteGitFiles(profile, report);
        SetupSteps.CreateScenes(profile, report);
        SetupSteps.InstallPackages(profile, report); // last: Unity recompiles afterwards
        report.LogToConsole();
    }
}
```

## Where to use it

| Situation | Use |
|---|---|
| Every new prototype or game jam | Run Setup once: folders, packages, git and scenes in under a minute |
| Projects with Asset Store packs | Root folder keeps your files apart; **Extra .gitignore Lines** keeps paid packs out of public repos |
| Teams | Same folder layout, namespace and git rules in every project; `.gitkeep` keeps empty folders on every computer |
| A studio template | Call `SetupSteps` from your own menu item with your own folders and packages |

## Good to know

- **Company and product name always come from the open project**. Everything else is remembered
  between projects. **Reset to Defaults** brings back the original choices.
- Packages added in a newer version of this package appear in the list, unticked.
- Untitled (never saved) scenes are closed when the starter scenes are created; saved ones are reopened.
- The application id only uses letters and digits: `"My Company"`, `"Super Game 2"` →
  `com.mycompany.supergame2`.
- `.gitattributes` keeps Unity's YAML files (scenes, prefabs, materials…) as text with LF endings and marks
  images, models, sounds and fonts as binary, or as LFS files when **Use Git LFS** is on. With LFS, run
  `git lfs install` once on each computer.

## Troubleshooting

| Problem | Fix |
|---|---|
| A package fails to install | Check the Console. Git packages need git on PATH and internet access. |
| `git init` shows "Couldn't run git" | Install git, restart Unity (and Unity Hub) so it sees the new PATH. |
| Input System warnings after installing it | Set **Active Input Handling** to *Both* or *Input System* and restart Unity. |
| The window keeps the old company name | It reads it from Player Settings; change it on the Project tab and click **Apply Project Info**. |
| The window opens in every new project and I don't want that | Settings tab → untick **Show In New Projects**. |

## Tests

27 EditMode tests: names and ids, safe folder paths (no `..`), folders with `.meta` and `.gitkeep`,
running twice, git files (not overwritten unless asked, LFS rules), the package plan (only ticked and
missing, tag vs. newest), project info, color space / serialization / meta files, input handling,
starter scenes and the build list (no duplicates), saving choices and merging new catalog entries.

To run them, add to `Packages/manifest.json`:

```json
"testables": [ "com.atthree.project-setup" ]
```

then **Window > General > Test Runner > EditMode > Run All**.

## License

[MIT](LICENSE.md)

---

<a name="türkçe"></a>
# Türkçe

- **Window > Atthree > Project Setup**: beş sekme var: **Project**, **Folders**, **Packages**, **Settings**,
  **Git & Scenes**.
- **Run Setup (all steps)** hepsini yapar. Her sekmenin kendi butonu da var.
- **Tekrar çalıştırmak güvenli**: var olan klasör, sahne ve git dosyalarına dokunmaz, "atlandı" diye yazar.
- **Seçimlerin hatırlanır** (EditorPrefs). Bir sonraki projende aynı kutular işaretli gelir.
- Kök klasörü olmayan bir projede **bir kez kendiliğinden açılır** (kapatılabilir).
- **Sadece editör**: build'ine hiçbir şey girmez.

**Gereksinim:** Unity 6 (6000.0+). *Git Init* için git kurulu olmalı.

## Adım adım

### 1. Paketi ekle

**Window > Package Manager** → **+** → **Install package from git URL…** →

```
https://github.com/Atthree/ProjectSetup.git
```

Sürüm sabitlemek için: `https://github.com/Atthree/ProjectSetup.git#v0.1.2`

### 2. Pencereyi aç

**Window > Atthree > Project Setup**. Yeni projede kendisi açılır.

### 3. Sekmeleri gez

| Sekme | Ne yapar |
|---|---|
| **Project** | Şirket ve ürün adı. Standalone, Android ve iOS için **Application id** (`com.sirket.oyun`). **Root namespace**: *Create > C# Script* ile açılan scriptler bu namespace ile gelir (varsayılan `Sirket.Oyun`). |
| **Folders** | Klasörlerin `Assets/_Project/` altına gider, Asset Store paketlerinden ayrı durur. Listeyi düzenle (sürükle, + / −). **Add .gitkeep** boş klasörlere boş bir dosya koyar, çünkü git boş klasörleri saklamaz. |
| **Packages** | **Atthree paketleri** (GitHub; etiketli sürüm, ya da *Atthree: Newest Version* ile en yenisi) ve sık kullanılan **Unity paketleri** (Input System, Cinemachine, ProBuilder, AI Navigation, Splines, Timeline…). Yüklü olanlarda **✓ installed** yazar. |
| **Settings** | Linear renk alanı, Force Text serileştirme (git'te okunur farklar), Visible Meta Files, **Active Input Handling** (varsayılan *Both*: eski `Input.GetKey` kodu da Input System de çalışır). |
| **Git & Scenes** | `.gitignore` ve `.gitattributes` yazar (istersen **Git LFS** ile), `git init` çalıştırır. **Extra .gitignore Lines**: yayınlamaman gereken şeyler için, örn. ücretli paketler (`/Assets/Synty/`). Kamera ve ışıklı başlangıç sahneleri (**MainMenu**, **Game**) oluşturur ve build listesine ekler. |

### 4. Çalıştır

**Run Setup (all steps)**'e bas. Alttaki liste ne olduğunu gösterir:
**✓** yapıldı, **–** atlandı (zaten vardı), **!** uyarı.

- Paket kurulumu arka planda ve en son yapılır. Bitince Unity yeniden derler.
- **Active Input Handling** değiştiyse **Restart Unity Now**'a bas (Unity'nin yeniden başlaması gerekir).
- Açık sahnede kaydedilmemiş değişiklik varsa, başlangıç sahnelerinden önce Unity kaydetmeyi sorar.

## Koddan kullanım

Her adım public bir metot. Kendi menünü yazabilir ya da CI'da çalıştırabilirsin. Örnek kod yukarıdaki
İngilizce bölümde.

## Nerede kullanılır

| Durum | Kullanım |
|---|---|
| Her yeni prototip ya da game jam | Bir kez Run Setup: klasör, paket, git ve sahneler bir dakikadan kısa sürede |
| Asset Store paketli projeler | Kök klasör dosyalarını ayırır; **Extra .gitignore Lines** ücretli paketleri herkese açık repodan uzak tutar |
| Ekipler | Her projede aynı klasör düzeni, namespace ve git kuralları; `.gitkeep` sayesinde boş klasörler her bilgisayarda olur |
| Stüdyo şablonu | Kendi menünden `SetupSteps`'i kendi klasör ve paketlerinle çağır |

## Bilmekte fayda var

- **Şirket ve ürün adı her zaman açık projeden okunur**. Diğer her şey projeler arasında hatırlanır.
  **Reset to Defaults** ilk ayarlara döndürür.
- Paketin yeni sürümünde listeye eklenen paketler işaretsiz olarak görünür.
- Başlangıç sahneleri oluşturulurken hiç kaydedilmemiş (Untitled) sahne kapanır, kayıtlı sahneler yeniden açılır.
- Application id sadece harf ve rakam kullanır: `"My Company"`, `"Super Game 2"` → `com.mycompany.supergame2`.
- `.gitattributes`, Unity'nin YAML dosyalarını (sahne, prefab, materyal…) LF satır sonlu metin olarak tutar.
  Resim, model, ses ve fontları binary olarak işaretler, **Use Git LFS** açıksa LFS'e yollar. LFS için her
  bilgisayarda bir kez `git lfs install` çalıştır.

## Sorun giderme

| Sorun | Çözüm |
|---|---|
| Paket kurulmuyor | Console'a bak. Git paketleri için git PATH'te olmalı ve internet olmalı. |
| `git init` "Couldn't run git" diyor | Git'i kur, Unity'yi (ve Unity Hub'ı) yeniden başlat ki yeni PATH'i görsün. |
| Input System kurulunca uyarılar | **Active Input Handling**'i *Both* veya *Input System* yap, Unity'yi yeniden başlat. |
| Pencere eski şirket adını gösteriyor | Player Settings'ten okur; Project sekmesinde değiştir, **Apply Project Info**'ya bas. |
| Her yeni projede açılmasın | Settings sekmesi → **Show In New Projects** işaretini kaldır. |

## Testler

27 EditMode testi. Çalıştırmak için `Packages/manifest.json` içine `"testables": [ "com.atthree.project-setup" ]`
ekle, sonra **Window > General > Test Runner > EditMode > Run All**.

## Lisans

[MIT](LICENSE.md)

---

🤖 *This project was developed with the help of AI tools.*  
🤖 *Bu proje yapay zekâ araçlarının yardımıyla geliştirilmiştir.*
