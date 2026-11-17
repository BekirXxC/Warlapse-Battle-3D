using UnityEngine;

public sealed partial class Battle3D
{
    Texture2D terrainFinish;

    Material BuildTerrainFinish(Vector3[] vertices,Color[] palette,float[] water)
    {
        var textureShader=Shader.Find("Universal Render Pipeline/Unlit");
        if(!textureShader || !textureShader.isSupported)
        {
            TessellateTerrain(palette,water);
            return new Material(Paint(Color.white));
        }
        var material=new Material(textureShader);
        if(!material.HasProperty("_BaseMap"))
        {
            Destroy(material);
            TessellateTerrain(palette,water);
            return new Material(Paint(Color.white));
        }
        int sizeX=Mathf.Clamp(Mathf.NextPowerOfTwo(gridX*3),256,1024);
        int sizeZ=Mathf.Clamp(Mathf.NextPowerOfTwo(gridZ*3),256,1024);
        int columns=gridX+1;
        var pixels=new Color32[sizeX*sizeZ];
        var uv=new Vector2[vertices.Length];
        for(int i=0;i<uv.Length;i++)uv[i]=new Vector2((i%columns)/(float)gridX,(i/columns)/(float)gridZ);
        for(int z=0;z<sizeZ;z++)
        for(int x=0;x<sizeX;x++)
        {
            float u=(x+.5f)/sizeX,v=(z+.5f)/sizeZ;
            pixels[z*sizeX+x]=FineTerrainTone(palette,water,u,v,x,z);
        }
        terrainFinish=new Texture2D(sizeX,sizeZ,TextureFormat.RGBA32,true,false) {
            name="Arazi yüzeyi", wrapMode=TextureWrapMode.Clamp, filterMode=FilterMode.Trilinear, anisoLevel=6 };
        terrainFinish.SetPixels32(pixels);
        terrainFinish.Apply(true,true);
        terrainMesh.uv=uv;
        material.SetTexture("_BaseMap",terrainFinish);
        return material;
    }

    void TessellateTerrain(Color[] palette,float[] water)
    {
        int sx=Mathf.Min(600,gridX*2),sz=Mathf.Min(600,gridZ*2);
        int columns=sx+1;
        var vertices=new Vector3[columns*(sz+1)];
        var colors=new Color32[vertices.Length];
        var triangles=new int[sx*sz*6];
        int next=0;
        for(int z=0;z<=sz;z++)
        for(int x=0;x<=sx;x++)
        {
            int i=z*columns+x;
            float u=x/(float)sx,v=z/(float)sz;
            float wx=(u*2-1)*half.x,wz=(v*2-1)*half.y;
            vertices[i]=new Vector3(wx,GroundHeight(wx,wz),wz);
            colors[i]=FineTerrainTone(palette,water,u,v,x,z);
            if(x==sx || z==sz)continue;
            triangles[next++]=i;
            triangles[next++]=i+columns;
            triangles[next++]=i+1;
            triangles[next++]=i+1;
            triangles[next++]=i+columns;
            triangles[next++]=i+columns+1;
        }
        terrainMesh.Clear(false);
        terrainMesh.vertices=vertices;
        terrainMesh.colors32=colors;
        terrainMesh.triangles=triangles;
    }

    Color32 FineTerrainTone(Color[] palette,float[] water,float u,float v,int x,int z)
    {
        int columns=gridX+1;
        float gx=u*gridX,gz=v*gridZ;
        int ix=Mathf.Min(gridX-1,(int)gx),iz=Mathf.Min(gridZ-1,(int)gz);
        float sx=gx-ix,sz=gz-iz;
        int at=iz*columns+ix;
        Color color=Color.Lerp(Color.Lerp(palette[at],palette[at+1],sx),
            Color.Lerp(palette[at+columns],palette[at+columns+1],sx),sz);
        Color edgeTone=color;
        float coast=Mathf.Lerp(Mathf.Lerp(water[at],water[at+1],sx),
            Mathf.Lerp(water[at+columns],water[at+columns+1],sx),sz);
        float wx=(u*2-1)*half.x,wz=(v*2-1)*half.y;
        float speckle=Noise01(unchecked(x*70277+z*3871));
        float clumps=Mathf.PerlinNoise(wx*1.19f+27,wz*1.19f+52);
        float bed=Mathf.PerlinNoise(wx*.25f+38,wz*.27f+19);
        float shade=.75f+.24f*clumps+.12f*bed+.16f*speckle;
        if(coast>.45f)shade=.89f+.12f*bed+.065f*speckle;
        else if(GreenGround(color))
        {
            float loam=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.48f,.82f,clumps*bed*1.43f));
            color=Color.Lerp(color,new Color(color.r*.94f,color.g*.75f,color.b*.80f),loam*.48f);
            float turf=Mathf.PerlinNoise(wx*6.4f+wz*1.1f+11,wz*8.9f+6);
            float mottling=.77f+turf*.27f+speckle*.18f;
            color=Color.Lerp(color,new Color(color.r*.81f,color.g*1.03f,color.b*.72f),.26f);
            shade=(shade*.64f+.36f)*mottling;
        }
        float borderDistance=Mathf.Min(half.x-Mathf.Abs(wx),half.y-Mathf.Abs(wz));
        color=Color.Lerp(edgeTone,color*shade,RimBlend(0,3.7f,borderDistance));
        color.a=1;
        return color;
    }
}
