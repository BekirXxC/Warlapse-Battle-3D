using System;
using System.Collections.Generic;
using UnityEngine;

// Unity remaps these references to the individual joints when it clones a figure.
public sealed class BattleFigureRig : MonoBehaviour
{
    public Transform Detail, Distant, Eye, Body;
    public Transform[] Legs, Knees, Arms;
    public MeshFilter DistantMesh;
    public Mesh[] Gaits;
}

public sealed partial class Battle3D
{
    sealed class Person
    {
        public BattleFigureRig Rig;
        public object Unit;
        public Outfit Outfit;
        public string Kit;
        public Color Tint;
        public Vector2 Slot, Position, Separation, GroupCenter, DepartureCenter, ManualTravel;
        public float Seed, Heading, Phase, Motion, Stride, Height;
        public float Clearance;
        public Person CellNext;
        public bool Ready, Detached;
    }

    Transform figureLibrary;
    readonly Dictionary<string, BattleFigureRig> figureTemplates = new Dictionary<string, BattleFigureRig>();
    readonly List<Person> soldiers = new List<Person>();
    readonly Dictionary<Vector2Int,Person> soldierCells = new Dictionary<Vector2Int,Person>();
    Person followed;
    float crowdDetailRange=32;
    float lastSeparation;
    System.Reflection.MethodInfo squadSize;

    int FigureCount(object data, object squad)
    {
        int count = Convert.ToInt32(Read(squad,"_full"));
        if (count <= 0)
        {
            if (squadSize == null)
            {
                var type = battleType.Assembly.GetType("Warlapse.Battle.SquadDisplay");
                squadSize = type == null ? null : type.GetMethod("SquadSizeFor");
            }
            if (squadSize != null)
                count = Convert.ToInt32(squadSize.Invoke(null,new[] { data }));
            if (count <= 0) count = Convert.ToInt32(Read(data,"squadSize"));
        }
        return Mathf.Clamp(count,1,30);
    }

    static float Noise01(int seed)
    {
        uint h = unchecked((uint)seed * 747796405u + 2891336453u);
        h = ((h >> ((int)(h >> 28) + 4)) ^ h) * 277803737u;
        return ((h >> 22 ^ h) & 0xffffff) / 16777216f;
    }

    void CreatePeople(Model model, object data, object squad, Color uniform, string role, int era, string id, int seed)
    {
        model.People = new List<Person>();
        bool crew = model.Kind == Kind.Artillery;
        bool mounted = model.Kind == Kind.Horse || model.Kind == Kind.Wolf || model.Kind == Kind.Elephant || model.Kind == Kind.Beast;
        int count = crew ? Mathf.Clamp(FigureCount(data,squad),2,4) : FigureCount(data,squad);
        model.MaxManpower = Mathf.Max(1,Convert.ToSingle(Read(data,"manpower")));
        var figureKind = crew ? Kind.Infantry : model.Kind;
        var kit=SoldierKit(id,crew ? "Crew" : role,era);
        var outfit=OutfitFor(era,id);
        for (int i=0; i<count; i++)
        {
            float rnd=Noise01(seed+i*19);
            float radius=i==0 ? 0 : Mathf.Sqrt(i) * (mounted ? 1.10f : .70f) * Mathf.Lerp(.90f,1.1f,rnd);
            float angle=i*2.3999632f + seed*.012f + (rnd-.5f)*.48f;
            Vector2 slot = new Vector2(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius*1.16f);
            if (crew) slot = new Vector2((i%2==0 ? -1 : 1) * (.67f + rnd*.19f),-.30f-.57f*(i/2)-rnd*.26f);
            var template = GetFigure(figureKind,uniform,crew ? "Crew" : role,era,id,(i+(int)(Noise01(seed)*3))%3);
            var rig = Instantiate(template,model.Root,false);
            rig.name = (mounted ? "Süvari " : crew ? "Topçu " : "Asker ") + (i+1);
            rig.transform.localScale = Vector3.one * Mathf.Lerp(.89f,1.07f,Noise01(seed+i*29+5)) *
                (model.Kind == Kind.Elephant ? 1.25f : crew ? .84f : 1);
            rig.gameObject.SetActive(true);
            model.People.Add(new Person { Rig=rig, Unit=model.Unit, Outfit=outfit, Kit=kit, Tint=uniform,
                Slot=slot, Phase=rnd*Mathf.PI*2, Seed=rnd*191,
                Stride=Mathf.Lerp(.80f,1.20f,Noise01(seed+i*31+8)) });
        }
    }

    BattleFigureRig GetFigure(Kind kind, Color uniform, string role, int era, string id, int variant)
    {
        string kit = SoldierKit(id,role,era);
        var outfit=OutfitFor(era,id);
        string key = kind+"|"+kit+"|"+outfit+"|"+(Color32)uniform+"|"+variant;
        BattleFigureRig rig;
        if (figureTemplates.TryGetValue(key,out rig)) return rig;
        if (!figureLibrary)
        {
            figureLibrary = Joint(landRoot,"Asker model şablonları",Vector3.zero);
            figureLibrary.gameObject.SetActive(false);
        }
        var template = Joint(figureLibrary,"Şablon",Vector3.zero);
        rig = template.gameObject.AddComponent<BattleFigureRig>();
        var detail = rig.Detail = Joint(template,"Yakın model",Vector3.zero);
        var design = new Model { Root=detail };
        var coat = Color.Lerp(uniform,uniform*(.82f+variant*.078f),.8f);
        if (kind == Kind.Infantry)
        {
            Human(detail,coat,kit,outfit,variant,out design.Legs,out design.Knees,out design.Arms);
            design.Eye = Joint(detail,"Göz",new Vector3(0,1.42f,.17f));
        }
        else Mount(design,coat,kit,outfit,kind,variant);
        rig.Eye = design.Eye;
        rig.Body = design.Body;
        rig.Arms = design.Arms;
        rig.Legs = design.Legs;
        rig.Knees = design.Knees;
        var originals = detail.GetComponentsInChildren<MeshRenderer>(true);
        rig.Distant = Joint(template,"Uzak model",Vector3.zero);
        var simplified = new List<MeshRenderer>();
        foreach (var part in originals)
            if (part.transform.localScale.sqrMagnitude >= .014f) simplified.Add(part);
        rig.Gaits=new Mesh[5];
        rig.Gaits[0]=BakeParts(template,simplified,key+"|far");
        for(int i=1;i<rig.Gaits.Length;i++)
        {
            PosePerson(new Person { Rig=rig,Phase=(i-1)*Mathf.PI*.5f+.35f, Motion=.69f });
            rig.Gaits[i]=BakeParts(template,simplified,key+"|far|"+i);
        }
        PosePerson(new Person { Rig=rig });
        rig.DistantMesh=AttachMesh(rig.Distant,rig.Gaits[0]);

        var groups = new List<Transform> { detail };
        if (rig.Body) groups.Add(rig.Body);
        if (rig.Arms != null) groups.AddRange(rig.Arms);
        groups.AddRange(rig.Legs);
        groups.AddRange(rig.Knees);
        var groupParts = new List<MeshRenderer>[groups.Count];
        for(int i=0;i<groups.Count;i++) groupParts[i]=new List<MeshRenderer>();
        foreach (var part in originals)
        {
            for(int i=groups.Count-1;i>=0;i--)
                if (part.transform.IsChildOf(groups[i]))
                { groupParts[i].Add(part); break; }
        }
        for(int i=0;i<groups.Count;i++)
            if (groupParts[i].Count>0) AttachMesh(groups[i],BakeParts(groups[i],groupParts[i],key+"|near|"+i));
        // Strip the temporary design objects before the first clone, without touching its joints.
        foreach (var part in originals)
        {
            part.transform.SetParent(figureLibrary,false);
            Destroy(part.gameObject);
        }
        rig.Distant.gameObject.SetActive(false);
        figureTemplates.Add(key,rig);
        return rig;
    }

    void UpdatePeople(Model model, object unit, Vector2 center, Vector2 velocity)
    {
        var people = model.People;
        if (people == null) return;
        int alive = Mathf.Clamp(Mathf.FloorToInt(people.Count*
            Mathf.Clamp01(Convert.ToSingle(Read(unit,"Manpower"))/model.MaxManpower)+.5f),1,people.Count);
        var group = model.Root;
        float scale = group.localScale.x;
        bool narrow = true.Equals(Read(unit,"InColumn")) || true.Equals(Read(unit,"InThroat")) ||
            true.Equals(Read(unit,"InQueueChannel"));
        float dt = Mathf.Clamp(Time.deltaTime,0,.075f);
        float speed = velocity.magnitude;
        var front = new Vector2(group.forward.x,group.forward.z);
        var side = new Vector2(front.y,-front.x);
        bool crew = model.Kind == Kind.Artillery;
        float heading = group.eulerAngles.y;
        for (int i=0;i<people.Count;i++)
        {
            var person=people[i];
            var rig=person.Rig;
            if (!rig) continue;
            bool present=i<alive;
            if (rig.gameObject.activeSelf!=present) rig.gameObject.SetActive(present);
            if (!present) { person.Ready=person.Detached=false; continue; }
            person.GroupCenter=center;
            if(person.Detached && !(person==followed && commanding) &&
                (center-person.DepartureCenter).sqrMagnitude>.36f)person.Detached=false;
            var slot=person.Slot;
            if (narrow && !crew)
            {
                slot.x*=.32f;
                slot.y=-i*.44f+(slot.y*.17f);
            }
            float time=Time.time;
            float drift=speed>.04f ? .12f : .038f;
            slot.x += Mathf.Sin(time*.92f*person.Stride+person.Seed)*drift;
            slot.y += Mathf.Cos(time*1.33f*person.Stride+person.Seed)*drift;
            Vector2 target=center+(side*slot.x+front*slot.y)*scale -
                velocity*((person.Stride-.8f)*.26f);
            var previous=person.Position;
            if (!person.Ready)
            {
                person.Ready=true;
                person.Position=target;
                previous=target;
                person.Heading=heading;
            }
            else if (!person.Detached && dt>0)
                person.Position=Vector2.MoveTowards(previous,target,
                    Mathf.Min((target-previous).magnitude*(1-Mathf.Exp(-dt*(5.4f+person.Stride*3))),
                        dt*Mathf.Max(3.2f,speed*1.4f)));
            var travel=person.Position-previous;
            float pace=dt>0 ? travel.magnitude/dt : speed;
            float aim=speed>.07f && travel.sqrMagnitude>.000002f ?
                Mathf.Atan2(travel.x,travel.y)*Mathf.Rad2Deg : heading + Mathf.Sin(person.Seed)*12;
            if(!person.Detached)person.Heading=Mathf.LerpAngle(person.Heading,aim,1-Mathf.Exp(-dt*9));
            float px=person.Position.x,pz=person.Position.y;
            person.Clearance=scale*(rig.Legs.Length==4 ? .61f : .27f)*(narrow ? .66f : 1);
            person.Height=GroundHeight(px,pz);
            rig.transform.SetPositionAndRotation(DisplayPoint(new Vector3(px,person.Height+.028f,pz)),
                Quaternion.Euler(0,person.Heading,0));

            bool close = person==followed ||
                (rig.transform.position-view.transform.position).sqrMagnitude < crowdDetailRange*crowdDetailRange;
            bool hidden = person==followed && ((view.transform.position-rig.Eye.position).sqrMagnitude<.85f || followBlendStart<0);
            if (rig.Detail.gameObject.activeSelf!=(close && !hidden)) rig.Detail.gameObject.SetActive(close && !hidden);
            if (rig.Distant.gameObject.activeSelf==close) rig.Distant.gameObject.SetActive(!close);
            float oldMotion=person.Motion;
            if(!(person==followed && steering))
            {
                person.Motion=Mathf.Lerp(person.Motion,Mathf.Clamp01(pace/Mathf.Max(.25f,scale)*.63f),1-Mathf.Exp(-dt*13));
                if(person.Motion<.001f)person.Motion=0;
            }
            person.Phase=Mathf.Repeat(person.Phase+travel.magnitude*MapScale/Mathf.Max(.25f,scale*MapScale)*
                (rig.Legs.Length==4 ? 3.9f : 6.2f)*person.Stride,Mathf.PI*2);
            if (close) { if (person.Motion>0 || oldMotion>0) PosePerson(person); }
            else
            {
                int pose=person.Motion<.20f ? 0 : 1+(Mathf.FloorToInt(person.Phase*(2/Mathf.PI))&3);
                if(rig.DistantMesh.sharedMesh!=rig.Gaits[pose]) rig.DistantMesh.sharedMesh=rig.Gaits[pose];
                rig.Distant.localPosition=Vector3.up*(Mathf.Abs(Mathf.Sin(person.Phase))*.041f*person.Motion);
            }
            soldiers.Add(person);
        }
    }

    static Vector2Int CrowdCell(Vector2 position)
    {
        const float unitsPerCell=2f;
        return new Vector2Int(Mathf.FloorToInt(position.x/unitsPerCell),Mathf.FloorToInt(position.y/unitsPerCell));
    }

    void SeparatePeople()
    {
        float now=Time.time;
        float dt=Mathf.Min(now-lastSeparation,.075f);
        if(dt<.040f)return;
        lastSeparation=now;
        soldierCells.Clear();
        foreach(var person in soldiers)
        {
            if(person.Detached){person.Separation=Vector2.zero;continue;}
            var cell=CrowdCell(person.Position);
            Person previous;
            soldierCells.TryGetValue(cell,out previous);
            person.CellNext=previous;
            soldierCells[cell]=person;
        }
        foreach(var person in soldiers)
        {
            if(person.Detached)continue;
            var cell=CrowdCell(person.Position);
            var push=Vector2.zero;
            for(int z=-1;z<=1;z++)
            for(int x=-1;x<=1;x++)
            {
                Person other;
                soldierCells.TryGetValue(cell+new Vector2Int(x,z),out other);
                for(;other!=null;other=other.CellNext)
                {
                    if(other==person)continue;
                    var delta=person.Position-other.Position;
                    float radius=person.Clearance+other.Clearance;
                    float sq=delta.sqrMagnitude;
                    if(sq>=radius*radius)continue;
                    if(sq<.00001f)
                    {
                        float angle=(person.Seed-other.Seed)*7.31f;
                        delta=new Vector2(Mathf.Sin(angle),Mathf.Cos(angle))*.006f;
                        if(person.Seed<other.Seed)delta.y=-delta.y;
                        sq=delta.sqrMagnitude;
                    }
                    float dist=Mathf.Sqrt(sq);
                    push+=delta*((radius-dist)/dist);
                }
            }
            person.Separation=Vector2.ClampMagnitude(push*(dt*8),dt*3.1f);
        }
        foreach(var person in soldiers)
        {
            if(person.Separation.sqrMagnitude<.0000001f)continue;
            person.Position+=person.Separation;
            var p=person.Position;
            person.Height=GroundHeight(p.x,p.y);
            person.Rig.transform.position=DisplayPoint(new Vector3(p.x,person.Height+.028f,p.y));
        }
    }

    static void PosePerson(Person person)
    {
        var rig=person.Rig;
        bool mounted=rig.Legs.Length==4;
        for(int i=0;i<rig.Legs.Length;i++)
        {
            float cycle=person.Phase+(mounted ? (i==0||i==3 ? 0 : Mathf.PI) : i*Mathf.PI);
            float swing=Mathf.Sin(cycle)*person.Motion;
            rig.Legs[i].localRotation=Quaternion.Euler(swing*(mounted ? 31 : 30),0,0);
            rig.Knees[i].localRotation=Quaternion.Euler(Mathf.Max(0,-Mathf.Cos(cycle))*person.Motion*48,0,0);
            if (rig.Arms!=null && i<rig.Arms.Length && rig.Arms[i])
                rig.Arms[i].localRotation=Quaternion.Euler(-swing*19,0,0);
        }
        if (rig.Body)
        {
            rig.Body.localPosition=new Vector3(0,1.05f+Mathf.Sin(person.Phase*2)*person.Motion*.066f,0);
            rig.Body.localRotation=Quaternion.Euler(Mathf.Sin(person.Phase*2)*person.Motion*5,0,0);
        }
        else if(!mounted)
            rig.Eye.localPosition=new Vector3(0,1.42f+Mathf.Abs(Mathf.Sin(person.Phase))*.035f*person.Motion,.17f);
    }
}
