using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class Battle3D
{
    const int ShadowResolution=1024;
    RenderTexture shadowMask,shadowSoftMask;
    CommandBuffer shadowCommands;
    Material shadowCasterMaterial,shadowReceiverMaterial,shadowSoftMaterial;
    Mesh shadowReceiver;
    Transform shadowReceiverObject;
    Vector2 shadowCenter;
    float shadowRadius,nextSilhouette,nextCasterScan;
    bool silhouetteReady;
    readonly List<MeshFilter> shadowCasters=new List<MeshFilter>(500);
    readonly List<MeshFilter> shadowScratch=new List<MeshFilter>();
    static readonly int ShadowHalf=Shader.PropertyToID("_WarlapseShadowHalf");
    static readonly int ShadowQuarter=Shader.PropertyToID("_WarlapseShadowQuarter");

    void CreateSilhouetteShadows()
    {
        shadowMask=new RenderTexture(ShadowResolution,ShadowResolution,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear)
        {
            name="Güneşin 3D siluetleri", filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp
        };
        int softResolution=ShadowResolution/(rainMesh ? 8 : 4);
        shadowSoftMask=new RenderTexture(softResolution,softResolution,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear)
        {
            name="Güneşin gölge yumuşatması",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp
        };
        shadowCommands=new CommandBuffer{name="Warlapse 3D: güneş maskesi"};
        shadowCasterMaterial=new Material(shader){color=Color.black};
        shadowCasterMaterial.SetInt("_Cull",0);
        shadowCasterMaterial.SetInt("_ZWrite",0);
        shadowCasterMaterial.SetInt("_ZTest",(int)CompareFunction.Always);
        shadowCasterMaterial.SetInt("_SrcBlend",(int)BlendMode.One);
        shadowCasterMaterial.SetInt("_DstBlend",(int)BlendMode.Zero);
        shadowReceiverMaterial=new Material(spriteMaterial){mainTexture=shadowMask,renderQueue=2964};
        shadowSoftMaterial=new Material(spriteMaterial){mainTexture=shadowSoftMask,renderQueue=2965};
        shadowReceiverMaterial.color=new Color(1,1,1,rainMesh ? .055f : .22f);
        shadowSoftMaterial.color=new Color(1,1,1,rainMesh ? .19f : .23f);
        shadowReceiverObject=Joint(landRoot,"Güneş ve temas gölgeleri",Vector3.zero);
        shadowReceiverObject.gameObject.layer=Layer;
        shadowReceiver=new Mesh{name="Araziye oturan yumuşak gölge",indexFormat=IndexFormat.UInt32};
        shadowReceiverObject.gameObject.AddComponent<MeshFilter>().sharedMesh=shadowReceiver;
        shadowReceiverObject.gameObject.AddComponent<MeshRenderer>().sharedMaterials=new[]{shadowReceiverMaterial,shadowSoftMaterial};
        shadowReceiverObject.gameObject.SetActive(false);
        nextSilhouette=nextCasterScan=0;
    }

    void UpdateSilhouetteShadows()
    {
        if(!shadowMask)CreateSilhouetteShadows();
        float now=Time.unscaledTime;
        if(now<nextSilhouette)return;
        nextSilhouette=now+.105f;
        var eye=MapPoint(view.transform.position);
        var focus=new Vector2(eye.x,eye.z);
        float radius=48;
        bool relocate=!silhouetteReady || radius!=shadowRadius || (focus-shadowCenter).sqrMagnitude>5.0f*5.0f;
        if(relocate)
        {
            shadowCenter=new Vector2(Mathf.Round(focus.x*.5f)*2,Mathf.Round(focus.y*.5f)*2);
            shadowRadius=radius;
        }
        var target=DisplayPoint(new Vector3(shadowCenter.x,0,shadowCenter.y));
        var sunCamera=Matrix4x4.Scale(new Vector3(1,1,-1))*
            Matrix4x4.TRS(target+sunDirection*128,Quaternion.LookRotation(-sunDirection,Vector3.up),Vector3.one).inverse;
        if(relocate)BuildShadowReceiver(sunCamera,radius);
        if(relocate || now>=nextCasterScan)
        {
            RefreshShadowCasters(radius);
            nextCasterScan=now+.35f;
        }
        var projection=GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-radius,radius,-radius,radius,.05f,240),true);
        shadowCommands.Clear();
        shadowCommands.SetRenderTarget(shadowMask);
        shadowCommands.SetViewport(new Rect(0,0,ShadowResolution,ShadowResolution));
        shadowCommands.ClearRenderTarget(false,true,Color.clear);
        shadowCommands.SetViewProjectionMatrices(sunCamera,projection);
        foreach(var filter in shadowCasters)
        {
            if(!filter || !filter.gameObject.activeInHierarchy || !filter.sharedMesh)continue;
            shadowCommands.DrawMesh(filter.sharedMesh,filter.transform.localToWorldMatrix,shadowCasterMaterial,0,0);
        }
        shadowCommands.GetTemporaryRT(ShadowHalf,ShadowResolution/2,ShadowResolution/2,0,FilterMode.Bilinear,
            RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        shadowCommands.Blit(shadowMask,ShadowHalf);
        if(rainMesh)
        {
            shadowCommands.GetTemporaryRT(ShadowQuarter,ShadowResolution/4,ShadowResolution/4,0,FilterMode.Bilinear,
                RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            shadowCommands.Blit(ShadowHalf,ShadowQuarter);
            shadowCommands.Blit(ShadowQuarter,shadowSoftMask);
            shadowCommands.ReleaseTemporaryRT(ShadowQuarter);
        }
        else shadowCommands.Blit(ShadowHalf,shadowSoftMask);
        shadowCommands.ReleaseTemporaryRT(ShadowHalf);
        var previous=RenderTexture.active;
        try{Graphics.ExecuteCommandBuffer(shadowCommands);}
        finally{RenderTexture.active=previous;}
        silhouetteReady=true;
        shadowReceiverObject.gameObject.SetActive(true);
    }

    void RefreshShadowCasters(float radius)
    {
        shadowCasters.Clear();
        float range=radius/MapScale*1.70f;
        foreach(var entry in models)
        {
            var model=entry.Value;
            if(!entry.Key || !model.Root || !model.Root.gameObject.activeInHierarchy || model.Kind==Kind.Aircraft || model.Relief)continue;
            var p=model.Root.localPosition;
            if((new Vector2(p.x,p.z)-shadowCenter).sqrMagnitude>range*range)continue;
            if(model.People!=null)
            {
                if(model.Kind==Kind.Artillery)AddShadowCaster(model.Root.GetComponent<MeshFilter>());
                continue;
            }
            AddCasterChildren(model.Root);
        }
        foreach(var entry in sprites)
        {
            var prop=entry.Value.Prop;
            if(!prop || !prop.gameObject.activeInHierarchy)continue;
            var p=MapPoint(prop.position);
            if((new Vector2(p.x,p.z)-shadowCenter).sqrMagnitude<range*range)AddCasterChildren(prop);
        }
        foreach(var entry in curtainRoofs)
            if(entry.Key)AddCasterChildren(entry.Key);
    }

    void AddCasterChildren(Transform rootTransform)
    {
        if(shadowCasters.Count>=700)return;
        rootTransform.GetComponentsInChildren(false,shadowScratch);
        foreach(var filter in shadowScratch)AddShadowCaster(filter);
    }

    void AddShadowCaster(MeshFilter filter)
    {
        if(!filter || filter.gameObject.layer!=Layer || !filter.sharedMesh || filter.sharedMesh.vertexCount==0 ||
            filter.sharedMesh.name=="Pencere ışığı" || shadowCasters.Count>=700)return;
        var renderer=filter.GetComponent<MeshRenderer>();
        if(renderer && renderer.enabled)shadowCasters.Add(filter);
    }

    void BuildShadowReceiver(Matrix4x4 sunCamera,float radius)
    {
        var vertices=new List<Vector3>();
        var uv=new List<Vector2>();
        var colors=new List<Color32>();
        var faces=new List<int>();
        float coverage=radius/MapScale*1.75f;
        float left=Mathf.Max(-half.x,shadowCenter.x-coverage),right=Mathf.Min(half.x,shadowCenter.x+coverage);
        float bottom=Mathf.Max(-half.y,shadowCenter.y-coverage),top=Mathf.Min(half.y,shadowCenter.y+coverage);
        shadowReceiver.Clear(false);
        if(right<=left || top<=bottom)return;
        int cols=Mathf.Clamp(Mathf.CeilToInt((right-left)/.55f),2,126);
        int rows=Mathf.Clamp(Mathf.CeilToInt((top-bottom)/.55f),2,126);
        for(int z=0;z<=rows;z++)
        for(int x=0;x<=cols;x++)
        {
            float px=Mathf.Lerp(left,right,x/(float)cols),pz=Mathf.Lerp(bottom,top,z/(float)rows);
            var point=new Vector3(px,GroundHeight(px,pz)+.039f,pz);
            var sunPoint=sunCamera.MultiplyPoint3x4(DisplayPoint(point));
            var tex=new Vector2(.5f+sunPoint.x/(radius*2),.5f+sunPoint.y/(radius*2));
            float edge=1-Mathf.Max(Mathf.Abs(tex.x-.5f),Mathf.Abs(tex.y-.5f))*2;
            float border=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.015f,.21f,edge));
            vertices.Add(point);
            uv.Add(tex);
            colors.Add(new Color(1,1,1,border));
            if(x==cols || z==rows)continue;
            int at=z*(cols+1)+x;
            AddSkyQuad(faces,at,at+cols+1,at+1,at+cols+2);
        }
        shadowReceiver.SetVertices(vertices);
        shadowReceiver.SetUVs(0,uv);
        shadowReceiver.SetColors(colors);
        shadowReceiver.subMeshCount=2;
        shadowReceiver.SetTriangles(faces,0);
        shadowReceiver.SetTriangles(faces,1,false);
    }

    void ReleaseSilhouetteShadows()
    {
        if(shadowCommands!=null)shadowCommands.Release();
        if(shadowMask){shadowMask.Release();Destroy(shadowMask);}
        if(shadowSoftMask){shadowSoftMask.Release();Destroy(shadowSoftMask);}
        if(shadowCasterMaterial)Destroy(shadowCasterMaterial);
        if(shadowReceiverMaterial)Destroy(shadowReceiverMaterial);
        if(shadowSoftMaterial)Destroy(shadowSoftMaterial);
        if(shadowReceiver)Destroy(shadowReceiver);
        if(shadowReceiverObject)Destroy(shadowReceiverObject.gameObject);
        shadowMask=shadowSoftMask=null;shadowCommands=null;shadowCasterMaterial=shadowReceiverMaterial=shadowSoftMaterial=null;
        shadowReceiver=null;shadowReceiverObject=null;silhouetteReady=false;
        shadowCasters.Clear();shadowScratch.Clear();
    }
}
