using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class Battle3D
{
    readonly Dictionary<long, Transform> fortLinks = new Dictionary<long, Transform>();
    readonly List<Component> forts = new List<Component>();
    readonly HashSet<int> knownFortIds = new HashSet<int>();
    readonly HashSet<long> fortTopology = new HashSet<long>();
    Mesh rainMesh;
    int fortSignature;
    Material rainMaterial;
    Vector3[] drops, rainVertices, splashPoints;
    float[] splashAges;
    Color32[] rainColors;
    System.Random weatherRandom;
    float rainStrength, transitionStart, exitStart = -1;
    Vector3 transitionFocus, exitPosition, exitFocus;
    Vector2 previousMapFocus;
    Quaternion exitRotation;
    float exitFov;
    const float IntroTime = .82f, OutroTime = .48f;

    static bool Fortification(string id)
    {
        return id.Contains("wall") || id.Contains("palisade") || id.Contains("fence") ||
            id.Contains("castle") || id.Contains("fort") || id.Contains("tower") || id == "gate";
    }

    void MakeFort(Model model,string id,Color tint)
    {
        bool timber = id.Contains("palisade") || id.Contains("fence") || id.Contains("wood");
        var baseTint = timber ? new Color(.37f,.26f,.16f) : new Color(.48f,.44f,.39f);
        var stone = Color.Lerp(baseTint, tint, .12f);
        if (rainMesh) stone *= new Color(.85f,.89f,.94f,1);
        model.Fort = true;
        model.FortTint = stone;
        model.Timber = timber;
        model.Gate = id == "gate";
        model.Keep = id=="castle" || id.Contains("tower");
        if (model.Gate)
        {
            MakeGatehouse(model.Root,stone);
            return;
        }
        if (timber)
        {
            MakeTimberWall(model.Root,stone,.87f,1.05f,.98f,7);
            return;
        }
        if (model.Keep)
        {
            MakeKeep(model.Root,stone);
            if (id=="castle")
                CrownKeep(model.Root,stone,tint);
            return;
        }
        Part(model.Root, "Sur temeli", stone*.72f, new Vector3(0,.13f,0), new Vector3(1.09f,.27f,1.08f));
        Part(model.Root, "Sur gövdesi", stone, new Vector3(0,.58f,0), new Vector3(.98f,.91f,.97f));
        Part(model.Root, "Üst yürüyüş yolu", stone*.84f, new Vector3(0,1.03f,0), new Vector3(1.08f,.15f,1.07f));
        for (int side = -1; side <= 1; side += 2)
        for (int i = -1; i <= 1; i++)
        {
            Part(model.Root, "Mazgal", stone * (i == 0 ? .94f : 1.04f), new Vector3(i*.35f,1.21f,side*.43f), new Vector3(.22f,.28f,.18f));
            Part(model.Root, "Yan mazgal", stone, new Vector3(side*.43f,1.21f,i*.35f), new Vector3(.18f,.28f,.22f));
        }
        for (int i = 0; i < 3; i++)
            Part(model.Root, "Duvar derzi", stone*.72f, new Vector3(0,.29f+i*.21f,.487f), new Vector3(.97f,.022f,.012f));
        if (!timber)
        {
            Masonry(model.Root, stone, .98f, 1.02f, .96f, 3);
            var face = Joint(model.Root, "Ön ve arka taş örgüsü", Vector3.zero);
            face.localRotation = Quaternion.Euler(0,90,0);
            Masonry(face, stone, .97f, 1.02f, .96f, 7);
        }
        Part(model.Root, "Ok penceresi", stone*.23f, new Vector3(0,.70f,.50f), new Vector3(.085f,.31f,.024f));
    }

    Transform KeepRing(Transform keep,string label,Color stone,float y,float height,float diameter)
    {
        if(!octagon)
        {
            var verts=new List<Vector3>();
            var colors=new List<Color>();
            var faces=new List<int>();
            Loft(verts,colors,faces,Vector3.down*.5f,Vector3.up*.5f,.5f,.5f,Color.white,8,0,0);
            octagon=new Mesh{name="Sekizgen kule gövdesi"};
            octagon.SetVertices(verts);
            octagon.SetColors(colors);
            octagon.SetTriangles(faces,0);
        }
        var part=Part(keep,label,stone,new Vector3(0,y,0),new Vector3(diameter,height,diameter));
        part.GetComponent<MeshFilter>().sharedMesh=octagon;
        return part;
    }

    void MakeKeep(Transform keep,Color stone)
    {
        KeepRing(keep,"Kule kaidesi",stone*.68f,.105f,.25f,1.25f);
        for(int row=0;row<6;row++)
            KeepRing(keep,"Kule taş sırası",stone*(.86f+(row%3)*.053f),.224f+row*.138f,.141f,1.13f+(row%2)*.018f);
        KeepRing(keep,"Kule kornişi",stone*.71f,1.025f,.11f,1.27f);
        KeepRing(keep,"Kule yürüyüş yolu",stone*.96f,1.095f,.10f,1.29f);
        for(int i=0;i<8;i++)
        {
            float angle=(i+.5f)*Mathf.PI/4;
            var radial=new Vector3(Mathf.Cos(angle),0,-Mathf.Sin(angle));
            var top=Part(keep,"Sekizgen mazgal",stone*(i%2==0 ? 1.06f : .95f),radial*.52f+Vector3.up*1.235f,
                new Vector3(.26f,.23f,.15f));
            top.localRotation=Quaternion.LookRotation(radial);
            var slot=Part(keep,"Kule ok yarığı",stone*.26f,radial*.532f+Vector3.up*.75f,
                new Vector3(.060f,.195f,.021f));
            slot.localRotation=Quaternion.LookRotation(radial);
            var ledge=Part(keep,"Pencere eşiği",stone*.76f,radial*.549f+Vector3.up*.641f,
                new Vector3(.11f,.027f,.044f));
            ledge.localRotation=Quaternion.LookRotation(radial);
            for(int row=0;row<5;row++)
            {
                var across=Vector3.Cross(Vector3.up,radial);
                float shift=((row+i)%2-.5f)*.11f;
                var seam=Part(keep,"Taş derzi",stone*.74f,radial*.529f+across*shift+Vector3.up*(.237f+row*.138f),
                    new Vector3(.012f,.106f,.011f));
                seam.localRotation=Quaternion.LookRotation(radial);
            }
        }
    }

    void CrownKeep(Transform keep,Color stone,Color tint)
    {
        var core=Joint(keep,"İç kule",new Vector3(0,1.11f,0));
        var roof=Color.Lerp(new Color(.34f,.22f,.16f),tint,.26f);
        KeepRing(core,"İç kule gövdesi",stone*.95f,.39f,.78f,.60f);
        KeepRing(core,"Saçak",roof*.79f,.77f,.10f,.69f);
        Shape(core,"Kule çatısı","cone",roof,new Vector3(0,1.10f,0),new Vector3(.77f,.72f,.77f));
        Shape(core,"Çatı ucu","taper",stone*.73f,new Vector3(0,1.45f,0),new Vector3(.053f,.19f,.053f));
        for(int i=0;i<4;i++)
        {
            var face=Joint(core,"Üst kule yüzü",Vector3.zero);
            face.localRotation=Quaternion.Euler(0,i*90,0);
            Part(face,"Üst kule penceresi",stone*.33f,new Vector3(0,.48f,.281f),new Vector3(.10f,.24f,.024f));
        }
    }
    void MakeGatehouse(Transform gate, Color stone)
    {
        for(int side=-1;side<=1;side+=2)
        {
            var tower=Joint(gate,"Kapı kulesi",new Vector3(side*.40f,0,0));
            Part(tower,"Temel",stone*.68f,new Vector3(0,.11f,0),new Vector3(.32f,.23f,.98f));
            Part(tower,"Kule gövdesi",stone,new Vector3(0,.65f,0),new Vector3(.29f,1.15f,.90f));
            Part(tower,"Kule terası",stone*.91f,new Vector3(0,1.26f,0),new Vector3(.36f,.17f,.99f));
            for(int end=-1;end<=1;end+=2)
            {
                Part(tower,"Kule mazgalı",stone*1.09f,new Vector3(0,1.43f,end*.41f),new Vector3(.31f,.21f,.19f));
                Part(tower,"Ok açıklığı",stone*.35f,new Vector3(0,.88f,end*.455f),new Vector3(.048f,.25f,.019f));
            }
            for(int face=-1;face<=1;face+=2)
            {
                Part(tower,"Meşale demiri",stone*.28f,new Vector3(0,.59f,face*.485f),new Vector3(.058f,.14f,.10f));
                Part(tower,"Meşale kasesi",stone*.37f,new Vector3(0,.665f,face*.51f),new Vector3(.093f,.042f,.086f));
            }
            for(int row=0;row<4;row++)
            {
                float y=.25f+row*.21f;
                Part(tower,"Ön taş derzi",stone*.73f,new Vector3(0,y,.455f),new Vector3(.29f,.017f,.017f));
                Part(tower,"Arka taş derzi",stone*.73f,new Vector3(0,y,-.455f),new Vector3(.29f,.017f,.017f));
            }
        }
        // Leave the space below the ring empty so the gate remains an open passage.
        const int stones=11;
        for(int i=0;i<stones;i++)
        {
            float angle=(i+.5f)*Mathf.PI/stones;
            var pos=new Vector3(Mathf.Cos(angle)*.263f,.63f+Mathf.Sin(angle)*.263f,0);
            var shade=stone*(i==stones/2 ? 1.19f : .87f+(i%4)*.055f);
            Part(gate,"Kemer taşı",shade,pos,new Vector3(.119f,.088f,.74f))
                .localRotation=Quaternion.Euler(0,0,angle*Mathf.Rad2Deg);
        }
        Part(gate,"Kapı yürüyüş yolu",stone*.87f,new Vector3(0,1.023f,0),new Vector3(.61f,.16f,.82f));
        for(int face=-1;face<=1;face+=2)
        for(int i=-1;i<=1;i++)
            Part(gate,"Kapı parapeti",stone*1.01f,new Vector3(i*.184f,1.16f,face*.354f),new Vector3(.117f,.18f,.145f));
    }
    bool TryFort(Component actor, out Rect footprint, out object owner)
    {
        footprint = default(Rect);
        owner = null;
        var state = Read(actor, "_s");
        var area = Read(state, "Footprint");
        if (!(area is Rect)) return false;
        footprint = (Rect)area;
        owner = Read(state, "Owner");
        return footprint.width > 0 && footprint.height > 0;
    }

    static long FortKey(Component a, Component b)
    {
        int x = a.GetInstanceID(), y = b.GetInstanceID();
        return ((long)Math.Min(x,y) << 32) | (uint)Math.Max(x,y);
    }

    bool CrossesGate(Component a, Component b, Vector2 start, Vector2 delta, Dictionary<Component, Rect> footprints)
    {
        foreach (var item in footprints)
        {
            if (item.Key == a || item.Key == b || !models[item.Key].Gate) continue;
            float t = Vector2.Dot(item.Value.center-start,delta) / delta.sqrMagnitude;
            if (t <= .01f || t >= .99f) continue;
            float radius = Mathf.Min(item.Value.width,item.Value.height)*.44f;
            if ((start+delta*t-item.Value.center).sqrMagnitude < radius*radius) return true;
        }
        return false;
    }
    void ConnectForts()
    {
        forts.Clear();
        foreach (var item in models)
            if (item.Key && item.Value.Fort && item.Key.gameObject.activeInHierarchy) forts.Add(item.Key);
        var footprints = new Dictionary<Component, Rect>();
        var owners = new Dictionary<Component, object>();
        int signature = forts.Count;
        foreach (var fort in forts)
        {
            Rect footprint;
            object owner;
            if (!TryFort(fort, out footprint, out owner)) continue;
            footprints.Add(fort, footprint);
            owners.Add(fort, owner);
            signature = unchecked(signature * 397 ^ fort.GetInstanceID() ^ footprint.GetHashCode() ^
                (owner == null ? 0 : owner.GetHashCode()));
        }
        if (signature == fortSignature && fortLinks.Count > 0) return;
        fortSignature = signature;
        var keep = new HashSet<long>();
        foreach (var a in forts)
        {
            Rect from;
            object side;
            if (!footprints.TryGetValue(a, out from)) continue;
            side = owners[a];
            var best = new Component[8];
            var distances = new float[8];
            foreach (var b in forts)
            {
                if (a == b) continue;
                Rect to;

                if (!footprints.TryGetValue(b, out to) || !Equals(side, owners[b])) continue;
                if (models[a].Timber != models[b].Timber) continue;
                var delta = to.center - from.center;
                var gap = new Vector2(Mathf.Max(0, Mathf.Abs(delta.x)-(from.width+to.width)*.5f),
                    Mathf.Max(0, Mathf.Abs(delta.y)-(from.height+to.height)*.5f));
                float span = Mathf.Min(Mathf.Max(from.width,from.height), Mathf.Max(to.width,to.height));
                if (delta.sqrMagnitude < .12f || gap.magnitude > Mathf.Clamp(span*.65f, 1.25f, 3.5f)) continue;
                long candidate = FortKey(a,b);
                if (knownFortIds.Contains(a.GetInstanceID()) && knownFortIds.Contains(b.GetInstanceID()) &&
                    !fortTopology.Contains(candidate)) continue;
                if (CrossesGate(a,b,from.center,delta,footprints)) continue;
                int bin = Mathf.RoundToInt(Mathf.Atan2(delta.y,delta.x) * 4 / Mathf.PI) & 7;
                if (!best[bin] || delta.sqrMagnitude < distances[bin])
                {
                    best[bin] = b;
                    distances[bin] = delta.sqrMagnitude;
                }
            }
            foreach (var b in best)
            {
                if (!b) continue;
                long key = FortKey(a,b);
                fortTopology.Add(key);
                if (!keep.Add(key) || fortLinks.ContainsKey(key)) continue;
                Rect to;

                if (!footprints.TryGetValue(b, out to)) continue;
                if (models[a].Gate) FaceGate(models[a], to.center - from.center);
                if (models[b].Gate) FaceGate(models[b], from.center - to.center);
                var curtain = MakeCurtain(from, to, models[a].FortTint, models[a].Gate, models[b].Gate, models[a].Timber);
                fortLinks[key] = curtain;
                var meshKey = "fort-link|" + key;
                // The first section keeps its orientation for wall inspection and camera placement.
                BakeStaticParts(new Model { Root = curtain }, meshKey);
            }
        }
        foreach (var fort in footprints.Keys) knownFortIds.Add(fort.GetInstanceID());
        var stale = new List<long>();
        foreach (var link in fortLinks)
            if (!keep.Contains(link.Key)) { curtainRoofs.Remove(link.Value); Destroy(link.Value.gameObject); stale.Add(link.Key); }
        foreach (var key in stale) fortLinks.Remove(key);
    }

    static void FaceGate(Model gate, Vector2 direction)
    {
        gate.Root.localRotation = Quaternion.Euler(0, Mathf.Atan2(-direction.y,direction.x)*Mathf.Rad2Deg, 0);
    }

    void MakeBuilding(Model model, string id, Color tint)
    {
        bool rubble = id.Contains("rubble") || id.Contains("ruin");
        bool defensive = id.Contains("bunker");
        var wall = Color.Lerp(new Color(.62f,.54f,.42f),tint,.28f);
        var roof = Color.Lerp(new Color(.41f,.23f,.18f),tint,.13f);
        var dark = new Color(.19f,.17f,.15f);
        var part = model.Root;
        if (MakeSpecialStructure(part,id,wall,dark)) return;
        if (rubble)
        {
            for (int i = 0; i < 5; i++)
                Shape(part,"Moloz","round",wall*(.65f+i*.075f),new Vector3(Mathf.Sin(i*3.8f)*.3f,.11f,Mathf.Cos(i*3.8f)*.3f),
                    new Vector3(.28f,.22f+i*.025f,.34f)).localRotation = Quaternion.Euler(i*7,i*37,0);
            for(int i=0;i<7;i++)
                Part(part,"Kırık kiriş",dark*.91f,new Vector3(Mathf.Cos(i*2.39f)*.35f,.085f,Mathf.Sin(i*2.39f)*.34f),
                    new Vector3(.052f,.067f,.23f)).localRotation=Quaternion.Euler(12,i*69,0);
            return;
        }
        if (defensive)
        {
            Part(part,"Beton gövde",wall*.76f,new Vector3(0,.26f,0),new Vector3(.99f,.49f,.97f));
            Part(part,"Siper üstü",wall*.67f,new Vector3(0,.53f,0),new Vector3(1.04f,.15f,.99f));
            Part(part,"Ateş açıklığı",dark,new Vector3(0,.38f,.492f),new Vector3(.45f,.095f,.018f));
            for (int i=-2;i<=2;i++)
            {
                Shape(part,"Kum torbası","round",wall*.81f,new Vector3(i*.18f,.63f,.42f),new Vector3(.19f,.12f,.22f));
                Shape(part,"Kum torbası","round",wall*.85f,new Vector3(i*.18f,.15f,-.45f),new Vector3(.20f,.12f,.21f));
            }
            return;
        }
        model.Settlement=true;
        MakeSettlementBuilding(part,id,wall,roof,dark);
    }

    bool MakeSpecialStructure(Transform part,string id,Color earth,Color dark)
    {
        if(id=="cave")
        {
            var rock=Color.Lerp(earth,new Color(.37f,.37f,.34f),.71f);
            for(int i=0;i<9;i++)
            {
                float theta=Mathf.PI*(i+.25f)/8.5f;
                Shape(part,"Mağara ağzı","round",rock*(.83f+Noise01(i)*.31f),
                    new Vector3(Mathf.Cos(theta)*.41f,.13f+Mathf.Sin(theta)*.53f,.055f),
                    new Vector3(.24f,.26f,.83f)).localRotation=Quaternion.Euler(i*4,0,i*11);
            }
            Part(part,"Mağara gölgesi",dark*.61f,new Vector3(0,.28f,-.30f),new Vector3(.65f,.52f,.04f));
            return true;
        }
        if(id.Contains("dronenet"))
        {
            Part(part,"Ağ kontrol kabini",earth*.63f,new Vector3(0,.24f,0),new Vector3(.76f,.48f,.73f));
            for(int i=-1;i<=1;i+=2)
                Part(part,"Anten",dark,new Vector3(i*.29f,.68f,-.17f),new Vector3(.033f,.69f,.033f));
            Shape(part,"Radar","shield",earth*.84f,new Vector3(0,.76f,.13f),new Vector3(.53f,.36f,.11f))
                .localRotation=Quaternion.Euler(-26,-23,0);
            return true;
        }
        return false;
    }

    void MakeSettlementBuilding(Transform part,string id,Color wall,Color roof,Color dark)
    {
        bool terrace=id.Contains("apartment")||id.Contains("factory")||id.Contains("industrial");
        bool storage=id.Contains("barn")||id.Contains("depot")||id.Contains("warehouse");
        int floors=terrace ? 3 : storage ? 1 : 2;
        var timber=new Color(.33f,.23f,.15f);
        var glass=new Color(.29f,.39f,.40f);
        float top=terrace ? 1.17f : .86f;
        Part(part,"Taş temel",wall*.66f,new Vector3(0,.085f,0),new Vector3(1.00f,.18f,.98f));
        Part(part,"Cephe",wall,new Vector3(0,(top+.14f)*.5f,0),new Vector3(.90f,top-.14f,.85f));
        for(int dir=0;dir<4;dir++)
        {
            bool front=dir==0, narrow=dir%2!=0;
            float z=narrow ? .451f : .426f;
            var facade=Joint(part,"Cephe ayrıntıları",Vector3.zero);
            facade.localRotation=Quaternion.Euler(0,dir*90,0);
            Part(facade,"Temel pervazı",wall*.76f,new Vector3(0,.171f,z),new Vector3(.89f,.053f,.067f));
            for(int side=-1;side<=1;side+=2)
            {
                Part(facade,"Köşe dikmesi",terrace ? wall*.78f : timber,new Vector3(side*.406f,(top+.19f)*.5f,z),
                    new Vector3(.065f,top-.19f,.051f));
                for(int floor=0;floor<floors;floor++)
                {
                    float y=.377f+floor*((top-.19f)/floors);
                    float x=side*(narrow ? .193f : .248f);
                    Part(facade,"Pencere çerçevesi",timber,new Vector3(x,y,z+.024f),new Vector3(.182f,.167f,.043f));
                    Part(facade,"Pencere camı",Color.Lerp(dark,glass,.55f+dir*.12f),new Vector3(x,y,z+.049f),new Vector3(.137f,.126f,.014f));
                    Part(facade,"Pencere çıtası",timber*.90f,new Vector3(x,y,z+.058f),new Vector3(.019f,.126f,.012f));
                    Part(facade,"Pencere eşiği",wall*.80f,new Vector3(x,y-.084f,z+.053f),new Vector3(.21f,.025f,.067f));
                    if(!terrace && !storage)
                        Part(facade,"Ahşap kepenk",timber*.84f,new Vector3(x+side*.113f,y,z+.037f),new Vector3(.038f,.143f,.029f));
                }
            }
            Part(facade,"Saçak altı",wall*.81f,new Vector3(0,top-.035f,z),new Vector3(.90f,.064f,.068f));
            if(front)
            {
                float door=storage ? .31f : .203f;
                Part(facade,"Kapı çerçevesi",timber*.61f,new Vector3(0,.317f,z+.026f),new Vector3(door+.049f,.38f,.071f));
                for(int slat=-2;slat<=2;slat++)
                    Part(facade,"Kapı tahtası",timber*(.75f+(slat+2)*.07f),new Vector3(slat*door/5,.299f,z+.067f),
                        new Vector3(door*.19f,.327f,.019f));
                Part(facade,"Kapı kolu",wall*.79f,new Vector3(door*.32f,.30f,z+.085f),new Vector3(.029f,.037f,.024f));
                Part(facade,"Kapı basamağı",wall*.72f,new Vector3(0,.05f,z+.087f),new Vector3(door+.11f,.10f,.21f));
            }
        }
        if(terrace)
        {
            Part(part,"Teras",roof*.71f,new Vector3(0,top+.023f,0),new Vector3(1.01f,.10f,.99f));
            for(int side=-1;side<=1;side+=2)
            {
                Part(part,"Teras korkuluğu",wall*.78f,new Vector3(side*.47f,top+.117f,0),new Vector3(.056f,.119f,.96f));
                Part(part,"Teras korkuluğu",wall*.78f,new Vector3(0,top+.117f,side*.46f),new Vector3(.97f,.119f,.056f));
            }
        }
        else
        {
            Shape(part,"Çatı altı","gable",wall*.91f,new Vector3(0,top+.14f,0),new Vector3(.90f,.29f,.85f));
            for(int side=-1;side<=1;side+=2)
            {
                var slope=Joint(part,"Kiremit çatı",new Vector3(side*.262f,top+.153f,0));
                slope.localRotation=Quaternion.Euler(0,0,side*-30);
                Part(slope,"Çatı örtüsü",roof*.82f,Vector3.zero,new Vector3(.617f,.060f,1.06f));
                for(int row=0;row<5;row++)
                for(int col=0;col<9;col++)
                {
                    float tone=.84f+.29f*Noise01(row*41+col*13+side);
                    float z=-.47f+col*.117f+(row%2==0 ? 0 : .020f);
                    Part(slope,"Kiremit",roof*tone,new Vector3(-.245f+row*.123f,.037f,z),
                        new Vector3(.114f,.028f,.108f));
                }
            }
            Part(part,"Mahya",roof*1.05f,new Vector3(0,top+.314f,0),new Vector3(.095f,.074f,1.078f));
        }
        float chimney=top+(terrace ? .22f : .36f);
        Part(part,"Baca",wall*.72f,new Vector3(.276f,chimney,-.249f),new Vector3(.14f,.36f,.145f));
        Part(part,"Baca başlığı",wall*.88f,new Vector3(.276f,chimney+.172f,-.249f),new Vector3(.184f,.047f,.179f));
        Part(part,"Baca deliği",dark,new Vector3(.276f,chimney+.198f,-.249f),new Vector3(.093f,.009f,.090f));
    }

    void Masonry(Transform section, Color stone, float width, float height, float length, int sectionIndex)
    {
        const int rows = 5;
        int blocks = Mathf.Clamp(Mathf.CeilToInt(length / Mathf.Max(.28f,height*.28f)), 2, 14);
        float course = height*.73f/rows;
        float brick = length/blocks;
        for (int side=-1; side<=1; side+=2)
        for (int row=0; row<rows; row++)
        for (int col=0; col<=blocks; col++)
        {
            float offset = (row % 2)*.5f;
            float start = Mathf.Max(-length*.5f, (col-offset)*brick-length*.5f);
            float stop = Mathf.Min(length*.5f, (col+1-offset)*brick-length*.5f);
            if (stop-start < .06f) continue;
            float variation = Mathf.PerlinNoise(col*.73f + sectionIndex*.51f + side*2 + 8, row*.87f+12);
            float depth = Mathf.Lerp(.028f,.075f,variation);
            float shade = Mathf.Lerp(.72f,1.12f,variation) * Mathf.Lerp(.82f,1,row/4f);
            var tint = Color.Lerp(stone*shade, new Color(.40f,.35f,.28f), Mathf.Max(0,variation-.57f)*.65f);
            Part(section,"Taş blok",tint,new Vector3(side*(width*.498f+depth*.25f),height*.19f+(row+.5f)*course,(start+stop)*.5f),
                new Vector3(depth,course*.87f,(stop-start)*.943f));
        }
        for (int side=-1; side<=1; side+=2)
        {
            Part(section,"Temel gölgesi",stone*.49f,new Vector3(side*width*.56f,height*.065f,0),
                new Vector3(.04f,height*.10f,length*.995f));
            Part(section,"Taş korniş",stone*1.03f,new Vector3(side*width*.53f,height*.895f,0),
                new Vector3(width*.09f,height*.075f,length));
        }
    }
    void MakeTimberWall(Transform wall,Color wood,float width,float height,float length,int posts)
    {
        float spacing=length/posts;
        Part(wall,"Ahşap yürüyüş yolu",wood*.74f,new Vector3(0,height*.57f,0),
            new Vector3(width*.76f,height*.095f,length));
        for(int side=-1;side<=1;side+=2)
        {
            for(int beam=0;beam<2;beam++)
                Part(wall,"Yatay bağ kirişi",wood*.69f,new Vector3(side*width*.34f,height*(.28f+beam*.34f),0),
                    new Vector3(width*.15f,height*.09f,length));
            for(int i=0;i<posts;i++)
            {
                float z=(i+.5f)*spacing-length*.5f;
                float top=height*(.93f+.12f*Mathf.PerlinNoise(i*.73f+side,7));
                var tint=wood*(.78f+.35f*Mathf.PerlinNoise(i*.63f+side+4,17));
                float x=side*width*.37f;
                Part(wall,"Palisad kazığı",tint,new Vector3(x,top*.46f,z),
                    new Vector3(width*.22f,top*.92f,spacing*.91f));
                Part(wall,"Kazık sivrisi",tint*1.12f,new Vector3(x,top*.95f,z),
                    new Vector3(width*.145f,top*.14f,spacing*.65f));
                Part(wall,"Kazık ucu",tint*1.17f,new Vector3(x,top*1.027f,z),
                    new Vector3(width*.075f,top*.09f,spacing*.34f));
            }
        }
    }
    Transform MakeCurtain(Rect from, Rect to, Color stone, bool gateFrom, bool gateTo, bool timber)
    {
        var link = Joint(landRoot, "Bağlı kale suru", Vector3.zero);
        var roofs = new List<RainRoof>();
        curtainRoofs.Add(link,roofs);
        Vector2 startPoint = from.center, endPoint = to.center;
        Vector2 axis = (endPoint-startPoint).normalized;
        if (gateFrom) startPoint += axis * Mathf.Min(from.width,from.height)*.30f;
        if (gateTo) endPoint -= axis * Mathf.Min(to.width,to.height)*.30f;
        Vector2 delta = endPoint - startPoint;
        float height = Mathf.Clamp(Mathf.Min(Mathf.Max(from.width,from.height), Mathf.Max(to.width,to.height))*.90f,.95f,7);
        float thickness = Mathf.Clamp(Mathf.Min(Mathf.Min(from.width,from.height),Mathf.Min(to.width,to.height))*.74f,.28f,3);
        float rise=Mathf.Abs(GroundHeight(startPoint.x,startPoint.y)-GroundHeight(endPoint.x,endPoint.y));
        int sections=Mathf.Clamp(Mathf.Max(Mathf.CeilToInt(delta.magnitude/3),Mathf.CeilToInt(rise/.52f)),1,32);
        for (int i = 0; i < sections; i++)
        {
            var a = startPoint + delta * (i/(float)sections);
            var b = startPoint + delta * ((i+1)/(float)sections);
            float lo=float.PositiveInfinity, hi=float.NegativeInfinity;
            var cross=new Vector2(axis.y,-axis.x)*thickness*.54f;
            for(int step=0;step<=4;step++)
            for(int side=-1;side<=1;side++)
            {
                var point=Vector2.Lerp(a,b,step*.25f)+cross*side;
                float ground=GroundHeight(point.x,point.y);
                lo=Mathf.Min(lo,ground);
                hi=Mathf.Max(hi,ground);
            }
            var center=(a+b)*.5f;
            float deck=hi+.035f;
            float length=Vector2.Distance(a,b)+.04f;
            var section=Joint(link,"Dik sur bölümü",new Vector3(center.x,deck,center.y));
            section.localRotation=Quaternion.LookRotation(new Vector3(axis.x,0,axis.y));
            float foundationLow=lo-deck-.12f;
            float foundationTop=height*.28f;
            Part(section,"Eğim temeli",stone*.72f,new Vector3(0,(foundationLow+foundationTop)*.5f,0),
                new Vector3(thickness*1.14f,foundationTop-foundationLow,length));
            roofs.Add(new RainRoof { Center=section.localPosition, Axis=axis, Extents=new Vector2(thickness*.55f,length*.5f),
                Top=deck+height*(timber ? .62f : 1.03f) });            if (timber)
            {
                MakeTimberWall(section,stone,thickness,height,length,
                    Mathf.Clamp(Mathf.CeilToInt(length/Mathf.Max(.18f,thickness*.35f)),4,16));
                continue;
            }
            Part(section, "Taş perde", stone, new Vector3(0,height*.48f,0), new Vector3(thickness,height*.93f,length));
            Part(section, "Yürüyüş yolu", stone*.87f, new Vector3(0,height*.98f,0), new Vector3(thickness*1.15f,height*.15f,length));
            Masonry(section, stone, thickness, height, length, i);
            int teeth = Mathf.Clamp(Mathf.CeilToInt(length / Mathf.Max(.35f,thickness*.72f)),1,12);
            for (int tooth = 0; tooth < teeth; tooth++)
            for (int side = -1; side <= 1; side += 2)
                Part(section, "Mazgal", stone*1.05f, new Vector3(side*thickness*.43f,height*1.13f,length*((tooth+.5f)/teeth-.5f)),
                    new Vector3(thickness*.26f,height*.23f,length/teeth*.64f));
        }
        return link;
    }

    float RandomRange(float min, float max)
    {
        return min + (max-min)*(float)weatherRandom.NextDouble();
    }

    void SetupRain()
    {
        var level = Read(battle, "_level");
        int weather = Convert.ToInt32(Read(level, "weather"));
        if (weather < 3 || weather > 5) return;
        float authored = Convert.ToSingle(Read(level, "weatherIntensity"));
        rainStrength = Mathf.Clamp(authored > 0 ? authored : weather == 5 ? .86f : .55f,.18f,1.4f);
        int count = Mathf.RoundToInt(2600 * rainStrength);
        drops = new Vector3[count];
        splashPoints = new Vector3[count];
        splashAges = new float[count];
        rainVertices = new Vector3[count*8];
        rainColors = new Color32[rainVertices.Length];
        var indices = new int[count*12];
        for (int i = 0; i < count; i++)
        {
            drops[i].y = float.NegativeInfinity;
            int v = i*8, t = i*12;
            indices[t] = v; indices[t+1] = v+2; indices[t+2] = v+1;
            indices[t+3] = v+1; indices[t+4] = v+2; indices[t+5] = v+3;
            rainColors[v] = rainColors[v+1] = new Color32(187,215,238,112);
            rainColors[v+2] = rainColors[v+3] = new Color32(177,204,224,8);
            indices[t+6] = v+4; indices[t+7] = v+6; indices[t+8] = v+5;
            indices[t+9] = v+5; indices[t+10] = v+6; indices[t+11] = v+7;
        }
        weatherRandom = new System.Random(713);
        rainMesh = new Mesh { name = "Havadan düşen yağmur" };
        rainMesh.MarkDynamic();
        rainMesh.vertices = rainVertices;
        rainMesh.colors32 = rainColors;
        rainMesh.triangles = indices;
        rainMaterial = new Material(shader);
        rainMaterial.SetInt("_SrcBlend",5);
        rainMaterial.SetInt("_DstBlend",10);
        rainMaterial.SetInt("_ZWrite",0);
        rainMaterial.SetInt("_Cull",0);
        rainMaterial.renderQueue = 3100;
        var rain = new GameObject("3D Yağmur");
        rain.layer = Layer;
        rain.transform.SetParent(landRoot,false);
        rain.AddComponent<MeshFilter>().sharedMesh = rainMesh;
        rain.AddComponent<MeshRenderer>().sharedMaterial = rainMaterial;
        view.backgroundColor = new Color(.25f,.31f,.36f);
    }

    void DrawRain()
    {
        if (!rainMesh) return;
        RefreshRainRoofs();
        Vector3 center = MapPoint(view.transform.position);
        float radius = Mathf.Clamp(23 + center.y*.55f,24,80);
        var velocity = RainWind(Time.unscaledTime)+Vector3.down*24;
        bool fineRain=advancedRequested;
        var width = landRoot.InverseTransformVector(view.transform.right*(fineRain ? .012f : .017f));
        float dt = Mathf.Min(Time.unscaledDeltaTime,.05f);
        for (int i = 0; i < drops.Length; i++)
        {
            bool foreground = (i & 3) == 0;
            float reach = foreground ? Mathf.Clamp(9+center.y*.11f,9,17) : radius;
            var p = drops[i];
            bool fresh = float.IsNegativeInfinity(p.y);
            float floorAtDrop = fresh ? 0 : RainSurface(p.x,p.z);
            bool hitsGround = !fresh && p.y < floorAtDrop;
            if (hitsGround && Mathf.Abs(p.x)<=half.x && Mathf.Abs(p.z)<=half.y)
            {
                splashPoints[i] = new Vector3(p.x,floorAtDrop+.028f,p.z);
                splashAges[i] = .18f;
            }
            bool outside = Mathf.Abs(p.x-center.x)>reach || Mathf.Abs(p.z-center.z)>reach;
            if (fresh || outside ||
                hitsGround)
            {
                p.x = center.x + RandomRange(-reach,reach);
                p.z = center.z + RandomRange(-reach,reach);
                float floor = RainSurface(p.x,p.z);
                float ceiling = foreground ? Mathf.Max(floor+11,center.y+9) : Mathf.Max(floor+23,center.y+16);
                p.y = fresh || outside ? RandomRange(floor,ceiling) : ceiling + RandomRange(0,3);
            }
            else p += velocity*dt;
            drops[i] = p;
            Vector3 tail = p - velocity*((foreground ? .027f : .045f) + (i&7)*.004f);
            int v = i*8;
            var streakWidth = width*(foreground ? .64f : 1);
            float distance = Vector3.Distance(p,center);
            float opacity = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.28f,1.75f,distance));
            rainColors[v]=rainColors[v+1]=new Color32(186,216,239,(byte)((foreground ? 98 : 120)*opacity));
            rainColors[v+2]=rainColors[v+3]=new Color32(186,216,239,5);
            rainVertices[v]=p-streakWidth; rainVertices[v+1]=p+streakWidth;
            rainVertices[v+2]=tail-streakWidth; rainVertices[v+3]=tail+streakWidth;
            splashAges[i] = Mathf.Max(0,splashAges[i]-dt);
            float intensity=splashAges[i]/.18f;
            var impact=splashPoints[i];
            float sprayHeight=.028f+Mathf.Sin((1-intensity)*Mathf.PI)*.077f;
            var sprayWidth=width*(.85f+(1-intensity)*2.1f);
            var sprayTop=impact+Vector3.up*sprayHeight;
            rainVertices[v+4]=impact-sprayWidth*.34f;
            rainVertices[v+5]=impact+sprayWidth*.34f;
            rainVertices[v+6]=sprayTop-sprayWidth;
            rainVertices[v+7]=sprayTop+sprayWidth;
            byte splashAlpha=(byte)((fineRain ? 72 : 88)*intensity);
            rainColors[v+4]=rainColors[v+5]=new Color32(197,223,240,splashAlpha);
            rainColors[v+6]=rainColors[v+7]=new Color32(197,223,240,0);
        }
        rainMesh.vertices = rainVertices;
        rainMesh.colors32 = rainColors;
        rainMesh.bounds = new Bounds(center, new Vector3(radius*2+12,Mathf.Max(80,center.y*2+40),radius*2+12));
    }

    Vector2 ResolveMapFocus(Vector3 eye,Vector3 direction)
    {
        eye=MapPoint(eye);
        direction=landRoot.InverseTransformVector(direction).normalized;
        if(direction.y<-.015f)
        {
            float maxRange=Mathf.Min(1800,Mathf.Max(12,Mathf.Max(half.x,half.y)*3));
            float step=Mathf.Clamp(maxRange/180,.3f,5);
            float previous=0;
            for(float t=step;t<=maxRange;t+=step)
            {
                var p=eye+direction*t;
                if(p.y>GroundHeight(p.x,p.z)){previous=t;continue;}
                float left=previous,right=t;
                for(int i=0;i<8;i++)
                {
                    float middle=(left+right)*.5f;
                    var q=eye+direction*middle;
                    if(q.y>GroundHeight(q.x,q.z))left=middle;
                    else right=middle;
                }
                var hit=eye+direction*right;
                return new Vector2(Mathf.Clamp(hit.x,-half.x,half.x),Mathf.Clamp(hit.z,-half.y,half.y));
            }
        }
        return new Vector2(Mathf.Clamp(eye.x,-half.x,half.x),Mathf.Clamp(eye.z,-half.y,half.y));
    }

    void SetMapFocus(Vector2 center)
    {
        var controller=Read(battle,"_camCtrl") as Component;
        if(!controller)return;
        var snap=controller.GetType().GetMethod("SnapTo",new[] { typeof(Vector2) });
        if(snap!=null)snap.Invoke(controller,new object[] { center });
    }

    void ToggleExit()
    {
        if(exitStart>=0)
        {
            exitStart=-1;
            SetMapFocus(previousMapFocus);
            return;
        }
        exitStart=Time.unscaledTime;
        StopSteering();
        exitPosition=view.transform.position;
        exitRotation=view.transform.rotation;
        exitFov=view.fieldOfView;
        var center=source.transform.position;
        previousMapFocus=new Vector2(center.x,center.y);
        var focus=ResolveMapFocus(exitPosition,exitRotation*Vector3.forward);
        SetMapFocus(focus);
        center=source.transform.position;
        exitFocus=DisplayPoint(new Vector3(center.x,0,center.y));
    }
    void RenderTransition()
    {
        if (exitStart >= 0)
        {
            float progress = Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.unscaledTime-exitStart)/OutroTime));

            var destination = exitFocus;
            var overheadRotation = Quaternion.Euler(89.5f,0,0);
            float distanceToMap = Mathf.Min(view.farClipPlane*.75f, source.orthographicSize * MapScale / Mathf.Tan(7*Mathf.Deg2Rad));
            var overheadPosition = destination - overheadRotation*Vector3.forward*distanceToMap;
            view.fieldOfView = Mathf.Lerp(exitFov,14,progress);
            view.transform.SetPositionAndRotation(Vector3.Lerp(exitPosition,overheadPosition,progress),
                Quaternion.Slerp(exitRotation,overheadRotation,progress));
            return;
        }
        float t = Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.unscaledTime-transitionStart)/IntroTime));
        if (t >= 1)
        {
            float lens=followed==null ? 64 : Mathf.Lerp(67,51,firstPersonAim);
            view.fieldOfView=Mathf.Lerp(view.fieldOfView,lens,1-Mathf.Exp(-Time.unscaledDeltaTime*12));
            return;
        }
        float fov = Mathf.Lerp(14,60,t);
        var rotation = Quaternion.Slerp(Quaternion.Euler(89.5f,0,0),view.transform.rotation,t);
        float distance = Mathf.Min(view.farClipPlane*.75f,source.orthographicSize * MapScale / Mathf.Tan(fov*Mathf.Deg2Rad*.5f));
        var overhead = transitionFocus - rotation*Vector3.forward*distance;
        view.fieldOfView = fov;
        view.transform.SetPositionAndRotation(Vector3.Lerp(overhead,flyPosition,t),rotation);
    }
}


















