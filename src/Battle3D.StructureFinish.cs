using System.Collections.Generic;
using UnityEngine;

public sealed partial class Battle3D
{
    sealed class StructureFinish
    {
        public Transform Surface,Windows;
        public Mesh SurfaceMesh,WindowMesh;
        public int LitSide=-1;
    }

    readonly Dictionary<Model,StructureFinish> structureFinishes=new Dictionary<Model,StructureFinish>();
    Texture2D masonryTexture,concreteTexture;
    Material masonryMaterial,concreteMaterial,windowLightMaterial,bounceMaterial;
    Mesh bounceMesh;
    float nextStructureFinish;

    void CreateSurfaceFinish()
    {
        masonryTexture=MakeFinishTexture(true);
        concreteTexture=MakeFinishTexture(false);
        masonryMaterial=new Material(spriteMaterial){mainTexture=masonryTexture,renderQueue=2968};
        concreteMaterial=new Material(spriteMaterial){mainTexture=concreteTexture,renderQueue=2968};
        windowLightMaterial=new Material(shader);
        windowLightMaterial.SetInt("_Cull",0);
        windowLightMaterial.SetInt("_ZWrite",0);
        windowLightMaterial.SetInt("_SrcBlend",1);
        windowLightMaterial.SetInt("_DstBlend",1);
        windowLightMaterial.renderQueue=2969;
        bounceMaterial=new Material(windowLightMaterial){renderQueue=2966};
        bounceMaterial.SetInt("_SrcBlend",5);
        bounceMesh=CreateAdvancedMesh("Pencereden zemine vuran ışık",bounceMaterial);
        nextStructureFinish=0;
    }

    Texture2D MakeFinishTexture(bool blocks)
    {
        const int size=256;
        var tex=new Texture2D(size,size,TextureFormat.RGBA32,true,false)
        {name=blocks ? "Taş ve sıva ayrıntısı" : "Beton gözenekleri", wrapMode=TextureWrapMode.Repeat,
            filterMode=FilterMode.Trilinear,anisoLevel=8};
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)
        for(int x=0;x<size;x++)
        {
            float u=(x+.5f)/size,v=(y+.5f)/size;
            float fine=Noise01(x*43+y*9571);
            float chips=Mathf.PerlinNoise(u*41+3,v*42+11);
            float grain=Mathf.PerlinNoise(u*9+32,v*12+3);
            float seams=0,lift=0;
            if(blocks)
            {
                float row=v*8;
                int band=Mathf.FloorToInt(row);
                float col=u*4+band*.5f;
                float t=Mathf.Repeat(row,1),s=Mathf.Repeat(col,1);
                float edge=Mathf.Min(Mathf.Min(s,1-s)*1.3f,Mathf.Min(t,1-t));
                seams=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.012f,.057f,edge+(chips-.5f)*.075f));
                lift=(Noise01(band*23+(Mathf.FloorToInt(col)&3)*87)-.5f)*.15f;
                lift+=Mathf.Max(0,1-t*10)*.10f;
            }
            float pores=Mathf.Max(0,.43f-chips)*.47f+Mathf.Max(0,.19f-fine)*.35f;
            float shade=.50f+lift+(grain-.5f)*.26f+(fine-.5f)*.19f-pores-seams*.26f;
            pixels[y*size+x]=new Color(shade*1.06f,shade,shade*.88f,.09f+seams*.28f+pores*.64f);
        }
        tex.SetPixels32(pixels);
        tex.Apply(true,true);
        return tex;
    }

    void UpdateStructureFinish()
    {
        if(Time.unscaledTime<nextStructureFinish)return;
        nextStructureFinish=Time.unscaledTime+.45f;
        var eye=view.transform.position;
        var remove=new List<Model>();
        foreach(var entry in structureFinishes)
            if(!entry.Key.Root || !entry.Key.Root.gameObject.activeInHierarchy || (entry.Key.Root.position-eye).sqrMagnitude>70*70)
            { DestroyStructureFinish(entry.Value); remove.Add(entry.Key); }
        foreach(var model in remove)structureFinishes.Remove(model);
        var closest=new List<Model>();
        foreach(var entry in models)
        {
            var model=entry.Value;
            if(!entry.Key || !model.Root || !model.Root.gameObject.activeInHierarchy || model.VisualId==null ||
                (!model.Settlement && !model.VisualId.Contains("bunker")) || structureFinishes.ContainsKey(model) ||
                (model.Root.position-eye).sqrMagnitude>56*56)continue;
            closest.Add(model);
        }
        closest.Sort((a,b)=>(a.Root.position-eye).sqrMagnitude.CompareTo((b.Root.position-eye).sqrMagnitude));
        foreach(var model in closest)
        {
            if(structureFinishes.Count>=65)break;
            structureFinishes.Add(model,BuildStructureFinish(model));
        }
        BuildGroundBounce(eye);
    }

    StructureFinish BuildStructureFinish(Model model)
    {
        var v=new List<Vector3>(); var uv=new List<Vector2>(); var f=new List<int>();
        var result=new StructureFinish();
        string id=model.VisualId;
        bool bunker=!model.Settlement;
        bool tall=id.Contains("apartment") || id.Contains("factory") || id.Contains("industrial");
        float top=bunker ? .503f : tall ? 1.17f : .86f;
        for(int side=0;side<4;side++)
        {
            bool narrow=(side&1)!=0;
            float z=bunker ? (narrow ? .496f : .486f) : (narrow ? .451f : .426f);
            var rotation=Quaternion.Euler(0,90*side,0);
            float x=bunker ? (narrow ? .484f : .494f) : (narrow ? .424f : .449f);
            float baseY=bunker ? .017f : .142f;
            AddFinishQuad(v,uv,f,rotation,-x,x,baseY,top,z+.0015f,
                (narrow ? model.Root.lossyScale.z : model.Root.lossyScale.x)*x*.93f,
                model.Root.lossyScale.y*(top-baseY)*.68f);
        }
        result.Surface=FinishObject(model.Root,"Yüzey dokusu",v,uv,f,bunker ? concreteMaterial : masonryMaterial,ReflectionLayer);
        result.SurfaceMesh=result.Surface.GetComponent<MeshFilter>().sharedMesh;
        if(!bunker && rainMesh)
        {
            var p=model.Root.localPosition;
            int seed=Mathf.RoundToInt(p.x*13)*981+Mathf.RoundToInt(p.z*11)*777;
            if(Noise01(seed)>.26f)
            {
                v.Clear();uv.Clear();f.Clear();
                result.LitSide=Mathf.FloorToInt(Noise01(seed+38)*4);
                for(int i=0;i<3;i++)
                {
                    int side=(result.LitSide+i)%4;
                    bool narrow=(side&1)!=0;
                    float z=(narrow ? .451f : .426f)+.057f;
                    float x=(narrow ? .193f : .248f)*(i==1 ? -1 : 1);
                    var rotation=Quaternion.Euler(0,90*side,0);
                    AddFinishQuad(v,uv,f,rotation,x-.062f,x+.062f,.318f,.436f,z,1,1);
                }
                result.Windows=FinishObject(model.Root,"Pencere ışığı",v,uv,f,windowLightMaterial,Layer);
                var colors=new Color[v.Count];
                for(int i=0;i<colors.Length;i++)
                    colors[i]=new Color(.96f,.57f,.24f)*(.48f+.27f*Noise01(seed+i/4*9)+((i&3)>1 ? .15f : 0));
                result.WindowMesh=result.Windows.GetComponent<MeshFilter>().sharedMesh;
                result.WindowMesh.colors=colors;
            }
        }
        return result;
    }

    static void AddFinishQuad(List<Vector3> v,List<Vector2> uv,List<int> f,Quaternion q,
        float x0,float x1,float y0,float y1,float z,float repeatX,float repeatY)
    {
        int start=v.Count;
        v.Add(q*new Vector3(x0,y0,z));v.Add(q*new Vector3(x1,y0,z));
        v.Add(q*new Vector3(x0,y1,z));v.Add(q*new Vector3(x1,y1,z));
        uv.Add(Vector2.zero);uv.Add(new Vector2(repeatX,0));uv.Add(new Vector2(0,repeatY));uv.Add(new Vector2(repeatX,repeatY));
        AddSkyQuad(f,start,start+1,start+2,start+3);
    }

    Transform FinishObject(Transform parent,string name,List<Vector3> v,List<Vector2> uv,List<int> f,Material material,int layer)
    {
        var item=Joint(parent,name,Vector3.zero);
        item.gameObject.layer=layer;
        var mesh=new Mesh{name=name};
        mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(f,0);
        var white=new Color32[v.Count];
        for(int i=0;i<white.Length;i++)white[i]=new Color32(255,255,255,255);
        mesh.colors32=white;
        item.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        item.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
        return item;
    }

    void BuildGroundBounce(Vector3 eye)
    {
        bounceMesh.Clear(false);
        if(!rainMesh)return;
        var v=new List<Vector3>(); var c=new List<Color32>(); var f=new List<int>();
        foreach(var entry in structureFinishes)
        {
            var model=entry.Key;
            if(entry.Value.LitSide<0 || !model.Root || (model.Root.position-eye).sqrMagnitude>34*34)continue;
            const int sides=16;
            var angle=Quaternion.Euler(0,entry.Value.LitSide*90,0);
            var p=MapPoint(model.Root.TransformPoint(angle*new Vector3(.19f,0,.76f)));
            p.y=GroundHeight(p.x,p.z)+.045f;
            int start=v.Count;
            v.Add(p); c.Add(new Color32(223,144,79,44));
            for(int i=0;i<sides;i++)
            {
                float theta=i*Mathf.PI*2/sides;
                var at=p+angle*new Vector3(Mathf.Cos(theta)*1.45f,0,Mathf.Sin(theta)*1.15f);
                at.y=GroundHeight(at.x,at.z)+.045f;
                v.Add(at);c.Add(new Color32(223,144,79,0));
                f.Add(start);f.Add(start+1+i);f.Add(start+1+(i+1)%sides);
            }
        }
        bounceMesh.SetVertices(v);bounceMesh.SetColors(c);bounceMesh.SetTriangles(f,0);
    }

    void DestroyStructureFinish(StructureFinish finish)
    {
        if(finish.Surface){finish.Surface.gameObject.SetActive(false);Destroy(finish.Surface.gameObject);}
        if(finish.Windows){finish.Windows.gameObject.SetActive(false);Destroy(finish.Windows.gameObject);}
        if(finish.SurfaceMesh)Destroy(finish.SurfaceMesh);
        if(finish.WindowMesh)Destroy(finish.WindowMesh);
    }

    void ReleaseSurfaceFinish()
    {
        foreach(var finish in structureFinishes.Values)DestroyStructureFinish(finish);
        structureFinishes.Clear();
        if(bounceMesh)DestroyAdvancedMesh(bounceMesh);
        if(masonryTexture)Destroy(masonryTexture);
        if(concreteTexture)Destroy(concreteTexture);
        if(masonryMaterial)Destroy(masonryMaterial);
        if(concreteMaterial)Destroy(concreteMaterial);
        if(windowLightMaterial)Destroy(windowLightMaterial);
        if(bounceMaterial)Destroy(bounceMaterial);
        bounceMesh=null;masonryTexture=concreteTexture=null;
        masonryMaterial=concreteMaterial=windowLightMaterial=bounceMaterial=null;
    }
}
