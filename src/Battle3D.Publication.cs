using System;
using System.IO;
using System.Reflection;
using UnityEngine;

public sealed partial class Battle3D
{
    const string ModVersion = "7.6.1";
    const string RepositoryUrl = "https://github.com/BekirXxC/Warlapse-Battle-3D";
    Texture2D publicationLogo;
    bool publicationMenu, publicationAbout;
    float nextPublicationScan;
    const string PublicationLanguageKey = "Battle3D.Language";
    bool publicationEnglish = true;
    string noticeTurkish, noticeEnglish;
    GUIStyle controlHintStyle, noticeStyle;

    string Text(string turkish, string english) { return publicationEnglish ? english : turkish; }

    void LoadPublicationLanguage()
    {
        publicationEnglish = PlayerPrefs.GetString(PublicationLanguageKey, "ENG") != "TR";
    }

    void SetPublicationLanguage(bool english)
    {
        publicationEnglish = english;
        PlayerPrefs.SetString(PublicationLanguageKey, english ? "ENG" : "TR");
        PlayerPrefs.Save();
        if (!string.IsNullOrEmpty(noticeEnglish)) notice = Text(noticeTurkish, noticeEnglish);
    }

    void ShowNotice(string turkish, string english, float seconds)
    {
        noticeTurkish = turkish;
        noticeEnglish = english;
        notice = Text(turkish, english);
        noticeUntil = Time.unscaledTime + seconds;
    }

    void DrawLanguageSelector(Rect rect)
    {
        float gap = 4, width = (rect.width - gap) * .5f;
        var previous = GUI.backgroundColor;
        GUI.backgroundColor = publicationEnglish ? previous : new Color(.35f, .70f, .57f);
        if (GUI.Button(new Rect(rect.x, rect.y, width, rect.height), "TR")) SetPublicationLanguage(false);
        GUI.backgroundColor = publicationEnglish ? new Color(.35f, .70f, .57f) : previous;
        if (GUI.Button(new Rect(rect.x + width + gap, rect.y, width, rect.height), "ENG")) SetPublicationLanguage(true);
        GUI.backgroundColor = previous;
    }

    string PublicationDescription()
    {
        return Text(
            "Mod kurulu ve çevrimdışı savaşlar için hazır.\n\n" +
            "Konuşlandırma bittikten sonra G ile 3D'ye geç. " +
            "F ile asker gözüne gir, WASD ile seçtiğin askeri hareket ettir. " +
            "G / Esc ile 2D haritaya dön.\n\n" +
            "Saldırı ve hasar birlik düzeyinde hesaplanır. " +
            "Güncellemeler GitHub Releases sayfasındadır. " +
            "Workshop sayfası tanıtım ve indirme bağlantısı içindir.",
            "The mod is installed and ready for offline battles.\n\n" +
            "After deployment, press G to enter 3D. " +
            "Press F for a soldier's view and use WASD to move that soldier. " +
            "Press G / Esc to return to the 2D map.\n\n" +
            "Attacks and damage are calculated at unit level. " +
            "Updates are on GitHub Releases. " +
            "The Workshop page provides information and a download link.");
    }

    string ControlHint()
    {
        if (followed == null) return Text(
            "Serbest kamera   |   Fare: bak   WASD: uç   E / Q: yüksel / alçal\nShift: hızlan   Alt: yavaşla   F: asker gözü   Tab: sonraki asker   G / Esc: 2D",
            "Free camera  |  Mouse: look  WASD: fly  E / Q: up / down\nShift: faster  Alt: slower  F: soldier view  Tab: next soldier  G / Esc: 2D");
        return (commanding ? Text(
            "Asker kontrolü   |   WASD: hareket   Sol tık: saldır   Sağ tık: nişan   P: kontrolü bırak",
            "Soldier control  |  WASD: move  Left click: attack  Right click: aim  P: release control") : Text(
            "Asker gözü   |   Fare: bak   Sağ tık: nişan al   P: kendi askerini kontrol et",
            "Soldier view  |  Mouse: look  Right click: aim  P: control your soldier")) + Text(
            "\nF: serbest kamera   Tab: sonraki asker   Shift+Tab: kendi askerin   G / Esc: 2D",
            "\nF: free camera  Tab: next soldier  Shift+Tab: your soldier  G / Esc: 2D");
    }

    void DrawNotice(float top)
    {
        if (Time.unscaledTime >= noticeUntil || string.IsNullOrEmpty(notice)) return;
        if (noticeStyle == null)
        {
            noticeStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 12, alignment = TextAnchor.MiddleLeft };
            noticeStyle.padding = new RectOffset(10, 10, 6, 6);
            noticeStyle.normal.textColor = Color.white;
        }
        float width = Mathf.Min(600, Screen.width - 32);
        float height = Mathf.Max(32, noticeStyle.CalcHeight(new GUIContent(notice), width));
        var rect = new Rect(16, top, width, height);
        DrawPublicationPanel(rect);
        GUI.Label(rect, notice, noticeStyle);
    }

    void DrawControlHint(Rect rect)
    {
        if (controlHintStyle == null)
        {
            controlHintStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            controlHintStyle.padding = new RectOffset(10, 10, 6, 6);
            controlHintStyle.normal.textColor = Color.white;
        }
        DrawPublicationPanel(rect);
        GUI.Label(rect, ControlHint(), controlHintStyle);
    }

    void LoadPublicationLogo()
    {
        try
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Warlapse.Logo"))
            {
                if (stream == null) return;
                var bytes = new byte[stream.Length];
                int offset = 0, count;
                while (offset < bytes.Length && (count = stream.Read(bytes, offset, bytes.Length - offset)) > 0)
                    offset += count;
                publicationLogo = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                var load = typeof(ImageConversion).GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) });
                if (load == null || !true.Equals(load.Invoke(null, new object[] { publicationLogo, bytes, true })))
                { Destroy(publicationLogo); publicationLogo = null; }
            }
        }
        catch (Exception error) { Debug.LogWarning("Battle 3D logo: " + error.Message); }
    }

    void UpdatePublicationMenu()
    {
        if (Time.unscaledTime < nextPublicationScan) return;
        nextPublicationScan = Time.unscaledTime + 1;
        publicationMenu = battleType != null && !FindAnyObjectByType(battleType);
        if (!publicationMenu) publicationAbout = false;
    }

    void DrawPublicationMenu()
    {
        if (!publicationMenu || root) return;
        var oldDepth = GUI.depth;
        GUI.depth = -900;
        var badge = new Rect(Mathf.Max(16, Screen.width - 266), 16, 250, 92);
        DrawPublicationPanel(badge);
        if (publicationLogo)
            GUI.DrawTexture(new Rect(badge.x + 6, badge.y + 5, 52, 52), publicationLogo, ScaleMode.ScaleToFit, true);
        GUI.Label(new Rect(badge.x + 66, badge.y + 7, 180, 24), Text("Battle 3D · Kurulu ", "Battle 3D · Installed ") + ModVersion);
        if (GUI.Button(new Rect(badge.x + 66, badge.y + 31, 172, 23), Text("Mod bilgisi / GitHub", "About mod / GitHub")))
            publicationAbout = !publicationAbout;
        DrawLanguageSelector(new Rect(badge.x + 66, badge.y + 60, 172, 24));
        if (publicationAbout)
        {
            float width = Mathf.Min(490, Screen.width - 32);
            var panel = new Rect(Screen.width - width - 16, 118, width, 294);
            DrawPublicationPanel(panel);
            var title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            var body = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 13 };
            GUI.Label(new Rect(panel.x + 16, panel.y + 16, width - 32, 30), "Warlapse Battle 3D " + ModVersion, title);
            GUI.Label(new Rect(panel.x + 16, panel.y + 53, width - 32, 168), PublicationDescription(), body);
            if (GUI.Button(new Rect(panel.x + 16, panel.y + 237, 210, 32), Text("GitHub · Güncellemeler", "GitHub · Updates")))
                Application.OpenURL(RepositoryUrl + "/releases");
            if (GUI.Button(new Rect(panel.xMax - 106, panel.y + 237, 90, 32), Text("Kapat", "Close")))
                publicationAbout = false;
        }
        GUI.depth = oldDepth;
    }

    void DrawPublicationPanel(Rect rect)
    {
        var previous = GUI.color;
        GUI.color = new Color(.075f, .11f, .095f, 1);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
        GUI.Box(rect, "");
    }

    void OnDestroy()
    {
        if (publicationLogo) Destroy(publicationLogo);
    }
}
