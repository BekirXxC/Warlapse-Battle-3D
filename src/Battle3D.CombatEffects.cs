using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public sealed partial class Battle3D
{
    struct BloodParticle
    {
        public Vector3 Position, Velocity;
        public float Size, Life;
    }

    struct BloodMark
    {
        public Vector2 Position;
        public float Radius;
        public int Seed;
    }

    const int BloodMarkLimit=380;
    const int BloodParticleLimit=240;
    Component bloodSource;
    Mesh bloodMesh;
    Material bloodMaterial;
    int bloodSeed, nextBloodMark;
    readonly List<BloodParticle> bloodParticles=new List<BloodParticle>(BloodParticleLimit);
    readonly List<BloodMark> bloodMarks=new List<BloodMark>(BloodMarkLimit);
    readonly List<Vector3> bloodVertices=new List<Vector3>();
    readonly List<Color32> bloodColors=new List<Color32>();
    readonly List<int> bloodTriangles=new List<int>();

    // Called by the native scar manager at the point where it would draw a 2D effect.
    public static bool RouteBloodDrop(Component source,Vector2 position,float min,float max)
    {
        var mod=instance;
        if(!mod || !source || mod.bloodSource!=source)return false;
        float size=UnityEngine.Random.Range(min,max);
        UnityEngine.Random.Range(0f,360f);
        if(mod.bloodMesh)mod.AddBloodMark(position,size*.48f);
        return true;
    }

    public static bool RouteBloodBurst(Component source,Vector2 position,float size)
    {
        var mod=instance;
        if(!mod || !source || mod.bloodSource!=source)return false;
        UnityEngine.Random.Range(0f,360f);
        if(mod.bloodMesh)mod.AddBloodBurst(position,size);
        return true;
    }

    void SetupCombatEffects(Component scarManager)
    {
        if(!scarManager)return;
        if(bloodSource!=scarManager)
        {
            bloodSource=scarManager;
            bloodMarks.Clear();
            nextBloodMark=bloodSeed=0;
            var droplets=Read(scarManager,"_droplets") as IEnumerable;
            if(droplets!=null)
                foreach(var item in droplets)
                {
                    var sprite=item as SpriteRenderer;
                    if(!sprite)continue;
                    var position=sprite.transform.position;
                    if(sprite.gameObject.activeInHierarchy)
                        AddBloodMark(new Vector2(position.x,position.y),sprite.bounds.extents.x);
                    sprite.forceRenderingOff=true;
                }
            var spurts=Read(scarManager,"_spurts") as IEnumerable;
            if(spurts!=null)
                foreach(var entry in spurts)
                {
                    var sprite=Read(entry,"Sr") as SpriteRenderer;
                    if(sprite)sprite.forceRenderingOff=true;
                }
        }
        bloodMesh=new Mesh{name="3D vuruş izleri"};
        bloodMesh.MarkDynamic();
        bloodMaterial=CreateWorldEffectMaterial(2980);
        var holder=Joint(landRoot,"3D vuruş parçacıkları",Vector3.zero);
        holder.gameObject.layer=Layer;
        holder.gameObject.AddComponent<MeshFilter>().sharedMesh=bloodMesh;
        holder.gameObject.AddComponent<MeshRenderer>().sharedMaterial=bloodMaterial;
    }

    Material CreateWorldEffectMaterial(int queue)
    {
        var material=new Material(shader);
        material.SetInt("_Cull",0);
        material.SetInt("_ZWrite",0);
        material.SetInt("_SrcBlend",5);
        material.SetInt("_DstBlend",10);
        material.renderQueue=queue;
        return material;
    }

    void AddBloodMark(Vector2 position,float radius)
    {
        if(Mathf.Abs(position.x)>half.x || Mathf.Abs(position.y)>half.y)return;
        var mark=new BloodMark{Position=position,Radius=Mathf.Clamp(radius,.022f,.16f),Seed=++bloodSeed};
        if(bloodMarks.Count<BloodMarkLimit)bloodMarks.Add(mark);
        else
        {
            bloodMarks[nextBloodMark]=mark;
            nextBloodMark=(nextBloodMark+1)%BloodMarkLimit;
        }
    }

    void AddBloodBurst(Vector2 position,float size)
    {
        if(bloodParticles.Count>=BloodParticleLimit)return;
        var eye=MapPoint(view.transform.position);
        if((position-new Vector2(eye.x,eye.z)).sqrMagnitude>42*42)return;
        float height=GroundHeight(position.x,position.y)+.74f;
        Vector2 direction=Vector2.right;
        float nearest=.90f*.90f;
        foreach(var person in soldiers)
        {
            float distance=(person.Position-position).sqrMagnitude;
            if(distance>=nearest)continue;
            nearest=distance;
            height=MapPoint(person.Rig.Eye.position).y-.24f;
            var hit=Read(person.Unit,"LastHitDir");
            if(hit is Vector2 && ((Vector2)hit).sqrMagnitude>.04f)direction=((Vector2)hit).normalized;
        }
        int count=Mathf.Clamp(Mathf.RoundToInt(5+size*6),5,10);
        for(int i=0;i<count && bloodParticles.Count<BloodParticleLimit;i++)
        {
            int seed=++bloodSeed*53;
            float spread=(Noise01(seed)-.5f)*1.5f;
            Vector2 flight=direction*(.55f+Noise01(seed+1)*1.7f)+new Vector2(-direction.y,direction.x)*spread;
            bloodParticles.Add(new BloodParticle{
                Position=new Vector3(position.x,height,position.y),
                Velocity=new Vector3(flight.x,.70f+Noise01(seed+2)*1.85f,flight.y),
                Size=.013f+Noise01(seed+3)*.026f,Life=.85f
            });
        }
    }

    void DrawCombatEffects()
    {
        if(!bloodMesh)return;
        bloodVertices.Clear(); bloodColors.Clear(); bloodTriangles.Clear();
        var eye=MapPoint(view.transform.position);
        var groundEye=new Vector2(eye.x,eye.z);
        float dt=Mathf.Min(.05f,Time.deltaTime);
        var right=landRoot.InverseTransformVector(view.transform.right).normalized;
        var forward=landRoot.InverseTransformVector(view.transform.forward);
        for(int i=bloodParticles.Count-1;i>=0;i--)
        {
            var particle=bloodParticles[i];
            particle.Velocity.y-=9.81f*dt;
            particle.Position+=particle.Velocity*dt;
            particle.Life-=dt;
            float ground=GroundHeight(particle.Position.x,particle.Position.z);
            if(particle.Position.y<=ground || particle.Life<=0)
            {
                if(particle.Position.y<=ground)
                    AddBloodMark(new Vector2(particle.Position.x,particle.Position.z),particle.Size*1.5f);
                bloodParticles[i]=bloodParticles[bloodParticles.Count-1];
                bloodParticles.RemoveAt(bloodParticles.Count-1);
                continue;
            }
            bloodParticles[i]=particle;
            var along=particle.Velocity.normalized*particle.Size;
            var across=Vector3.Cross(along,forward).normalized;
            if(across.sqrMagnitude<.01f)across=right;
            AddGroundTriangle(bloodVertices,bloodColors,bloodTriangles,particle.Position-across*particle.Size,
                particle.Position+across*particle.Size,particle.Position+along*2.0f,new Color32(122,20,16,226));
        }

        foreach(var mark in bloodMarks)
        {
            if((mark.Position-groundEye).sqrMagnitude>36*36)continue;
            int center=bloodVertices.Count;
            bloodVertices.Add(new Vector3(mark.Position.x,GroundHeight(mark.Position.x,mark.Position.y)+.036f,mark.Position.y));
            bloodColors.Add(new Color32(84,12,10,195));
            const int sides=7;
            for(int side=0;side<sides;side++)
            {
                float angle=side*Mathf.PI*2/sides;
                float radius=mark.Radius*Mathf.Lerp(.65f,1.45f,Noise01(mark.Seed*19+side));
                var point=mark.Position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                bloodVertices.Add(new Vector3(point.x,GroundHeight(point.x,point.y)+.035f,point.y));
                bloodColors.Add(new Color32(65,12,9,20));
                bloodTriangles.Add(center);
                bloodTriangles.Add(center+1+side);
                bloodTriangles.Add(center+1+(side+1)%sides);
            }
        }
        bloodMesh.Clear(false);
        bloodMesh.SetVertices(bloodVertices);
        bloodMesh.SetColors(bloodColors);
        bloodMesh.SetTriangles(bloodTriangles,0);
    }

    void ReleaseCombatEffects()
    {
        if(bloodMesh)Destroy(bloodMesh);
        if(bloodMaterial)Destroy(bloodMaterial);
        bloodMesh=null;
        bloodMaterial=null;
        bloodParticles.Clear();
    }

    void ShowSingleUnitIcons()
    {
        if(!battle || Flag("ReplayActive"))return;
        var display=battleType.Assembly.GetType("Warlapse.Battle.SquadDisplay");
        var mode=display==null ? null : display.GetField("_mode",BindingFlags.Static|BindingFlags.NonPublic);
        if(mode!=null)
            mode.SetValue(null,Enum.Parse(mode.FieldType,"Off"));
    }
}
