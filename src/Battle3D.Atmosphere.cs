using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class Battle3D
{
    Vector3 sunDirection;
    float sunContrast, cloudCover;
    Color horizonColor, zenithColor;
    Texture2D sunGlare;
    float glareVisibility,nextGlareCheck,glareAlpha;
    Transform skyDome, highClouds, lowClouds;
    readonly List<Mesh> skyMeshes = new List<Mesh>();
    readonly List<Material> skyMaterials = new List<Material>();

    void ConfigureAtmosphere()
    {
        int weather=Convert.ToInt32(Read(Read(battle,"_level"),"weather"));
        bool overcast=weather>=3 && weather<=5;
        bool foggy=weather==2;
        sunDirection=new Vector3(-.62f,.43f,-.65f).normalized;
        sunContrast=overcast ? .24f : foggy ? .30f : .54f;
        cloudCover=weather==5 ? .92f : overcast ? .76f : foggy ? .62f : .26f;
        horizonColor=overcast ? new Color(.42f,.49f,.55f) : new Color(.77f,.84f,.88f);
        zenithColor=overcast ? new Color(.15f,.21f,.29f) : new Color(.20f,.44f,.71f);
        view.backgroundColor=horizonColor;
    }

    void BuildSky()
    {
        skyDome=Joint(root.transform,"3D atmosfer",Vector3.zero);
        BuildSkyLayer("Gökyüzü ve güneş ışığı",1480,-90,52,112,1000,SkyColor);
        if(cloudCover<.50f) { BuildSun(); BuildSunGlare(); }
        highClouds=BuildSkyLayer("Yüksek bulutlar",1210,-1,32,112,1002,n=>CloudColor(n,true));
        lowClouds=BuildSkyLayer("Alçak bulutlar",965,-2,42,128,1003,n=>CloudColor(n,false));
    }

    Color SkyColor(Vector3 direction)
    {
        float height=Mathf.Max(0,direction.y);
        var color=Color.Lerp(horizonColor,zenithColor,Mathf.Pow(height,.64f));
        float glow=Mathf.Pow(Mathf.Clamp01(Vector3.Dot(direction,sunDirection)),10);
        color=Color.Lerp(color,new Color(1,.85f,.62f),glow*(1-cloudCover)*.64f);
        if(direction.y<0)
            color=Color.Lerp(color,Color.Lerp(horizonColor,new Color(.34f,.42f,.47f),.72f),
                Mathf.SmoothStep(0,1,-direction.y*2.9f));
        return color;
    }

    Color CloudColor(Vector3 direction,bool high)
    {
        float height=Mathf.Max(.13f,direction.y);
        float spread=(high ? 4.6f : 3.5f)/(.66f+height);
        float x=direction.x*spread+(high ? 36 : 12);
        float z=direction.z*spread+21+direction.y*2;
        float broad=Mathf.PerlinNoise(x,z);
        float detail=Mathf.PerlinNoise(x*2.24f+8,z*2.19f)*.26f+
            Mathf.PerlinNoise(x*5.2f,z*4.8f+12)*.12f;
        float shape=broad*.62f+detail;
        float threshold=Mathf.Lerp(.66f,.37f,cloudCover)+(high ? .035f : -.03f);
        float opacity=Mathf.SmoothStep(0,1,Mathf.InverseLerp(threshold,threshold+.19f,shape));
        float horizon=Mathf.SmoothStep(0,1,Mathf.Clamp01(direction.y*6.5f));
        float raised=Mathf.PerlinNoise(x+sunDirection.x*.19f,z+sunDirection.z*.19f);
        float brightness=Mathf.Clamp01(.53f+(broad-raised)*5.4f+(high ? .16f : 0));
        var dark=Color.Lerp(new Color(.65f,.72f,.80f),new Color(.23f,.29f,.37f),cloudCover);
        var lit=Color.Lerp(new Color(1,.98f,.93f),new Color(.51f,.57f,.63f),cloudCover);
        var color=Color.Lerp(horizonColor,Color.Lerp(dark,lit,brightness),horizon);
        color.a=opacity*horizon*(high ? .44f : .88f);
        return color;
    }

    void BuildSun()
    {
        var vertices=new List<Vector3>{sunDirection*1380};
        var colors=new List<Color>{new Color(1,.97f,.81f,1)};
        var faces=new List<int>();
        var horizontal=Vector3.Cross(sunDirection,Vector3.up).normalized;
        var vertical=Vector3.Cross(horizontal,sunDirection).normalized;
        const int sides=36;
        float[] radii={11,16,46,142};
        float[] opacity={1,.93f,.32f,0};
        for(int ring=0;ring<radii.Length;ring++)
        for(int i=0;i<sides;i++)
        {
            float angle=i*Mathf.PI*2/sides;
            vertices.Add(vertices[0]+(horizontal*Mathf.Cos(angle)+vertical*Mathf.Sin(angle))*radii[ring]);
            colors.Add(new Color(1,.88f,.64f,opacity[ring]));
            int point=1+ring*sides+i, next=1+ring*sides+(i+1)%sides;
            if(ring==0) { faces.Add(0); faces.Add(point); faces.Add(next); }
            else AddSkyQuad(faces,point-sides,next-sides,point,next);
        }
        CreateSkyMesh("Güneş diski",1004,vertices,colors,faces);
    }

    Transform BuildSkyLayer(string label,float radius,float floor,int rings,int sides,int queue,Func<Vector3,Color> tint)
    {
        var vertices=new List<Vector3>();
        var colors=new List<Color>();
        var faces=new List<int>();
        for(int ring=0;ring<=rings;ring++)
        {
            float tilt=Mathf.Lerp(floor,90,ring/(float)rings)*Mathf.Deg2Rad;
            for(int i=0;i<=sides;i++)
            {
                float angle=(i%sides)*Mathf.PI*2/sides;
                var direction=new Vector3(Mathf.Cos(angle)*Mathf.Cos(tilt),Mathf.Sin(tilt),Mathf.Sin(angle)*Mathf.Cos(tilt));
                vertices.Add(direction*radius);
                colors.Add(tint(direction));
                if(ring==rings || i==sides)continue;
                int a=ring*(sides+1)+i, b=a+sides+1;
                AddSkyQuad(faces,a,a+1,b,b+1);
            }
        }
        return CreateSkyMesh(label,queue,vertices,colors,faces);
    }

    static void AddSkyQuad(List<int> faces,int a,int b,int c,int d)
    {
        faces.Add(a); faces.Add(b); faces.Add(c);
        faces.Add(b); faces.Add(d); faces.Add(c);
    }

    Transform CreateSkyMesh(string label,int queue,List<Vector3> vertices,List<Color> colors,List<int> faces)
    {
        var mesh=new Mesh{name=label};
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(faces,0);
        var material=new Material(shader);
        material.SetInt("_Cull",0);
        material.SetInt("_ZWrite",0);
        material.SetInt("_SrcBlend",queue==1000 ? 1 : 5);
        material.SetInt("_DstBlend",queue==1000 ? 0 : 10);
        material.renderQueue=queue;
        var part=Joint(skyDome,label,Vector3.zero);
        part.gameObject.layer=Layer;
        part.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        part.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
        skyMeshes.Add(mesh);
        skyMaterials.Add(material);
        return part;
    }

    void DrawAtmosphere()
    {
        if(!skyDome)return;
        skyDome.position=view.transform.position;
        float time=Time.unscaledTime-transitionStart;
        if(sunGlare)
        {
            float target=0;
            if(Vector3.Dot(view.transform.forward,sunDirection)>.75f)
            {
                if(Time.unscaledTime>=nextGlareCheck)
                {
                    nextGlareCheck=Time.unscaledTime+.08f;
                    RefreshRainRoofs();
                    var start=MapPoint(view.transform.position);
                    var direction=landRoot.InverseTransformVector(sunDirection);
                    glareVisibility=1;
                    for(float distance=.4f;distance<80;distance+=.65f)
                    {
                        var point=start+direction*distance;
                        if(RainSurface(point.x,point.z)<point.y)continue;
                        glareVisibility=0;
                        break;
                    }
                }
                target=glareVisibility;
            }
            else nextGlareCheck=0;
            glareAlpha=Mathf.Lerp(glareAlpha,target,1-Mathf.Exp(-Time.unscaledDeltaTime*13));
        }
        highClouds.localRotation=Quaternion.Euler(0,time*.045f,0);
        lowClouds.localRotation=Quaternion.Euler(0,time*.12f,0);
    }

    void BuildSunGlare()
    {
        const int size=96;
        sunGlare=new Texture2D(size,size,TextureFormat.RGBA32,false)
        {
            name="Güneş kamaşması", wrapMode=TextureWrapMode.Clamp, filterMode=FilterMode.Bilinear
        };
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)
        for(int x=0;x<size;x++)
        {
            float u=2f*x/(size-1)-1, v=2f*y/(size-1)-1;
            float radius=Mathf.Sqrt(u*u+v*v);
            float alpha=Mathf.Pow(Mathf.Max(0,1-radius),2.8f);
            pixels[y*size+x]=new Color(1,.89f,.64f,alpha);
        }
        sunGlare.SetPixels32(pixels);
        sunGlare.Apply(false,true);
    }

    void DrawSunGlare(float fade)
    {
        if(!sunGlare || glareAlpha<.003f || Event.current.type!=EventType.Repaint)return;
        var viewport=view.WorldToViewportPoint(view.transform.position+sunDirection*100);
        if(viewport.z<=0)return;
        float center=RimBlend(.76f,.99f,Vector3.Dot(view.transform.forward,sunDirection));
        if(center<=0)return;
        float size=Screen.height*1.53f;
        var previous=GUI.color;
        GUI.color=new Color(1,1,1,fade*glareAlpha*center*.63f);
        GUI.DrawTexture(new Rect(viewport.x*Screen.width-size*.5f,(1-viewport.y)*Screen.height-size*.5f,size,size),sunGlare);
        GUI.color=previous;
    }

    void ReleaseAtmosphere()
    {
        if(sunGlare)Destroy(sunGlare);
        sunGlare=null;
        glareAlpha=glareVisibility=nextGlareCheck=0;
        foreach(var mesh in skyMeshes)Destroy(mesh);
        foreach(var material in skyMaterials)Destroy(material);
        skyMeshes.Clear();
        skyMaterials.Clear();
        skyDome=highClouds=lowClouds=null;
    }
}
