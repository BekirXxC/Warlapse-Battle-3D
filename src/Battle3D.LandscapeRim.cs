using System.Collections.Generic;
using UnityEngine;

public sealed partial class Battle3D
{
    void BuildLandscapeRim(Vector3[] ground,Color[] palette,float[] water,
        out Vector3[] rimVertices,out int[] rimFaces,out float[] rimWater)
    {
        var border=new List<int>((gridX+gridZ)*2);
        int columns=gridX+1;
        for(int x=0;x<gridX;x++)border.Add(x);
        for(int z=0;z<gridZ;z++)border.Add(z*columns+gridX);
        for(int x=gridX;x>0;x--)border.Add(gridZ*columns+x);
        for(int z=gridZ;z>0;z--)border.Add(z*columns);
        int sides=border.Count;
        float[] distances={0,.65f,1.7f,3.5f,6,9.5f,14,20,27,35,44,54,66,81,98,119,146,178,220};
        var vertices=new List<Vector3>(sides*distances.Length);
        var colors=new List<Color32>(vertices.Capacity);
        var faces=new List<int>(sides*(distances.Length-1)*6);
        rimWater=new float[vertices.Capacity];
        var broadWater=new float[sides];
        float hillStart=Mathf.Max(11,Mathf.Max(half.x,half.y)*.32f+1);
        int waterWindow=Mathf.Clamp(Mathf.RoundToInt(sides/48f),4,18);
        for(int i=0;i<sides;i++)
        {
            for(int k=-waterWindow;k<=waterWindow;k++)
                broadWater[i]+=water[border[(i+k+sides)%sides]];
            broadWater[i]/=waterWindow*2+1;
        }

        for(int ring=0;ring<distances.Length;ring++)
        for(int i=0;i<sides;i++)
        {
            int at=border[i];
            var edge=ground[at];
            float radius=distances[ring];
            float x=edge.x*(1+radius/half.x), z=edge.z*(1+radius/half.y);
            float smooth=RimBlend(1,16,radius);
            float wet=Mathf.Lerp(water[at],broadWater[i],smooth);
            rimWater[ring*sides+i]=wet;
            float land=1-RimBlend(.27f,.79f,wet);
            float rolling=Mathf.PerlinNoise(x*.087f+8,z*.084f+20);
            float hills=Mathf.PerlinNoise(x*.028f+28,z*.026f+9);
            float mountain=RimBlend(.43f,.78f,Mathf.PerlinNoise(x*.015f+15,z*.018f+42));
            float rocky=RimBlend(.34f,.85f,mountain);
            float foothills=RimBlend(hillStart,hillStart+23,radius)*(rolling*2.5f+hills*4.0f);
            float ridge=RimBlend(hillStart+6,hillStart+50,radius)*mountain*(14+rolling*12);
            float y=Mathf.Lerp(edge.y,(foothills+ridge)*land-.24f*(1-land),RimBlend(.35f,13,radius));
            vertices.Add(new Vector3(x,y,z));
            var meadow=Color.Lerp(new Color(.32f,.43f,.24f),new Color(.38f,.47f,.30f),rolling);
            var scree=Color.Lerp(new Color(.43f,.43f,.37f),new Color(.55f,.52f,.46f),rolling);
            var dryGround=Color.Lerp(meadow,scree,rocky*RimBlend(12,44,radius));
            var openWater=Color.Lerp(new Color(.26f,.43f,.52f),new Color(.29f,.47f,.55f),rolling);
            var tint=Color.Lerp(openWater,dryGround,land);
            var near=palette[at];
            tint=Color.Lerp(near,tint,RimBlend(0,27,radius))*terrainBaseTint;
            tint=Color.Lerp(tint,horizonColor,RimBlend(12,208,radius)*.84f);
            tint.a=1;
            colors.Add(tint);
            if(ring==distances.Length-1)continue;
            int a=ring*sides+i, b=ring*sides+(i+1)%sides;
            AddSkyQuad(faces,a,b,a+sides,b+sides);
        }
        ShadeRim(vertices,colors,border.Count,distances);
        AddLandscapeMesh("Ufuk arazisi ve dağ sırtları",vertices,colors,faces,Paint(Color.white));
        AddRimGrass(vertices,colors,rimWater,sides);
        rimVertices=vertices.ToArray();
        rimFaces=faces.ToArray();
    }

    void AddRimGrass(List<Vector3> ground,List<Color32> palette,float[] wet,int sides)
    {
        var vertices=new List<Vector3>();
        var colors=new List<Color32>();
        var faces=new List<int>();
        for(int ring=0;ring<5;ring++)
        for(int i=0;i<sides;i++)
        {
            int a=ring*sides+i, b=ring*sides+(i+1)%sides, c=a+sides, d=b+sides;
            if(Mathf.Max(Mathf.Max(wet[a],wet[b]),Mathf.Max(wet[c],wet[d]))>.24f)continue;
            Color tint=palette[a];
            if(!GreenGround(tint))continue;
            int seed=(a+23)*11471;
            if(Noise01(seed+21)>.94f-ring*.16f)continue;
            float u=.12f+Noise01(seed+37)*.76f, v=.16f+Noise01(seed+51)*.67f;
            var point=u+v<1 ? ground[a]+(ground[b]-ground[a])*u+(ground[c]-ground[a])*v :
                ground[d]+(ground[c]-ground[d])*(1-u)+(ground[b]-ground[d])*(1-v);
            AddGrassTuft(vertices,colors,faces,new Vector2(point.x,point.z),tint,seed,
                .68f-ring*.105f,.43f,wet[a]>.09f,point.y);
        }
        AddLandscapeMesh("Çimenli etekler",vertices,colors,faces,groundDetailMaterial);
    }

    void ShadeRim(List<Vector3> vertices,List<Color32> colors,int sides,float[] distances)
    {
        for(int ring=1;ring<distances.Length-1;ring++)
        for(int i=0;i<sides;i++)
        {
            int at=ring*sides+i;
            var tangent=vertices[ring*sides+(i+1)%sides]-vertices[ring*sides+(i+sides-1)%sides];
            var outward=vertices[at+sides]-vertices[at-sides];
            var normal=Vector3.Cross(tangent,outward).normalized;
            float contrast=RimBlend(.7f,14,distances[ring])*(1-RimBlend(52,200,distances[ring]));
            float light=1+contrast*sunContrast*(Vector3.Dot(normal,sunDirection)-sunDirection.y)*1.43f;
            Color tint=colors[at];
            tint*=light;
            tint.a=1;
            colors[at]=tint;
        }
    }

    static float RimBlend(float start,float end,float value)
    {
        return Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,end,value));
    }
}
