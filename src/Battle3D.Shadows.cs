using System.Collections.Generic;
using UnityEngine;

public sealed partial class Battle3D
{
    Mesh contactMesh;
    Material contactMaterial;
    readonly List<Vector3> contactVertices = new List<Vector3>();
    readonly List<Color32> contactColors = new List<Color32>();
    readonly List<int> contactIndices = new List<int>();
    readonly List<Vector2> castPoints = new List<Vector2>(12);
    readonly List<Vector2> castHull = new List<Vector2>(16);
    Vector2 shadowRay;
    float nextShadowFrame;
    byte castOpacity;
    bool silhouetteUnavailable;
    static readonly System.Comparison<Vector2> ShadowPointOrder=CompareShadowPoints;

    void BuildContactShadows()
    {
        contactMesh = new Mesh { name = "Zemin gölgeleri", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        contactMesh.MarkDynamic();
        contactMaterial = new Material(shader);
        contactMaterial.SetInt("_Cull",0);
        contactMaterial.SetInt("_ZWrite",0);
        contactMaterial.SetInt("_SrcBlend",5);
        contactMaterial.SetInt("_DstBlend",10);
        contactMaterial.renderQueue = 2990;
        var host = new GameObject("Birlik ve yapı gölgeleri");
        host.layer = Layer;
        host.transform.SetParent(landRoot,false);
        host.AddComponent<MeshFilter>().sharedMesh = contactMesh;
        host.AddComponent<MeshRenderer>().sharedMaterial = contactMaterial;
    }

    void DrawContactShadows()
    {
        if(!silhouetteUnavailable)
        {
            try{UpdateSilhouetteShadows();}
            catch(System.Exception error)
            {
                ReleaseSilhouetteShadows();
                silhouetteUnavailable=true;
                Debug.LogWarning("Soft shadow fallback: "+error.Message);
            }
        }
        if(Time.unscaledTime<nextShadowFrame)return;
        nextShadowFrame=Time.unscaledTime+.033f;
        contactVertices.Clear();
        contactColors.Clear();
        contactIndices.Clear();
        var camera=MapPoint(view.transform.position);
        var cameraGround=new Vector2(camera.x,camera.z);
        float lightLength=rainMesh ? .61f : 1;
        shadowRay=-new Vector2(sunDirection.x,sunDirection.z)*(lightLength/(sunDirection.y*MapScale));
        castOpacity=rainMesh ? (byte)29 : (byte)78;
        const float individualShadowRange=72/MapScale;
        foreach (var item in models)
        {
            var actor = item.Key;
            if (!actor || !actor.gameObject.activeInHierarchy) continue;
            var model = item.Value;
            if (!model.Root.gameObject.activeSelf || model.Relief) continue;
            if (model.Kind == Kind.Aircraft) continue;
            var p = model.Root.localPosition;
            if (Mathf.Abs(p.x)>half.x || Mathf.Abs(p.z)>half.y) continue;
            var center=new Vector2(p.x,p.z);
            var scale = model.Root.localScale;
            bool unit = actor.GetType() == unitType;
            if (model.People != null)
            {
                foreach (var person in model.People)
                {
                    if (!person.Ready || !person.Rig.gameObject.activeSelf) continue;
                    var at=person.Position;
                    if(Mathf.Abs(at.x)>half.x || Mathf.Abs(at.y)>half.y) continue;
                    float distanceSquared=(at-cameraGround).sqrMagnitude;
                    if(distanceSquared>individualShadowRange*individualShadowRange)
                        continue;
                    bool mounted=person.Rig.Legs.Length==4;
                    float fade=1-RimBlend(individualShadowRange*.72f,individualShadowRange,
                        Mathf.Sqrt(distanceSquared));
                    AddContact(at,scale.x*(mounted ? .34f : .25f),
                        scale.x*(mounted ? .63f : .29f),person.Heading*Mathf.Deg2Rad,(byte)(58*fade));
                    AddFigureShadow(person,scale.x,mounted,(byte)(castOpacity*fade));
                }
                if (model.Kind!=Kind.Artillery) continue;
            }
            bool field=model.Field!=Fieldwork.None;
            if(model.Field==Fieldwork.Wire)continue;
            float width = unit ? scale.x*(model.Kind==Kind.Horse ? .55f : .46f) : scale.x*(field ? .42f : .67f);
            float length = unit ? scale.z*(model.Kind==Kind.Horse ? 1.28f : .67f) : scale.z*(field ? .51f : .67f);
            Vector3 facing = model.Root.forward;
            var axis=new Vector2(facing.x,facing.z).normalized;
            var offset = shadowRay*.025f*Mathf.Min(scale.y,5);
            float angle = Mathf.Atan2(facing.x,facing.z);
            AddContact(center+offset,width*.77f,length*.77f,angle,unit ? (byte)63 : field ? (byte)48 : (byte)75);
            if(silhouetteReady || (center-cameraGround).sqrMagnitude>140*140 || model.Field==Fieldwork.Trench)continue;
            if(model.Gate)
            {
                var side=new Vector2(axis.y,-axis.x);
                for(int i=-1;i<=1;i+=2)
                    AddBoxShadow(center+side*(i*.39f*scale.x),axis,scale.x*.16f,scale.z*.45f,p.y,scale.y*1.28f,castOpacity);
                continue;
            }
            float height=scale.y*(model.Keep ? 1.24f : model.Fort ? 1.05f : .86f);
            AddBoxShadow(center,axis,width*.69f,length*.78f,p.y,height,castOpacity);
        }
        foreach(var item in sprites)
        {
            var copy=item.Value;
            if(!copy.Prop || !copy.Prop.gameObject.activeSelf || !copy.Tree)continue;
            var p=copy.View.transform.localPosition;
            var center=new Vector2(p.x,p.z);
            if((center-cameraGround).sqrMagnitude>86*86)continue;
            var scale=copy.View.transform.localScale;
            AddContact(center,scale.x*.24f,scale.z*.28f,0,64);
            if(!silhouetteReady)AddTreeShadow(center,p.y,scale);
        }
        if(!silhouetteReady)foreach(var link in curtainRoofs)
        {
            if(!link.Key)continue;
            foreach(var section in link.Value)AddWallShadow(section);
        }
        contactMesh.Clear(false);
        if (contactVertices.Count==0) return;
        contactMesh.SetVertices(contactVertices);
        contactMesh.SetColors(contactColors);
        contactMesh.SetTriangles(contactIndices,0);
    }

    void AddFigureShadow(Person person,float scale,bool mounted,byte opacity)
    {
        var foot=person.Position;
        float width=scale*(mounted ? .33f : .21f);
        var side=new Vector2(-shadowRay.y,shadowRay.x).normalized*width;
        var end=ProjectShadow(foot,person.Height+person.Rig.transform.lossyScale.y*(mounted ? 1.9f : 1.42f));
        castPoints.Clear();
        castPoints.Add(foot-side);
        castPoints.Add(foot+side);
        castPoints.Add(end+side*.59f);
        castPoints.Add(end+shadowRay.normalized*width*.45f);
        castPoints.Add(end-side*.59f);
        AddCastHull(width*.55f+.05f,opacity);
    }

    void AddBoxShadow(Vector2 center,Vector2 axis,float width,float length,float baseY,float height,byte opacity)
    {
        var side=new Vector2(axis.y,-axis.x);
        castPoints.Clear();
        for(int x=-1;x<=1;x+=2)
        for(int z=-1;z<=1;z+=2)
        {
            var foot=center+side*(x*width)+axis*(z*length);
            castPoints.Add(foot);
            castPoints.Add(ProjectShadow(foot,baseY+height));
        }
        AddCastHull(Mathf.Clamp(height*.16f,.10f,.95f),opacity);
    }

    void AddTreeShadow(Vector2 center,float baseY,Vector3 scale)
    {
        castPoints.Clear();
        castPoints.Add(center);
        var crown=ProjectShadow(center,baseY+scale.y*.76f);
        for(int i=0;i<8;i++)
        {
            float theta=i*Mathf.PI*.25f;
            castPoints.Add(crown+new Vector2(Mathf.Cos(theta)*scale.x*.31f,Mathf.Sin(theta)*scale.z*.38f));
        }
        AddCastHull(Mathf.Clamp(scale.y*.20f,.18f,1.2f),(byte)(castOpacity*.72f));
    }

    Vector2 ProjectShadow(Vector2 origin,float height)
    {
        var shadow=origin;
        for(int i=0;i<2;i++)
            shadow=origin+shadowRay*Mathf.Max(0,height-GroundHeight(shadow.x,shadow.y));
        return shadow;
    }

    static int CompareShadowPoints(Vector2 a,Vector2 b)
    {
        int byX=a.x.CompareTo(b.x);
        return byX!=0 ? byX : a.y.CompareTo(b.y);
    }

    static float ShadowTurn(Vector2 a,Vector2 b,Vector2 c)
    {
        var ab=b-a;
        var ac=c-a;
        return ab.x*ac.y-ab.y*ac.x;
    }

    void AddHullCorner(Vector2 corner,int fixedCount)
    {
        while(castHull.Count>=fixedCount+2 &&
            ShadowTurn(castHull[castHull.Count-2],castHull[castHull.Count-1],corner)<=.00001f)
            castHull.RemoveAt(castHull.Count-1);
        castHull.Add(corner);
    }

    void AddCastHull(float feather,byte opacity)
    {
        castPoints.Sort(ShadowPointOrder);
        castHull.Clear();
        foreach(var point in castPoints)AddHullCorner(point,0);
        int lower=castHull.Count-1;
        for(int i=castPoints.Count-2;i>=0;i--)AddHullCorner(castPoints[i],lower);
        castHull.RemoveAt(castHull.Count-1);
        if(castHull.Count<3)return;
        var center=Vector2.zero;
        foreach(var point in castHull)center+=point;
        center/=castHull.Count;
        int start=contactVertices.Count;
        AddWallShadowPoint(center,opacity);
        for(int i=0;i<castHull.Count;i++)
        {
            var edge=castHull[i];
            AddWallShadowPoint(edge,opacity);
            AddWallShadowPoint(edge+(edge-center).normalized*feather,0);
            int inner=start+1+i*2, next=start+1+(i+1)%castHull.Count*2;
            contactIndices.Add(start); contactIndices.Add(inner); contactIndices.Add(next);
            AddSkyQuad(contactIndices,inner,inner+1,next,next+1);
        }
    }

    void AddWallShadow(RainRoof section)
    {
        var center=new Vector2(section.Center.x,section.Center.z);
        var axis=section.Axis;
        var side=new Vector2(axis.y,-axis.x);
        var light=shadowRay.normalized;
        if(Vector2.Dot(side,light)<0)side=-side;
        float height=Mathf.Max(.2f,section.Top-section.Center.y);
        float cast=Mathf.Clamp(height*shadowRay.magnitude,.20f,7f);
        int divisions=Mathf.Clamp(Mathf.CeilToInt(section.Extents.y*2/.63f),2,18);
        int start=contactVertices.Count;
        for(int i=0;i<=divisions;i++)
        {
            var foot=center+axis*((i/(float)divisions*2-1)*section.Extents.y);
            var edge=foot+side*section.Extents.x*.94f;
            AddWallShadowPoint(edge,rainMesh ? (byte)70 : (byte)113);
            AddWallShadowPoint(edge+light*(cast*.48f),rainMesh ? (byte)31 : (byte)58);
            AddWallShadowPoint(edge+light*cast,0);
            if(i==divisions)continue;
            int k=start+i*3;
            for(int band=0;band<2;band++)
            {
                int v=k+band;
                contactIndices.Add(v);contactIndices.Add(v+1);contactIndices.Add(v+3);
                contactIndices.Add(v+1);contactIndices.Add(v+4);contactIndices.Add(v+3);
            }
        }
    }

    void AddWallShadowPoint(Vector2 point,byte alpha)
    {
        float x=Mathf.Clamp(point.x,-half.x,half.x);
        float z=Mathf.Clamp(point.y,-half.y,half.y);
        contactVertices.Add(new Vector3(x,GroundHeight(x,z)+.049f,z));
        contactColors.Add(new Color32(21,26,32,alpha));
    }
    void AddContact(Vector2 center,float width,float length,float angle,byte alpha)
    {
        const int sides=10;
        int start=contactVertices.Count;
        float c=Mathf.Cos(angle), s=Mathf.Sin(angle);
        contactVertices.Add(new Vector3(center.x,GroundHeight(center.x,center.y)+.043f,center.y));
        contactColors.Add(new Color32(20,26,32,alpha));
        for(int i=0;i<sides;i++)
        {
            float theta=i*Mathf.PI*2/sides;
            float x=Mathf.Cos(theta)*width,z=Mathf.Sin(theta)*length;
            var edge = new Vector2(Mathf.Clamp(center.x+x*c+z*s,-half.x,half.x),
                Mathf.Clamp(center.y+z*c-x*s,-half.y,half.y));
            contactVertices.Add(new Vector3(edge.x,GroundHeight(edge.x,edge.y)+.043f,edge.y));
            contactColors.Add(new Color32(20,26,32,0));
            contactIndices.Add(start);
            contactIndices.Add(start+1+i);
            contactIndices.Add(start+1+(i+1)%sides);
        }
    }

    void ReleaseContactShadows()
    {
        ReleaseSilhouetteShadows();
        silhouetteUnavailable=false;
        if (contactMesh) Destroy(contactMesh);
        if (contactMaterial) Destroy(contactMaterial);
        contactMesh = null;
        contactMaterial = null;
        nextShadowFrame=0;
        contactVertices.Clear();
        contactColors.Clear();
        contactIndices.Clear();
    }
}

