using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class Battle3D
{
    const int ReflectionLayer=30;
    bool advancedRequested,advancedActive,graphicsPointer;
    Transform advancedRoot;
    Camera reflectionCamera;
    RenderTexture reflectionFrame;
    Material reflectionMaterial,rippleMaterial;
    Mesh glossyMesh,rippleMesh;
    float nextReflection,nextLightScan;
    int lightCursor;
    GUIStyle graphicsButtonStyle,graphicsHintStyle;
    readonly List<Vector4> puddles=new List<Vector4>();
    readonly List<Vector3> glossyPoints=new List<Vector3>();
    readonly List<float> glossyOpacity=new List<float>();
    readonly List<Vector2> glossyUV=new List<Vector2>();
    readonly List<Color32> glossyColors=new List<Color32>();
    readonly List<int> glossyFaces=new List<int>();
    readonly List<Vector3> ripplePoints=new List<Vector3>();
    readonly List<Color32> rippleColors=new List<Color32>();
    readonly List<int> rippleFaces=new List<int>();
    readonly Dictionary<MeshFilter,SurfaceLight> litSurfaces=new Dictionary<MeshFilter,SurfaceLight>();
    readonly Dictionary<Mesh,LightGeometry> lightGeometry=new Dictionary<Mesh,LightGeometry>();
    readonly List<SurfaceLight> lightingQueue=new List<SurfaceLight>();

    sealed class LightGeometry
    {
        public Vector3[] Points,Normals;
        public Color32[] Colors;
        public bool MetalParts;
    }
    sealed class SurfaceLight
    {
        public MeshFilter Filter;
        public Mesh Original,Shaded;
        public LightGeometry Geometry;
        public Color32[] Colors;
        public Vector3 LastPosition,LastEye,LastScale;
        public Quaternion LastRotation;
        public float LastShade=-10,LastStorm;
    }

    void DrawGraphicsButton()
    {
        if(graphicsButtonStyle==null)
        {
            graphicsButtonStyle=new GUIStyle(GUI.skin.button){fontSize=14,fontStyle=FontStyle.Bold};
            graphicsButtonStyle.normal.textColor=graphicsButtonStyle.hover.textColor=Color.white;
            graphicsHintStyle=new GUIStyle(GUI.skin.box){fontSize=12,alignment=TextAnchor.MiddleCenter};
            graphicsHintStyle.normal.textColor=Color.white;
        }
        float width=Mathf.Min(254,Screen.width*.34f);
        var button=new Rect(Screen.width-width-16,16,width,37);
        var old=GUI.backgroundColor;
        GUI.backgroundColor=advancedActive ? new Color(.38f,.72f,.67f) : new Color(.47f,.51f,.57f);
        if(GUI.Button(button, Text("Gelişmiş grafikler  ", "Advanced graphics  ") +
            (advancedRequested ? Text("AÇIK", "ON") : Text("KAPALI", "OFF")), graphicsButtonStyle))
            advancedRequested=!advancedRequested;
        GUI.backgroundColor=old;
        GUI.Box(new Rect(button.x,button.yMax+3,width,25), Text("Ctrl basılı tut: fare   |   F8: değiştir",
            "Hold Ctrl: cursor   |   F8: toggle"), graphicsHintStyle);
        var languages = new Rect(button.x, button.yMax + 32, width, 32);
        DrawPublicationPanel(languages);
        DrawLanguageSelector(new Rect(languages.x + 6, languages.y + 4, languages.width - 12, 24));
    }

    void PrepareWetGround(object world,float[] water,float[] bare)
    {
        puddles.Clear();
        int weather=Convert.ToInt32(Read(Read(battle,"_level"),"weather"));
        if(weather<3 || weather>5)return;
        var occupied=new bool[water.Length];
        foreach(var structure in Read(world,"Structures") as IEnumerable ?? new object[0])
        {
            var footprint=Read(structure,"Footprint");
            if(footprint is Rect)MarkLandscapeArea(occupied,(Rect)footprint,.65f);
        }
        const float step=2.0f;
        for(int z=0;z<Mathf.FloorToInt(half.y*2/step);z++)
        for(int x=0;x<Mathf.FloorToInt(half.x*2/step);x++)
        {
            int seed=x*1637+z*7919;
            var at=new Vector2(-half.x+(x+.18f+Noise01(seed)*.64f)*step,
                -half.y+(z+.18f+Noise01(seed+67)*.64f)*step);
            int index=LandscapeIndex(at);
            if(occupied[index] || water[index]>.025f || Noise01(seed+31)>(bare[index]>.6f ? .48f : .045f))continue;
            float y=GroundHeight(at.x,at.y);
            float slope=Mathf.Abs(GroundHeight(at.x+.8f,at.y)-GroundHeight(at.x-.8f,at.y))+
                Mathf.Abs(GroundHeight(at.x,at.y+.8f)-GroundHeight(at.x,at.y-.8f));
            if(slope>.08f || Mathf.Abs(y)>.10f)continue;
            puddles.Add(new Vector4(at.x,at.y,.35f+Noise01(seed+127)*.74f,.29f+Noise01(seed+263)*.48f));
        }
    }

    void EnableAdvancedGraphics()
    {
        advancedRoot=Joint(root.transform,"Gelişmiş ışık ve ıslak yüzeyler",Vector3.zero);
        reflectionCamera=Joint(advancedRoot,"Yansıma kamerası",Vector3.zero).gameObject.AddComponent<Camera>();
        reflectionCamera.enabled=false;
        reflectionCamera.clearFlags=CameraClearFlags.SolidColor;
        reflectionCamera.backgroundColor=horizonColor;
        reflectionCamera.cullingMask=1<<Layer;
        reflectionCamera.allowHDR=false;
        reflectionCamera.allowMSAA=false;
        var format=SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float) ?
            RenderTextureFormat.RGB111110Float : RenderTextureFormat.Default;
        int width=Mathf.Clamp(frame.width/2,320,640);
        int height=Mathf.Max(180,Mathf.RoundToInt(width/(float)frame.width*frame.height));
        reflectionFrame=new RenderTexture(width,height,24,format){name="Islak yüzey yansıması",filterMode=FilterMode.Bilinear};
        reflectionCamera.targetTexture=reflectionFrame;
        reflectionMaterial=new Material(spriteMaterial);
        reflectionMaterial.mainTexture=reflectionFrame;
        reflectionMaterial.renderQueue=2980;
        glossyMesh=CreateAdvancedMesh("Su ve birikinti yansımaları",reflectionMaterial);
        rippleMaterial=new Material(shader);
        rippleMaterial.SetInt("_Cull",0);
        rippleMaterial.SetInt("_ZWrite",0);
        rippleMaterial.SetInt("_SrcBlend",5);
        rippleMaterial.SetInt("_DstBlend",10);
        rippleMaterial.renderQueue=2985;
        rippleMesh=CreateAdvancedMesh("Yağmurun yüzeydeki halkaları",rippleMaterial);
        EnableColorFinish();
        CreateSurfaceFinish();
        advancedActive=true;
        nextReflection=nextLightScan=0;
        lightCursor=0;
    }

    Mesh CreateAdvancedMesh(string label,Material material)
    {
        var item=Joint(landRoot,label,Vector3.zero);
        item.gameObject.layer=ReflectionLayer;
        var mesh=new Mesh{name=label,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
        mesh.MarkDynamic();
        item.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        item.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
        return mesh;
    }

    void DrawAdvancedGraphics()
    {
        if(!advancedRequested)
        {
            if(advancedActive)ReleaseAdvancedGraphics();
            return;
        }
        try
        {
            if(!advancedActive)EnableAdvancedGraphics();
            view.cullingMask=(1<<Layer)|(1<<ReflectionLayer);
            UpdateColorFinish();
            RefreshRainRoofs();
            UpdateSurfaceLighting();
            UpdateStructureFinish();
            if(Time.unscaledTime>=nextReflection)
            {
                nextReflection=Time.unscaledTime+.10f;
                RenderWetReflection();
                BuildGlossySurfaces();
            }
            DrawSurfaceRipples();
        }
        catch(Exception error)
        {
            Debug.LogWarning("Advanced graphics disabled: "+error);
            advancedRequested=false;
            ReleaseAdvancedGraphics();
            ShowNotice("Gelişmiş görünüm açılamadı. Normal grafiklere dönüldü.",
                "Advanced graphics could not be enabled. Standard graphics restored.", 5);
        }
    }

    void RenderWetReflection()
    {
        if(puddles.Count==0 && !waterMesh)return;
        var mirror=Matrix4x4.identity;
        mirror.m11=-1;
        mirror.m13=.10f;
        reflectionCamera.transform.position=mirror.MultiplyPoint(view.transform.position);
        reflectionCamera.transform.rotation=Quaternion.Euler(-view.transform.eulerAngles.x,view.transform.eulerAngles.y,0);
        reflectionCamera.worldToCameraMatrix=view.worldToCameraMatrix*mirror;
        reflectionCamera.projectionMatrix=view.projectionMatrix;
        var matrix=reflectionCamera.worldToCameraMatrix;
        var normal=matrix.MultiplyVector(Vector3.up).normalized;
        var point=matrix.MultiplyPoint(new Vector3(0,.075f,Offset));
        reflectionCamera.projectionMatrix=reflectionCamera.CalculateObliqueMatrix(
            new Vector4(normal.x,normal.y,normal.z,-Vector3.Dot(point,normal)));
        bool oldCulling=GL.invertCulling;
        bool heldVisible=heldModel && heldModel.gameObject.activeSelf;
        try
        {
            if(heldVisible)heldModel.gameObject.SetActive(false);
            GL.invertCulling=!oldCulling;
            reflectionCamera.Render();
        }
        finally
        {
            GL.invertCulling=oldCulling;
            if(heldVisible && heldModel)heldModel.gameObject.SetActive(true);
        }
    }

    void AddGlossyPoint(Vector3 point,float alpha)
    {
        glossyPoints.Add(point);
        glossyOpacity.Add(alpha);
    }

    void BuildGlossySurfaces()
    {
        glossyPoints.Clear();glossyOpacity.Clear();glossyFaces.Clear();
        var eye=MapPoint(view.transform.position);
        const int sides=14;
        foreach(var puddle in puddles)
        {
            var center=new Vector2(puddle.x,puddle.y);
            if((center-new Vector2(eye.x,eye.z)).sqrMagnitude>62*62)continue;
            int start=glossyPoints.Count;
            float surface=GroundHeight(center.x,center.y)+.052f;
            AddGlossyPoint(new Vector3(center.x,surface,center.y),.76f);
            for(int ring=0;ring<2;ring++)
            for(int i=0;i<sides;i++)
            {
                float angle=i*Mathf.PI*2/sides;
                float radius=(ring==0 ? .77f : 1)*(.81f+.19f*Mathf.Sin(i*2.37f+center.x));
                var edge=center+new Vector2(Mathf.Cos(angle)*puddle.z,Mathf.Sin(angle)*puddle.w)*radius;
                AddGlossyPoint(new Vector3(edge.x,GroundHeight(edge.x,edge.y)+.052f,edge.y),ring==0 ? .70f : 0);
                int a=start+1+ring*sides+i,b=start+1+ring*sides+(i+1)%sides;
                if(ring==0){glossyFaces.Add(start);glossyFaces.Add(a);glossyFaces.Add(b);}
                else AddSkyQuad(glossyFaces,a-sides,b-sides,a,b);
            }
        }
        if(waterMesh)
        {
            int start=glossyPoints.Count;
            for(int i=0;i<waterPoints.Length;i++)
                AddGlossyPoint(waterPoints[i]+Vector3.up*.025f,(1-shoreWeights[i]*.8f)*.34f);
            foreach(int triangle in waterMesh.triangles)glossyFaces.Add(start+triangle);
        }
        glossyMesh.Clear(false);
        UpdateGlossyProjection();
    }

    void UpdateGlossyProjection()
    {
        glossyUV.Clear();glossyColors.Clear();
        for(int i=0;i<glossyPoints.Count;i++)
        {
            var world=DisplayPoint(glossyPoints[i]);
            var uv=reflectionCamera.WorldToViewportPoint(world);
            float ripple=Mathf.Sin(glossyPoints[i].x*7+Time.unscaledTime*1.4f)*
                Mathf.Sin(glossyPoints[i].z*8-Time.unscaledTime*1.1f);
            glossyUV.Add(new Vector2(uv.x+ripple*.0011f,uv.y+ripple*.0007f));
            var angle=(view.transform.position-world).normalized;
            float fresnel=.26f+.74f*Mathf.Pow(1-Mathf.Abs(angle.y),2.5f);
            float alpha=uv.z>0 && uv.x>0 && uv.x<1 && uv.y>0 && uv.y<1 ? glossyOpacity[i]*fresnel : 0;
            glossyColors.Add(new Color(.85f,.92f,.97f,alpha));
        }
        glossyMesh.SetVertices(glossyPoints);
        glossyMesh.SetUVs(0,glossyUV);
        glossyMesh.SetColors(glossyColors);
        glossyMesh.SetTriangles(glossyFaces,0);
    }

    void DrawSurfaceRipples()
    {
        ripplePoints.Clear();rippleColors.Clear();rippleFaces.Clear();
        if(!rainMesh){rippleMesh.Clear(false);return;}
        var eye=MapPoint(view.transform.position);
        int count=0;
        for(int i=0;i<puddles.Count && count<100;i++)
        {
            var p=puddles[i];
            if((new Vector2(p.x-eye.x,p.y-eye.z)).sqrMagnitude>32*32)continue;
            float phase=Mathf.Repeat(Time.unscaledTime*(.84f+Noise01(i)*.40f)+Noise01(i+41)*6,1);
            float radius=(.025f+phase*.39f)*Mathf.Min(p.z,p.w);
            float alpha=Mathf.Sin(phase*Mathf.PI)*.52f;
            if(RainSurface(p.x,p.y)>GroundHeight(p.x,p.y)+.20f)continue;
            int start=ripplePoints.Count;
            for(int side=0;side<18;side++)
            {
                float angle=side*Mathf.PI*2/18;
                var normal=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                for(int edge=0;edge<2;edge++)
                {
                    var at=new Vector2(p.x,p.y)+normal*(radius+edge*.010f);
                    ripplePoints.Add(new Vector3(at.x,GroundHeight(at.x,at.y)+.063f,at.y));
                    rippleColors.Add(new Color(.66f,.81f,.91f,alpha));
                }
                int a=start+side*2,b=start+(side+1)%18*2;
                AddSkyQuad(rippleFaces,a,a+1,b,b+1);
            }
            count++;
        }
        rippleMesh.Clear(false);
        rippleMesh.SetVertices(ripplePoints);
        rippleMesh.SetColors(rippleColors);
        rippleMesh.SetTriangles(rippleFaces,0);
    }

    void ReleaseAdvancedGraphics()
    {
        ReleaseColorFinish();
        ReleaseSurfaceFinish();
        foreach(var item in litSurfaces.Values)RestoreSurface(item);
        litSurfaces.Clear();lightGeometry.Clear();lightingQueue.Clear();surfaceVisibility.Clear();
        if(view)view.cullingMask=1<<Layer;
        if(reflectionCamera)reflectionCamera.targetTexture=null;
        if(reflectionFrame){reflectionFrame.Release();Destroy(reflectionFrame);}
        if(advancedRoot)Destroy(advancedRoot.gameObject);
        if(glossyMesh)DestroyAdvancedMesh(glossyMesh);
        if(rippleMesh)DestroyAdvancedMesh(rippleMesh);
        if(reflectionMaterial)Destroy(reflectionMaterial);
        if(rippleMaterial)Destroy(rippleMaterial);
        reflectionFrame=null;reflectionCamera=null;advancedRoot=null;
        reflectionMaterial=rippleMaterial=null;glossyMesh=rippleMesh=null;
        advancedActive=graphicsPointer=false;
    }

    void DestroyAdvancedMesh(Mesh mesh)
    {
        if(landRoot)
            foreach(var filter in landRoot.GetComponentsInChildren<MeshFilter>())
                if(filter.sharedMesh==mesh)Destroy(filter.gameObject);
        Destroy(mesh);
    }
}
