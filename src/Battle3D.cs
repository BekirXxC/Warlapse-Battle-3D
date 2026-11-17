using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;
using UnityEngine.InputSystem;



public sealed partial class Battle3D : MonoBehaviour
{
    const float Offset = 5000;
    const float MapScale = 1.65f;
    Transform landRoot;

    Vector3 DisplayPoint(Vector3 local) { return landRoot.TransformPoint(local); }
    Vector3 MapPoint(Vector3 display) { return landRoot.InverseTransformPoint(display); }
    const int Layer = 31;

    readonly Dictionary<Type, Dictionary<string, MemberInfo>> members = new Dictionary<Type, Dictionary<string, MemberInfo>>();

    readonly Dictionary<Color32, Material> materials = new Dictionary<Color32, Material>();
    Type battleType, unitType, structureType, zoneType;
    Component battle;
    Behaviour input;
    Camera source, view;
    GameObject root;
    RenderTexture frame;
    Mesh cube, octagon;
    Shader shader;
    Vector2 half; float nextScan;
    Material spriteMaterial;
    string notice;
    float noticeUntil;

    static Battle3D instance;

    public static void Install()
    {
        try
        {
            if (instance) return;
            var host = new GameObject("Warlapse Battle 3D");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<Battle3D>();
        }
        catch (Exception error) { Debug.LogException(error); }
    }

    void Awake()
    {
        var game = Assembly.Load("Assembly-CSharp");
        battleType = game.GetType("Warlapse.Battle.BattleBootstrap");
        unitType = game.GetType("Warlapse.Battle.UnitView");
        structureType = game.GetType("Warlapse.Battle.StructureView");
        zoneType = game.GetType("Warlapse.Battle.ZoneView");
        var settings=game.GetType("Warlapse.Core.GameSettings");
        var shake=settings==null ? null : settings.GetProperty("CameraShakeEnabled");
        if(shake!=null && shake.GetGetMethod()!=null)
            cameraShakeEnabled=(Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>),shake.GetGetMethod());
        LoadPublicationLanguage();
        LoadPublicationLogo();
        Debug.Log("Warlapse Battle 3D " + ModVersion + " ready. Press G during an offline battle.");
    }

    object Read(object target, string name)
    {
        if (target == null) return null;
        var type = target.GetType();
        Dictionary<string, MemberInfo> cache;
        if (!members.TryGetValue(type, out cache))
            members[type] = cache = new Dictionary<string, MemberInfo>();
        MemberInfo member;
        if (!cache.TryGetValue(name, out member))
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            member = (MemberInfo)type.GetField(name, flags) ?? type.GetProperty(name, flags);
            cache[name] = member;
        }
        var field = member as FieldInfo;
        var property = member as PropertyInfo;
        return field != null ? field.GetValue(target) : property != null ? property.GetValue(target, null) : null;
    }

    bool Flag(string name) { return true.Equals(Read(battle, name)); }

    bool Available()
    {
        return battle && source && !Flag("NetActive") && !Flag("IsEscOpen") &&
            !Flag("ChatOwnsKeys") && !Flag("_deployPhase") && !Flag("MatchOver") &&
            !Flag("_funSetupOpen") && Read(battle, "World") != null;
    }

    void Update()
    {
        try
        {
            UpdatePublicationMenu();
            var keys = Keyboard.current;
            if(root && keys!=null && keys.f8Key.wasPressedThisFrame)
                advancedRequested=!advancedRequested;
            if (root && (!Available() || (keys != null && keys.escapeKey.wasPressedThisFrame)))
                Close();
            if (keys == null || !keys.gKey.wasPressedThisFrame) return;
            if (root) { ToggleExit(); return; }
            battle = battleType == null ? null : FindAnyObjectByType(battleType) as Component;
            source = Camera.main;
            if (!Available())
            {
                ShowNotice("3D: Çevrimdışı savaşı başlatıp konuşlandırmayı tamamlayın.",
                    "3D: Start an offline battle and finish deployment first.", 3);
                return;
            }
            Open();
        }
        catch (Exception error) { Fail(error); }
    }

    void Open()
    {
        var controller = Read(battle, "_input") as Behaviour;
        if (!controller || !controller.isActiveAndEnabled)
            throw new InvalidOperationException("Battle input unavailable.");
        shader = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Sprites/Default");
        if (!shader) throw new InvalidOperationException("Sprite shader unavailable.");

        spriteMaterial = new Material(Shader.Find("Sprites/Default"));
        root = new GameObject("Warlapse 3D Preview");
        root.transform.position = new Vector3(0, 0, Offset);
        landRoot = Joint(root.transform,"Geniş savaş alanı",Vector3.zero);
        landRoot.localScale = new Vector3(MapScale,1,MapScale);
        var cameraObject = new GameObject("3D Camera");
        cameraObject.transform.SetParent(root.transform, false);
        view = cameraObject.AddComponent<Camera>();
        view.enabled = false;
        view.clearFlags = CameraClearFlags.SolidColor;
        view.backgroundColor = new Color(.18f, .22f, .26f);
        view.cullingMask = 1 << Layer;
        view.nearClipPlane = .2f;
        view.farClipPlane = 3400;
        var width = Mathf.Clamp(Screen.width, 320, 1920);
        var height = Mathf.Clamp(Mathf.RoundToInt(width * (float)Screen.height / Mathf.Max(1, Screen.width)), 180, 1200);
        // The 3D scene must remain opaque even when a transparent surface writes alpha.
        var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float)
            ? RenderTextureFormat.RGB111110Float : RenderTextureFormat.Default;
        frame = new RenderTexture(width, height, 24, format);
        view.targetTexture = frame;
        ConfigureAtmosphere();

        var sample = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sample.SetActive(false);
        cube = Instantiate(sample.GetComponent<MeshFilter>().sharedMesh);
        var normals = cube.normals;
        var shades = new Color[normals.Length];
        for (int i = 0; i < normals.Length; i++)
            shades[i] = Color.white * (1-sunContrast + Mathf.Max(0, Vector3.Dot(normals[i], sunDirection)) * sunContrast);
        for (int i = 0; i < shades.Length; i++) shades[i].a = 1;
        cube.colors = shades;
        Destroy(sample);

        var world = Read(battle, "World");
        half = (Vector2)Read(world, "MapHalf");
        BuildTerrain(world);
        var cancel = controller.GetType().GetMethod("CancelGesture", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, Type.EmptyTypes, null);
        if (cancel != null) cancel.Invoke(controller, null);
        input = controller;
        input.enabled = false;
        nextScan = 0;
        var fxType = battleType.Assembly.GetType("Warlapse.Battle.BattleFxManager");
        fxManager = fxType == null ? null : FindAnyObjectByType(fxType) as Component;
        pitch = 54;
        yaw = 25;
        var focus = source.transform.position;
        var target = DisplayPoint(new Vector3(focus.x,0,focus.y));
        flyVelocity = Vector3.zero;
        transitionFocus = target;
        transitionStart = Time.unscaledTime;
        exitStart = -1;
        SetupRain();
        SetupCombatEffects(Read(battle,"_scar") as Component);
        BuildSky();
        SetupStorm();
        SetupHaze();
        SetupFortFlags();
        SetupTorches();
        BuildContactShadows();
        flyPosition = target - Quaternion.Euler(pitch, yaw, 0) * Vector3.forward * Mathf.Max(8, source.orthographicSize * 1.8f * Mathf.Sqrt(MapScale));
        oldCursorLock = Cursor.lockState;
        oldCursorVisible = Cursor.visible;
        ownsCursor = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Debug.Log("3D opened.");
    }

    Material Paint(Color color)
    {
        color.a = 1;
        Color32 key = color;
        Material material;
        if (!materials.TryGetValue(key, out material))
        {
            material = new Material(shader);
            material.color = color;
            material.SetInt("_ZWrite", 1);
            material.SetInt("_SrcBlend", 1);
            material.SetInt("_DstBlend", 0);
            material.SetInt("_Cull", 2);
            material.renderQueue = 2000;
            materials.Add(key, material);
        }
        return material;
    }

    Transform Block(string label, Transform parent, Color color)
    {
        var item = new GameObject(label);
        item.layer = Layer;
        item.transform.SetParent(parent, false);
        item.AddComponent<MeshFilter>().sharedMesh = cube;
        item.AddComponent<MeshRenderer>().sharedMaterial = Paint(color);
        return item.transform;
    }

    void BuildTerrain(object world)
    {
        var terrain=Read(world,"Terrain");
        gridX = Mathf.Clamp(Mathf.CeilToInt(half.x * 3), 24, 480);
        gridZ = Mathf.Clamp(Mathf.CeilToInt(half.y * 3), 24, 480);
        int width = gridX + 1;
        float dx = half.x * 2 / gridX;
        float dz = half.y * 2 / gridZ;
        var colors = new Color[width * (gridZ + 1)];
        heights = new float[colors.Length];
        var waterCoverage = new float[colors.Length];
        var bareCoverage = new float[colors.Length];
        var rockyCoverage = new float[colors.Length];
        for (int i = 0; i < colors.Length; i++) colors[i] = new Color(.31f,.42f,.25f);
        var regions = Read(terrain, "Regions") as IEnumerable;
        if (regions != null)
        foreach (var region in regions)
        {
            var area = Read(region, "Area");
            var data = Read(region, "Data");
            if (!(area is Rect) || data == null) continue;
            var rect = (Rect)area;
            var tint = Read(data, "debugColor");
            var color = tint is Color ? (Color)tint : new Color(.43f,.42f,.32f);
            var id = Convert.ToString(Read(data, "id")).ToLowerInvariant();
            bool water = id.Contains("water") || id.Contains("river") || id.Contains("lake") || id.Contains("sea");
            bool hill = id.Contains("hill") || id.Contains("mountain") || id.Contains("high_ground");
            bool bare = id.Contains("dirt") || id.Contains("road") || id.Contains("bridge") ||
                id.Contains("pave") || id.Contains("sand") || id.Contains("snow") || id.Contains("ash");
            if (water) color = Color.Lerp(color, new Color(.19f,.43f,.55f), .50f);
            var lowX = Mathf.Clamp(Mathf.FloorToInt((rect.xMin + half.x) / dx), 0, gridX);
            var lowZ = Mathf.Clamp(Mathf.FloorToInt((rect.yMin + half.y) / dz), 0, gridZ);
            var highX = Mathf.Clamp(Mathf.CeilToInt((rect.xMax + half.x) / dx), 0, gridX);
            var highZ = Mathf.Clamp(Mathf.CeilToInt((rect.yMax + half.y) / dz), 0, gridZ);
            float span = Mathf.Max(.1f, Mathf.Min(rect.width, rect.height));
            for (int z = lowZ; z <= highZ; z++)
            for (int x = lowX; x <= highX; x++)
            {
                float wx = -half.x + x * dx, wz = -half.y + z * dz;
                float edge = Mathf.Min(Mathf.Min(rect.xMin<=-half.x+.01f ? span : wx-rect.xMin,
                    rect.xMax>=half.x-.01f ? span : rect.xMax-wx),
                    Mathf.Min(rect.yMin<=-half.y+.01f ? span : wz-rect.yMin,
                    rect.yMax>=half.y-.01f ? span : rect.yMax-wz));
                if (edge < -Mathf.Min(dx,dz) * .45f) continue;
                float blend = Mathf.SmoothStep(0,1,Mathf.Clamp01((edge + .35f) / .85f));
                int i = z * width + x;
                colors[i] = Color.Lerp(colors[i], color, blend);
                waterCoverage[i] = Mathf.Lerp(waterCoverage[i], water ? 1 : 0, blend);
                bareCoverage[i] = Mathf.Lerp(bareCoverage[i], bare ? 1 : 0, blend);
                rockyCoverage[i] = Mathf.Lerp(rockyCoverage[i], hill ? 1 : 0, blend);
                if (water) heights[i] = Mathf.Lerp(heights[i], -.22f, blend);
                else if (hill)
                {
                    float slope = Mathf.SmoothStep(0,1,Mathf.Clamp01(edge / (span * .37f)));
                    heights[i] = Mathf.Max(heights[i], slope * Mathf.Clamp(span * .28f,.55f,6));
                }
            }
        }

        BlendHighGround(world,colors,rockyCoverage,waterCoverage,bareCoverage,dx,dz);
        var vertices = new Vector3[colors.Length];
        var triangles = new int[gridX * gridZ * 6];
        int next = 0;
        for (int z = 0; z <= gridZ; z++)
        for (int x = 0; x <= gridX; x++)
        {
            int i = z * width + x;
            vertices[i] = new Vector3(-half.x + x * dx, heights[i], -half.y + z * dz);
            float riseX = heights[z * width + Mathf.Min(gridX,x+1)] - heights[z * width + Mathf.Max(0,x-1)];
            float riseZ = heights[Mathf.Min(gridZ,z+1) * width + x] - heights[Mathf.Max(0,z-1) * width + x];
            var normal = new Vector3(-riseX / (2*dx), 1, -riseZ / (2*dz)).normalized;
            float worldX=vertices[i].x, worldZ=vertices[i].z;
            float grain = .58f*Mathf.PerlinNoise(worldX*.40f+4,worldZ*.40f+4)
                + .42f*Mathf.PerlinNoise(worldX*.12f+15,worldZ*.13f+24);
            if (waterCoverage[i]>.08f && waterCoverage[i]<.63f)
                colors[i]=Color.Lerp(colors[i],new Color(.66f,.59f,.44f),Mathf.Sin(waterCoverage[i]*Mathf.PI)*.35f);
            colors[i] *= .91f - sunContrast*.55f + sunContrast*.60f * Mathf.Max(0, Vector3.Dot(normal,sunDirection)) + .19f * grain;
            colors[i].a = 1;
            if (x == gridX || z == gridZ) continue;
            triangles[next++] = i;
            triangles[next++] = i + width;
            triangles[next++] = i + 1;
            triangles[next++] = i + 1;
            triangles[next++] = i + width;
            triangles[next++] = i + width + 1;
        }
        terrainMesh = new Mesh { name = "Savaş arazisi", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        terrainMesh.vertices = vertices;
        terrainMesh.colors = colors;
        terrainMesh.triangles = triangles;
        terrainMesh.RecalculateBounds();
        var surface = new GameObject("Birleşik arazi");
        surface.layer = Layer;
        surface.transform.SetParent(landRoot, false);
        surface.AddComponent<MeshFilter>().sharedMesh = terrainMesh;
        terrainMaterial = BuildTerrainFinish(vertices,colors,waterCoverage);
        int weather=Convert.ToInt32(Read(Read(battle,"_level"),"weather"));
        terrainBaseTint=weather>=3 && weather<=5 ? new Color(.79f,.83f,.86f) : Color.white;
        terrainMaterial.color=terrainBaseTint;
        surface.AddComponent<MeshRenderer>().sharedMaterial = terrainMaterial;
        BuildGroundDetail(world,colors,waterCoverage,bareCoverage,rockyCoverage);
        PrepareWetGround(world,waterCoverage,bareCoverage);
        Vector3[] rimVertices;
        int[] rimFaces;
        float[] rimWater;
        BuildLandscapeRim(vertices,colors,waterCoverage,out rimVertices,out rimFaces,out rimWater);
        BuildWater(vertices,triangles,waterCoverage,rimVertices,rimFaces,rimWater);
        BuildBridgeDecks(terrain);
    }

    void BuildBridgeDecks(object terrain)
    {
        var regions=Read(terrain,"Regions") as IEnumerable;
        if(regions==null)return;
        var group=Joint(landRoot,"Köprü döşemesi",Vector3.zero);
        var used=new HashSet<Vector2Int>();
        foreach(var region in regions)
        {
            var area=Read(region,"Area");
            if(!(area is Rect))continue;
            string id=Convert.ToString(Read(Read(region,"Data"),"id")).ToLowerInvariant();
            if(!id.Contains("bridge"))continue;
            var bounds=(Rect)area;
            var p=bounds.center;
            var cell=new Vector2Int(Mathf.RoundToInt(p.x*12),Mathf.RoundToInt(p.y*12));
            if(!used.Add(cell))continue;
            var timber=id.Contains("stone") ? new Color(.55f,.51f,.46f) : new Color(.42f,.31f,.21f);
            bool alongZ=bounds.height>bounds.width;
            var tile=Joint(group,"Köprü parçası",new Vector3(p.x,GroundHeight(p.x,p.y)+.095f,p.y));
            tile.localRotation=Quaternion.Euler(0,alongZ ? 0 : 90,0);
            float width=alongZ ? bounds.width : bounds.height;
            float length=alongZ ? bounds.height : bounds.width;
            int boards=Mathf.Clamp(Mathf.CeilToInt(length/.24f),2,12);
            for(int i=0;i<boards;i++)
                Part(tile,"Köprü tahtası",timber*(.81f+Noise01(i+cell.x*43+cell.y*7)*.29f),
                    new Vector3(0,0,(i+.5f)*length/boards-length*.5f),new Vector3(width,.097f,length/boards*.965f));
        }
        if(used.Count>0) BakeStaticParts(new Model{Root=group},"terrain|bridge");
        else Destroy(group.gameObject);
    }

    sealed class Model
    {
        public Transform Root;
        public Transform[] Legs, Knees, Arms;
        public Transform Body;
        public List<Person> People;
        public float MaxManpower;
        public object Unit;
        public Kind Kind;
        public bool Fort, Timber, Gate, Keep, Relief, Settlement;
        public string VisualId;
        public Fieldwork Field;
        public Color FortTint;

        public Transform Eye;
    }

    sealed class SpriteCopy
    {
        public SpriteRenderer View;
        public bool Ground, Tree;
        public float Lift;
        public Transform Prop;
    }

    readonly Dictionary<SpriteRenderer, SpriteCopy> sprites = new Dictionary<SpriteRenderer, SpriteCopy>();
    readonly Dictionary<Component, Model> models = new Dictionary<Component, Model>();
    readonly Dictionary<string, Mesh> infantryMeshes = new Dictionary<string, Mesh>();
    Mesh terrainMesh;
    Material terrainMaterial;
    Color terrainBaseTint;
    float[] heights;
    int gridX, gridZ;

    Component fxManager;
    Vector3 flyPosition, flyVelocity;
    float pitch, yaw;
    float followBlendStart = -1;
    Vector3 followBlendPosition;
    Quaternion followBlendRotation;
    bool ownsCursor, oldCursorVisible;
    CursorLockMode oldCursorLock;

    float GroundHeight(float x, float z)
    {
        if (heights == null) return 0;
        float gx = Mathf.Clamp01((x + half.x) / (2 * half.x)) * gridX;
        float gz = Mathf.Clamp01((z + half.y) / (2 * half.y)) * gridZ;
        int ix = Mathf.Min((int)gx, gridX - 1), iz = Mathf.Min((int)gz, gridZ - 1);
        float u = gx - ix, v = gz - iz;
        int width = gridX + 1, i = iz * width + ix;
        float a = heights[i], b = heights[i + 1], c = heights[i + width], d = heights[i + width + 1];
        return u + v <= 1 ? a + (b-a)*u + (c-a)*v : d + (c-d)*(1-u) + (b-d)*(1-v);
    }
    Transform Part(Transform parent, string name, Color color, Vector3 pos, Vector3 scale)
    {
        var part = Block(name, parent, color);
        part.localPosition = pos;
        part.localScale = scale;
        return part;
    }

    Transform Joint(Transform parent, string name, Vector3 pos)
    {
        var joint = new GameObject(name).transform;
        joint.SetParent(parent, false);
        joint.localPosition = pos;
        return joint;
    }

    enum Kind { Infantry, Horse, Elephant, Wolf, Beast, Armor, Artillery, Aircraft }

    static Kind UnitKind(string id, bool air)
    {
        if (air) return Kind.Aircraft;
        if (id.Contains("cavalry") || id.Contains("lightcav") || id.Contains("knight") || id.Contains("chariot") ||
            id.Contains("hussar") || id.Contains("dragoon") || id.Contains("mounted") || id.Contains("horse"))
            return Kind.Horse;
        if (id.Contains("elephant") || id.Contains("mammoth")) return Kind.Elephant;
        if (id.Contains("wolfrider")) return Kind.Wolf;
        if (id.Contains("wardog")||id.Contains("sandworm")) return Kind.Beast;
        if (id.Contains("tank") || id.Contains("armoredcar") || id.Contains("mechanized") ||
            id.StartsWith("ifv_") || id.Contains("truck") || id.StartsWith("mlrs_") ||
            id.StartsWith("shorad_") || id.Contains("mech") || id.Contains("walker") ||
            id.Contains("moped") || id.Contains("bike") || id.Contains("forklift") || id.Contains("watercannon")) return Kind.Armor;
        if (id.Contains("catapult") || id.Contains("trebuchet") || id.Contains("ballista") ||
            id.Contains("bombard") || id.Contains("cannon") || id.Contains("fieldgun") ||
            id.Contains("howitzer") || id.Contains("artillery") || id.StartsWith("atgun_") ||
            id.StartsWith("flak_") || id.StartsWith("plasmaarty_") || id.StartsWith("rocket_")) return Kind.Artillery;
        return Kind.Infantry;
    }

    void Mount(Model model, Color uniform, string role, Outfit outfit, Kind kind, int variation)
    {
        var coat = Color.Lerp(new Color(.27f,.17f,.11f), new Color(.59f,.39f,.23f), variation * .25f);
        if (kind == Kind.Elephant) coat = new Color(.53f,.50f,.49f);
        if (kind == Kind.Wolf || kind == Kind.Beast) coat = new Color(.44f,.43f,.39f);
        var dark = coat * .57f;
        var leather = new Color(.26f,.17f,.10f);
        var body = Joint(model.Root, "Binek", new Vector3(0, 1.05f, 0));
        model.Body = body;
        bool elephant=kind==Kind.Elephant, wolf=kind==Kind.Wolf || kind==Kind.Beast;
        Shape(body,"Gövde","round",coat,Vector3.zero,new Vector3(elephant ? .84f : .58f,.65f,1.53f));
        Shape(body,"Sağrı","round",coat,new Vector3(0,-.015f,-.46f),new Vector3(.65f,.66f,.65f));
        Shape(body,"Göğüs","round",coat,new Vector3(0,.018f,.47f),new Vector3(.58f,.65f,.57f));
        float neck=elephant ? .22f : wolf ? .24f : .43f;
        Shape(body,"Boyun","torso",coat,new Vector3(0,neck*.63f,.62f),new Vector3(.37f,.28f+neck,.47f))
            .localRotation=Quaternion.Euler(26,0,0);
        if(!elephant)
            Shape(body,wolf ? "Ense" : "Yele","round",dark,new Vector3(0,neck*.84f,.47f),
                new Vector3(wolf ? .37f : .16f,.18f+neck,.18f)).localRotation=Quaternion.Euler(24,0,0);
        Shape(body,"Baş","head",coat,new Vector3(0,neck+.096f,.91f),
            new Vector3(elephant ? .59f : .32f,elephant ? .54f : .31f,.51f)).localRotation=Quaternion.Euler(16,0,0);
        if(!elephant)
            Shape(body,"Burun","round",dark,new Vector3(0,neck+.008f,1.15f),new Vector3(.25f,.24f,.27f));
        for(int side=-1;side<=1;side+=2)
        {
            if(elephant)
            {
                Shape(body,"Fil kulağı","shield",coat*.85f,new Vector3(side*.29f,.28f,.80f),new Vector3(.37f,.54f,.14f))
                    .localRotation=Quaternion.Euler(0,side*-24,side*18);
                Shape(body,"Fildişi","cone",new Color(.88f,.80f,.62f),new Vector3(side*.18f,.15f,1.21f),new Vector3(.086f,.49f,.086f))
                    .localRotation=Quaternion.Euler(60,0,side*-9);
            }
            else
                Shape(body,"Kulak","cone",coat,new Vector3(side*.105f,neck+.29f,.79f),
                    new Vector3(.125f,wolf ? .22f : .17f,.13f));
            Part(body,"Göz",new Color(.105f,.075f,.055f),new Vector3(side*(elephant ? .237f : .145f),neck+.148f,1.015f),
                new Vector3(.032f,.054f,.057f));
            if(!elephant)
            {
                Part(body,"Dizgin",leather,new Vector3(side*.136f,neck+.02f,1.09f),new Vector3(.035f,.047f,.27f));
                Shape(body,"Burun deliği","round",dark*.61f,new Vector3(side*.089f,neck+.05f,1.257f),new Vector3(.048f,.034f,.026f));
                var reins=Part(body,"Dizgin kayışı",leather*.75f,new Vector3(side*.165f,neck+.07f,.65f),new Vector3(.027f,.030f,.70f));
                reins.localRotation=Quaternion.Euler(7,side*3,0);
            }
        }
        if(elephant)
        {
            for(int i=0;i<4;i++)
                Shape(body,"Hortum","round",coat,new Vector3(0,.237f-i*.126f,1.16f+i*i*.012f),
                    new Vector3(.22f-i*.025f,.245f,.26f-i*.032f)).localRotation=Quaternion.Euler(-12-i*13,0,0);
        }
        Shape(body,"Kuyruk","taper",dark,new Vector3(0,-.15f,-.86f),new Vector3(.15f,.62f,.17f))
            .localRotation=Quaternion.Euler(wolf ? 66 : 34,0,0);
        if(kind!=Kind.Beast)
        {
        Part(body,"Eyer örtüsü",uniform,new Vector3(0,.27f,-.045f),new Vector3(.66f,.18f,.67f));
        for(int side=-1;side<=1;side+=2)
        {
            Part(body,"Eyer kayışı",leather,new Vector3(side*.32f,.12f,-.05f),new Vector3(.053f,.33f,.078f));
            Part(body,"Eyer çantası",leather*.89f,new Vector3(side*.32f,.21f,-.325f),new Vector3(.155f,.205f,.27f));
        }
        Shape(body,"Eyer","torso",leather,new Vector3(0,.37f,-.055f),new Vector3(.48f,.137f,.53f));
        }

        model.Legs = new Transform[4];
        model.Knees = new Transform[4];
        for (int i = 0; i < 4; i++)
        {
            float side = i % 2 == 0 ? -1 : 1;
            float front = i < 2 ? .50f : -.54f;
            var leg = Joint(model.Root, "Binek kalçası", new Vector3(side*.23f,.94f,front));
            var knee = Joint(leg, "Binek dizi", new Vector3(0,-.43f,0));
            Shape(leg, "Üst bacak", "torso", coat, new Vector3(0,-.20f,0), new Vector3(elephant ? .26f : .20f,.46f,.25f));
            Shape(knee, "Alt bacak", "taper", coat, new Vector3(0,-.22f,0), new Vector3(elephant ? .24f : .14f,.47f,.17f));
            Shape(knee, wolf || elephant ? "Ayak" : "Toynak", "hoof", dark, new Vector3(0,-.45f,.035f), new Vector3(elephant ? .27f : .18f,.15f,.22f));
            model.Legs[i] = leg;
            model.Knees[i] = knee;
        }

        if(kind==Kind.Beast)
        {
            model.Eye=Joint(body,"Hayvan gözü",new Vector3(0,.41f,1.30f));
            return;
        }
        var rider = Joint(body, "Binici", new Vector3(0,.02f,-.10f));
        rider.localScale = Vector3.one * .72f;
        Transform[] legs, knees, arms;
        Human(rider, uniform, role, outfit, variation, out legs, out knees, out arms);
        for (int i = 0; i < 2; i++)
        {
            legs[i].localPosition += new Vector3(i == 0 ? -.25f : .25f,0,0);
            legs[i].localRotation = Quaternion.Euler(-54,0,i == 0 ? -15 : 15);
            knees[i].localRotation = Quaternion.Euler(72,0,0);
            arms[i].localRotation = Quaternion.Euler(-24,0,0);
        }
        model.Eye = Joint(rider, "Binici gözü", new Vector3(0,1.42f,.18f));
    }

    Model CreateModel(Component actor, bool building)
    {
        var model = new Model { Root = Joint(landRoot, building ? "Yapı" : "Birlik", Vector3.zero) };
        var sprite = Read(actor, "_sr") as SpriteRenderer;
        var baseColor = Read(actor, "_baseColor");
        var color = baseColor is Color ? (Color)baseColor : sprite ? sprite.color : Color.gray;
        if (color.maxColorComponent < .08f) color = Color.gray;
        if (building)
        {
            var state = Read(actor, "_s");
            var info = Read(state, "Data");
            var id = Convert.ToString(Read(info, "id")).ToLowerInvariant();
            model.VisualId=id;
            model.Relief=id=="high_ground";
            if(model.Relief)return model;
            model.Field=FieldworkFor(id);
            if(model.Field!=Fieldwork.None)
            {
                MakeFieldwork(model.Root,model.Field,color);
                if(sprite)
                    model.Root.localRotation=Quaternion.Euler(0,-sprite.transform.eulerAngles.z,0);
            }
            else if (Fortification(id)) MakeFort(model,id,color);
            else MakeBuilding(model, id, color);
            BakeStaticParts(model, "structure|" + id + "|" + (Color32)color);
            return model;
        }

        var unit = Read(actor, "_unit");
        model.Unit=unit;
        var data = Read(unit, "Data");
        var role = Convert.ToString(Read(data, "role"));
        var era = Convert.ToInt32(Read(data, "eraIndex"));
        var air = true.Equals(Read(actor, "_isAir"));
        var idKey = Convert.ToString(Read(data, "id")).ToLowerInvariant();
        model.Kind = UnitKind(idKey, air);
        if ((model.Kind == Kind.Artillery || model.Kind == Kind.Armor || model.Kind == Kind.Aircraft) &&
            !PlaceVehicle(model.Root,idKey,color))
        {
            MakeMachine(model,idKey,color,era);
            BakeStaticParts(model,"vehicle|"+idKey+"|"+(Color32)color);
        }
        if (model.Kind != Kind.Armor && model.Kind != Kind.Aircraft)
            CreatePeople(model,data,Read(actor,"_squad"),color,role,era,idKey,actor.GetInstanceID());
        return model;
    }

    void BakeStaticParts(Model model, string key)
    {
        var pieces = model.Root.GetComponentsInChildren<MeshRenderer>(true);
        AttachMesh(model.Root,BakeParts(model.Root,pieces,key));
        foreach(var part in pieces)
        {
            part.enabled=false;
            Destroy(part.gameObject);
        }
    }

    Mesh BakeParts(Transform parent, IEnumerable<MeshRenderer> pieces, string key)
    {
        Mesh mesh;
        if (infantryMeshes.TryGetValue(key,out mesh)) return mesh;
        var vertices=new List<Vector3>();
        var indices=new List<int>();
        var colors=new List<Color32>();
        var transform=parent.worldToLocalMatrix;
        var shapes=new Dictionary<Mesh,Geometry>();
        foreach(var part in pieces)
        {
            var shape=part.GetComponent<MeshFilter>().sharedMesh;
            Geometry g;
            if (!shapes.TryGetValue(shape,out g))
                shapes[shape]=g=new Geometry { Vertices=shape.vertices, Indices=shape.triangles, Colors=shape.colors };
            var matrix=transform*part.transform.localToWorldMatrix;
            int start=vertices.Count;
            var tint=part.sharedMaterial.color;
            foreach(int index in g.Indices) indices.Add(start+index);
            for(int i=0;i<g.Vertices.Length;i++)
            {
                vertices.Add(matrix.MultiplyPoint3x4(g.Vertices[i]));
                colors.Add((Color32)(g.Colors.Length>i ? g.Colors[i]*tint : tint));
            }
        }
        mesh=new Mesh { name=key, indexFormat=vertices.Count>65535 ?
            UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(indices,0);
        infantryMeshes.Add(key,mesh);
        return mesh;
    }

    sealed class Geometry { public Vector3[] Vertices; public int[] Indices; public Color[] Colors; }

    MeshFilter AttachMesh(Transform parent, Mesh mesh)
    {
        var item=Joint(parent,"Birleşik geometri",Vector3.zero).gameObject;
        item.layer=Layer;
        var filter=item.AddComponent<MeshFilter>();
        filter.sharedMesh=mesh;
        item.AddComponent<MeshRenderer>().sharedMaterial=Paint(Color.white);
        return filter;
    }

    void Scan(Type type, bool building)
    {
        if (type == null) return;
        foreach (var item in FindObjectsByType(type, FindObjectsSortMode.None))
        {
            var actor = item as Component;
            if (!actor || actor.gameObject.scene != battle.gameObject.scene) continue;
            Model existing;
            if (models.TryGetValue(actor,out existing))
            {
                if(building || ReferenceEquals(existing.Unit,Read(actor,"_unit"))) continue;
                existing.Root.gameObject.SetActive(false);
                Destroy(existing.Root.gameObject);
                models.Remove(actor);
            }
            if (!building && Read(actor,"_unit")==null) continue;
            models.Add(actor, CreateModel(actor, building));
        }
    }

    void ScanSprites()
    {
        foreach (var item in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            TrackSprite(item, false);
    }

    void TrackSprite(SpriteRenderer item, bool effect)
    {
        if (!item || item.gameObject.scene != battle.gameObject.scene ||
            item.transform.IsChildOf(root.transform) || sprites.ContainsKey(item)) return;
        var name = item.name.ToLowerInvariant();
        if (hazeVolume && (item.sortingOrder == 40 || name.Contains("fog"))) return;
        if (rainMesh && (item.sortingOrder >= 44 && item.sortingOrder <= 49 || name.Contains("rain") || name.Contains("splash"))) return;
        if (name.StartsWith("terrain_") || name.StartsWith("dirtwash") || name.StartsWith("dirtblotch")) return;
        if (name.StartsWith("blood") || name.StartsWith("corpse") || name.StartsWith("minicorpse")) return;
        if (name.Contains("fog") || name.Contains("influence") || name.Contains("selection") ||
            name.Contains("morale") || name.Contains("manpower") || name.Contains("hpbar")) return;
        var unit = item.GetComponentInParent(unitType);
        var building = item.GetComponentInParent(structureType);
        bool fx = effect || name.Contains("fire") || name.Contains("smoke") || name.Contains("shadow") ||
            name.Contains("flag") || name.Contains("rout") || name.Contains("skull");
        if ((unit || building) && !fx) return;
        var copy = new GameObject("Görsel: " + item.name);
        object entry = null;
        int index;
        if (name.StartsWith("decor_") && int.TryParse(name.Substring(6), out index))
        {
            var decor = Read(Read(battle, "_level"), "decor") as IList;
            if (decor != null && index >= 0 && index < decor.Count) entry = decor[index];
        }
        var propId = Convert.ToString(Read(entry, "sprite")).ToLowerInvariant();
        var prop = BuildProp(propId, copy.transform);
        var zone = zoneType == null ? null : item.GetComponentInParent(zoneType);
        bool zoneGround = zone && (item == Read(zone, "_marker") as SpriteRenderer || item == Read(zone, "_frame") as SpriteRenderer);
        copy.layer = Layer;
        copy.transform.SetParent(landRoot, false);
        var renderer = copy.AddComponent<SpriteRenderer>();
        renderer.sharedMaterial = spriteMaterial;
        sprites.Add(item, new SpriteCopy { View = renderer, Prop = prop, Tree = IsTreeProp(propId),
            Ground = !effect && (name.StartsWith("terrain_") || name.StartsWith("dirt") ||
                (name.Contains("zone") && !name.Contains("flag")) || name.Contains("shadow") ||
                name.Contains("scar") || name.Contains("rubble") || name.Contains("road") || name.Contains("ring") || zoneGround),
            Lift = effect ? .45f : 0 });
    }

    void SyncEffects(string listName, bool projectile)
    {
        var effects = Read(fxManager, listName) as IEnumerable;
        if (effects == null) return;
        foreach (var effect in effects)
        {
            var sprite = Read(effect, "Sr") as SpriteRenderer;
            if (!sprite) continue;
            TrackSprite(sprite, true);
            SpriteCopy copy;
            if (!sprites.TryGetValue(sprite, out copy)) continue;
            copy.Ground = false;
            copy.Lift = .35f;
            if (!projectile) continue;
            float life = Mathf.Clamp01(Convert.ToSingle(Read(effect, "T")) /
                Mathf.Max(.01f, Convert.ToSingle(Read(effect, "Dur"))));
            copy.Lift += Mathf.Sin(life * Mathf.PI) * 1.1f;
        }
    }
    void SyncSprite(SpriteRenderer item, SpriteCopy copy)
    {
        if (!item) { copy.View.enabled = false; return; }
        if (copy.Prop)
        {
            copy.View.enabled = false;
            copy.Prop.gameObject.SetActive(item.gameObject.activeInHierarchy && item.enabled && item.color.a > .02f);
            var center = item.transform.position;
            var size = item.bounds.size;
            copy.View.transform.localPosition = new Vector3(center.x, GroundHeight(center.x, center.y) + .10f, center.y);
            float propScale = Mathf.Clamp(Mathf.Max(size.x, size.y), .25f, 20);
            copy.View.transform.localScale = new Vector3(propScale/Mathf.Sqrt(MapScale),propScale,propScale/Mathf.Sqrt(MapScale));
            copy.View.transform.localRotation = Quaternion.Euler(0,-item.transform.eulerAngles.z,0);
            return;
        }
        var image = copy.View;
        image.enabled = item.enabled && item.gameObject.activeInHierarchy && item.sprite && item.color.a > .02f;
        if (!image.enabled) return;
        image.sprite = item.sprite;
        image.color = item.color;
        image.flipX = item.flipX;
        image.flipY = item.flipY;
        image.drawMode = item.drawMode;
        if (image.drawMode != SpriteDrawMode.Simple) image.size = item.size;
        var point = item.transform.position;
        var scale = item.transform.lossyScale;
        var ground = GroundHeight(point.x, point.y);
        image.sortingOrder = copy.Ground ? -2000 + item.sortingOrder : item.sortingOrder;
        image.transform.localScale = new Vector3(Mathf.Abs(scale.x)/(copy.Ground ? 1 : MapScale), Mathf.Abs(scale.y), 1);
        if (copy.Ground)
        {
            var h = Mathf.Clamp(item.sortingOrder, -100, 100) * .002f + .24f;
            image.transform.localPosition = new Vector3(point.x, ground + h, point.y);
            image.transform.localRotation = Quaternion.Euler(90, -item.transform.eulerAngles.z, 0);
        }
        else
        {
            float lift = Mathf.Clamp(item.bounds.extents.y * .82f, .05f, 10);
            image.transform.localPosition = new Vector3(point.x, ground + lift + copy.Lift, point.y);
            image.transform.rotation = Quaternion.Euler(0, yaw, 0);
        }
    }

    void SelectSoldier(bool cycle,bool ownSide=false)
    {
        if (soldiers.Count == 0)
        {
            ShowNotice("İzlenebilecek canlı asker yok.", "No living soldiers are available to follow.", 2);
            return;
        }
        if (cycle && followed != null && !ownSide)
        {
            int index = soldiers.IndexOf(followed);
            SetFollowed(soldiers[(index + 1) % soldiers.Count]);
            return;
        }
        float best = float.PositiveInfinity;
        Person selected = null;
        foreach (var person in soldiers)
        {
            if(ownSide && !MayCommand(person))continue;
            var delta = person.Rig.Eye.position - view.transform.position;
            float score = Vector3.Angle(view.transform.forward,delta)*1.7f+delta.magnitude*.20f;
            if (score < best) { best=score; selected=person; }
        }
        if (selected != null) SetFollowed(selected);
        else if(ownSide)
        {
            ShowNotice("Komuta edebileceğin görünür bir asker yok.", "No visible soldiers are available for you to control.", 2);
        }
    }

    void SetFollowed(Person soldier)
    {
        StopSteering();
        flyVelocity=Vector3.zero;
        BeginFollowBlend();
        followed=soldier;
        StartSoldierControl(soldier);
        eyeReady=false;
        heldSway=Vector2.zero;
        yaw=soldier.Rig.transform.eulerAngles.y;
        pitch=2;
    }

    void BeginFollowBlend()
    {
        followBlendStart=Time.unscaledTime;
        followBlendPosition=view.transform.position;
        followBlendRotation=view.transform.rotation;
    }

    void LeaveFollow()
    {
        StopSteering();
        commanding=false;
        eyeReady=false;
        flyVelocity = Vector3.zero;
        BeginFollowBlend();
        followed=null;
        flyPosition=followBlendPosition+Vector3.up*1.45f-followBlendRotation*Vector3.forward*.9f;
    }

    void UpdateCamera()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        graphicsPointer=keyboard!=null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
        Cursor.lockState=graphicsPointer ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible=graphicsPointer;
        if(graphicsPointer)
        {
            StopSteering();
            flyVelocity=Vector3.zero;
            return;
        }
        Vector2 lookDelta=Vector2.zero;
        if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
        {
            lookDelta=mouse.delta.ReadValue();
            var delta=lookDelta*(.13f*(1-firstPersonAim*.49f));
            yaw=Mathf.Repeat(yaw+delta.x,360);
            pitch=Mathf.Clamp(pitch-delta.y,-86,86);
        }
        if (keyboard != null)
        {
            if (keyboard.fKey.wasPressedThisFrame)
            {
                if (followed != null) LeaveFollow();
                else SelectSoldier(false);
            }
            if (keyboard.tabKey.wasPressedThisFrame)
                SelectSoldier(true,keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        }
        float dtFrame=Mathf.Min(.075f,Time.unscaledDeltaTime);
        float aim=followed!=null && mouse!=null && mouse.rightButton.isPressed ? 1 : 0;
        firstPersonAim=Mathf.Lerp(firstPersonAim,aim,1-Mathf.Exp(-dtFrame*12));
        var rotation = Quaternion.Euler(pitch, yaw, 0);
        if (followed != null && followed.Rig && followed.Rig.gameObject.activeInHierarchy)
        {
            view.nearClipPlane=.085f;
            UpdateFirstPersonOrders(keyboard,mouse,rotation);
            flyPosition=StabilizeEye(followed,rotation);
            ApplySoldierCamera(followed,ref flyPosition,ref rotation);
        }
        else
        {
            if (followed != null) LeaveFollow();
            view.nearClipPlane=.20f;
            var desiredVelocity = Vector3.zero;
            if (keyboard != null)
            {
                float x = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
                float z = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
                float y = (keyboard.eKey.isPressed ? 1 : 0) - (keyboard.qKey.isPressed ? 1 : 0);
                var move = Quaternion.Euler(0, yaw, 0) * new Vector3(x, y, z).normalized;
                float speed = keyboard.leftAltKey.isPressed ? .28f : keyboard.leftShiftKey.isPressed ? 3f : 1;
                desiredVelocity = move * (Mathf.Max(6, Mathf.Max(half.x, half.y) * .15f * Mathf.Sqrt(MapScale)) * speed);
            }
            float dt=Mathf.Min(Time.unscaledDeltaTime,.065f);
            float response=desiredVelocity.sqrMagnitude>0 ? 13 : 20;
            flyVelocity=Vector3.Lerp(flyVelocity,desiredVelocity,1-Mathf.Exp(-response*dt));
            if(flyVelocity.sqrMagnitude<.001f)flyVelocity=Vector3.zero;
            var unconstrained=flyPosition+flyVelocity*dt;
            flyPosition=unconstrained;
            flyPosition.x = Mathf.Clamp(flyPosition.x, -half.x * 1.3f * MapScale, half.x * 1.3f * MapScale);
            flyPosition.z = Mathf.Clamp(flyPosition.z, Offset - half.y * 1.3f * MapScale, Offset + half.y * 1.3f * MapScale);
            flyPosition.y = Mathf.Clamp(flyPosition.y, GroundHeight(flyPosition.x/MapScale, (flyPosition.z-Offset)/MapScale) + .30f,
                Mathf.Max(30, Mathf.Max(half.x, half.y) * 2));
            if(flyPosition.x!=unconstrained.x)flyVelocity.x=0;
            if(flyPosition.y!=unconstrained.y)flyVelocity.y=0;
            if(flyPosition.z!=unconstrained.z)flyVelocity.z=0;
        }
        SyncHeldModel(lookDelta);
        if (followBlendStart>=0)
        {
            float t=Mathf.Clamp01((Time.unscaledTime-followBlendStart)/.62f);
            if(t>=1)followBlendStart=-1;
            else
            {
                float ease=Mathf.SmoothStep(0,1,t);
                var point=Vector3.Lerp(followBlendPosition,flyPosition,ease);
                float distance=Vector3.Distance(followBlendPosition,flyPosition);
                point.y+=Mathf.Sin(t*Mathf.PI)*Mathf.Min(4,distance*.15f);
                point.y=Mathf.Max(point.y,GroundHeight(point.x/MapScale,(point.z-Offset)/MapScale)+.23f);
                view.transform.SetPositionAndRotation(point,Quaternion.Slerp(followBlendRotation,rotation,ease));
                return;
            }
        }
        view.transform.SetPositionAndRotation(flyPosition, rotation);
    }

    void LateUpdate()
    {
        if (!root) return;
        try
        {
            if (!Available() || (exitStart >= 0 && Time.unscaledTime-exitStart >= OutroTime)) { Close(); return; }
            if (Time.unscaledTime >= nextScan)
            {
                var dead = new List<Component>();
                foreach (var entry in models) if (!entry.Key) { Destroy(entry.Value.Root.gameObject); dead.Add(entry.Key); }
                foreach (var actor in dead) models.Remove(actor);
                var oldSprites = new List<SpriteRenderer>();
                foreach (var entry in sprites) if (!entry.Key) { Destroy(entry.Value.View.gameObject); oldSprites.Add(entry.Key); }
                foreach (var item in oldSprites) sprites.Remove(item);
                Scan(structureType, true);
                Scan(unitType, false);
                ScanSprites();
                ConnectForts();
                OrientFieldworks();
                nextScan = Time.unscaledTime + .12f;
            }

            crowdDetailRange=Mathf.Lerp(32,18,Mathf.InverseLerp(500,1450,soldiers.Count));
            soldiers.Clear();
            foreach (var entry in models)
            {
                var actor = entry.Key;
                var model = entry.Value;
                bool isUnit = actor && actor.GetType() == unitType;
                var unit = isUnit ? Read(actor, "_unit") : null;
                bool visible = actor && actor.gameObject.activeInHierarchy &&
                    (!isUnit || (unit != null && ReferenceEquals(unit,model.Unit) && true.Equals(Read(unit, "Alive"))));
                model.Root.gameObject.SetActive(visible);
                if (!visible) continue;
                var sprite = Read(actor, "_sr") as SpriteRenderer;
                var center = actor.transform.position;
                var unitPos = Read(unit, "Position");
                if (unitPos is Vector2) { center.x = ((Vector2)unitPos).x; center.y = ((Vector2)unitPos).y; }
                float h = GroundHeight(center.x, center.y);
                if (isUnit)
                {
                    var data = Read(unit, "Data");
                    bool air = true.Equals(Read(actor, "_isAir"));
                    if (air) h += Mathf.Max(2, Convert.ToSingle(Read(data, "altitude")));
                    var vel = Read(unit, "Velocity");
                    var velocity = vel is Vector2 ? (Vector2)vel : Vector2.zero;
                    var squad = Read(actor, "_squad") ?? Read(actor, "_mass");
                    var direction = Read(squad, "_facing");
                    var facing = velocity.sqrMagnitude > .01f ? velocity : direction is Vector2 ? (Vector2)direction : Vector2.zero;
                    if (facing.sqrMagnitude > .01f)
                        model.Root.localRotation = Quaternion.Euler(0, Mathf.Atan2(facing.x, facing.y) * Mathf.Rad2Deg, 0);
                    float body = Mathf.Clamp(Convert.ToSingle(Read(actor, "_bodyDiameter")) * .48f, .30f, 1.5f);
                    if(model.Kind==Kind.Armor)body=Mathf.Max(.78f,body*1.90f);
                    if(model.Kind==Kind.Aircraft)body=Mathf.Max(.72f,body*1.68f);
                    model.Root.localScale = new Vector3(body/MapScale,body,body/MapScale);
                    float clearance=model.Kind==Kind.Aircraft ? .12f : .025f;
                    model.Root.localPosition = new Vector3(center.x,h+clearance,center.y);
                    UpdatePeople(model,unit,new Vector2(center.x,center.y),velocity);
                    continue;
                }
                else if(model.Field!=Fieldwork.None)
                {
                    Rect area;
                    object owner;
                    Vector2 size=sprite ? (Vector2)sprite.bounds.size : Vector2.one;
                    if(TryFort(actor,out area,out owner))
                    {
                        center.x=area.center.x;
                        center.y=area.center.y;
                        size=area.size;
                    }
                    float width=Mathf.Max(.30f,Mathf.Min(size.x,size.y));
                    float length=Mathf.Max(width,Mathf.Max(size.x,size.y));
                    h=GroundHeight(center.x,center.y);
                    model.Root.localScale=new Vector3(width,FieldworkHeight(model.Field,width),length);
                    model.Root.localPosition=new Vector3(center.x,h+.025f,center.y);
                    continue;
                }
                else if (sprite)
                {
                    var size = sprite.bounds.size;
                    float plan = model.Fort ? 1 : Mathf.Sqrt(MapScale);
                    model.Root.localScale = new Vector3(Mathf.Clamp(size.x, .35f, 35)/plan,
                        Mathf.Clamp(Mathf.Min(size.x, size.y) * (model.Keep ? 1.18f : .74f), .45f, 13), Mathf.Clamp(size.y, .35f, 35)/plan);
                }
                model.Root.localPosition = new Vector3(center.x, h + .18f, center.y);
            }

            SeparatePeople();
            if (exitStart < 0) UpdateCamera();
            else if(heldModel)heldModel.gameObject.SetActive(false);
            UpdateGrassTrampling();
            if (fxManager) { SyncEffects("_active", false); SyncEffects("_projectiles", true); }
            foreach (var entry in sprites) SyncSprite(entry.Key, entry.Value);
            RenderTransition();
            DrawAtmosphere();
            DrawWater();
            DrawContactShadows();
            DrawRain();
            DrawWindDebris();
            DrawCombatEffects();
            DrawHaze();
            DrawStorm();
            DrawFortFlags();
            DrawTorches();
            DrawAdvancedGraphics();
            view.Render();
        }
        catch (Exception error) { Fail(error); }
    }

    void OnGUI()
    {
        DrawPublicationMenu();
        if (root && frame)
        {
            GUI.depth = -1000;
            var rect = new Rect(0, 0, Screen.width, Screen.height);
            float fade = exitStart < 0 ? Mathf.Clamp01((Time.unscaledTime-transitionStart)/.26f) :
                1-Mathf.Clamp01(((Time.unscaledTime-exitStart)/OutroTime-.42f)/.58f);
            var previousColor = GUI.color;
            var weatherTint=rainMesh && !advancedActive ? new Color(.74f,.79f,.85f) : Color.white;
            weatherTint.a=Mathf.SmoothStep(0,1,fade);
            GUI.color = weatherTint;
            GUI.DrawTexture(rect, frame, ScaleMode.StretchToFill, fade < 1);
            if (stormLight > .015f)
            {
                GUI.color = new Color(.77f,.86f,1,stormLight*fade*.15f);
                GUI.DrawTexture(rect,Texture2D.whiteTexture);
            }
            GUI.color = previousColor;
            DrawSunGlare(fade);
            var controlsRect = new Rect(16, Screen.width < 960 ? 126 : 16, Mathf.Min(640, Screen.width - 32), 52);
            DrawControlHint(controlsRect);
            DrawGraphicsButton();
            GUI.Label(new Rect(Screen.width / 2 - 4, Screen.height / 2 - 10, 20, 22), "+");
            DrawNotice(controlsRect.yMax + 6);
        }
        else DrawNotice(16);
    }

    void Fail(Exception error)
    {
        Debug.LogException(error);
        Close();
        ShowNotice("3D kullanılamıyor. Unity oyuncu günlüğünü kontrol edin.",
            "3D is unavailable. Check the Unity player log.", 5);
    }

    void Close()
    {
        if(root)
        {
            StopSteering();
            ShowSingleUnitIcons();
        }
        ReleaseCombatEffects();
        ReleaseWindDebris();
        ReleaseAdvancedGraphics();
        if (input) input.enabled = true;
        input = null;
        commanding=steering=eyeReady=false;
        firstPersonAim=lastSeparation=0;
        heldModel=null;
        heldPerson=null;
        lastAttack=-10;
        if (root) { root.SetActive(false); Destroy(root); Debug.Log("3D closed."); }
        if (view) view.targetTexture = null;
        if (frame) { frame.Release(); Destroy(frame); }
        foreach (var material in materials.Values) Destroy(material);
        materials.Clear();
        if (spriteMaterial) Destroy(spriteMaterial);
        if (cube) Destroy(cube);
        if (octagon) Destroy(octagon);
        octagon=null;
        spriteMaterial = null;
        cube = null;
        sprites.Clear();
        fortLinks.Clear();
        knownFortIds.Clear();
        fortTopology.Clear();
        forts.Clear();
        fieldSignature=0;
        fieldPlacements.Clear();
        ReleaseContactShadows();
        ReleaseWater();
        ReleaseStorm();
        ReleaseHaze();
        ReleaseFortFlags();
        ReleaseTorches();
        fortSignature = 0;
        ReleaseAtmosphere();
        if (rainMesh) Destroy(rainMesh);
        if (rainMaterial) Destroy(rainMaterial);
        rainMesh = null;
        rainMaterial = null;
        drops = null;
        rainVertices = null;
        rainColors = null;
        rainRoofs.Clear();
        curtainRoofs.Clear();
        nextRoofUpdate = 0;
        splashPoints = null;
        splashAges = null;
        exitStart = -1;
        soldiers.Clear();
        soldierCells.Clear();
        if (terrainMesh) Destroy(terrainMesh);
        terrainMesh = null;
        if (terrainMaterial) Destroy(terrainMaterial);
        if (terrainFinish) Destroy(terrainFinish);
        terrainFinish=null;
        terrainMaterial = null;
        ReleaseLandscape();
        heights = null;

        followed = null;
        if (ownsCursor)
        {
            Cursor.lockState = oldCursorLock;
            Cursor.visible = oldCursorVisible;
            ownsCursor = false;
        }
        foreach (var mesh in infantryMeshes.Values) Destroy(mesh);
        infantryMeshes.Clear();
        figureTemplates.Clear();
        figureLibrary=null;
        ReleaseDetailShapes();
        models.Clear();
        root = null;
        landRoot=null;
        view = null;
        frame = null;
    }

    void OnDisable() { Close(); }
}





























