using System;
using System.Collections;
using UnityEngine;

public sealed partial class Battle3D
{
    bool IsHighGround(object structure)
    {
        return string.Equals(Convert.ToString(Read(Read(structure,"Data"),"id")),"high_ground",StringComparison.OrdinalIgnoreCase);
    }

    void BlendHighGround(object world,Color[] colors,float[] rocks,float[] water,float[] bare,float dx,float dz)
    {
        foreach(var structure in Read(world,"Structures") as IEnumerable ?? new object[0])
        {
            if(!IsHighGround(structure))continue;
            var footprint=Read(structure,"Footprint");
            if(!(footprint is Rect))continue;
            var rect=(Rect)footprint;
            var center=rect.center;
            float rx=Mathf.Max(.85f,rect.width*.76f),rz=Mathf.Max(.85f,rect.height*.76f);
            float rise=Mathf.Clamp(Mathf.Min(rect.width,rect.height)*.29f,.43f,2.0f);
            int x0=Mathf.Clamp(Mathf.FloorToInt((center.x-rx+half.x)/dx),0,gridX);
            int x1=Mathf.Clamp(Mathf.CeilToInt((center.x+rx+half.x)/dx),0,gridX);
            int z0=Mathf.Clamp(Mathf.FloorToInt((center.y-rz+half.y)/dz),0,gridZ);
            int z1=Mathf.Clamp(Mathf.CeilToInt((center.y+rz+half.y)/dz),0,gridZ);
            for(int z=z0;z<=z1;z++)
            for(int x=x0;x<=x1;x++)
            {
                int index=z*(gridX+1)+x;
                if(water[index]>.14f || bare[index]>.64f)continue;
                float wx=x*dx-half.x,wz=z*dz-half.y;
                var p=new Vector2((wx-center.x)/rx,(wz-center.y)/rz);
                float grain=Mathf.PerlinNoise(wx*.69f+90,wz*.72f+117);
                float shape=Mathf.Clamp01((1-p.sqrMagnitude)*(.86f+grain*.23f));
                if(shape<=.001f)continue;
                float height=shape*shape*rise*(1-bare[index]);
                heights[index]=Mathf.Max(heights[index],height);
                rocks[index]=Mathf.Max(rocks[index],shape*(.45f+grain*.43f));
                colors[index]=Color.Lerp(colors[index],new Color(.47f,.43f,.34f),shape*grain*.24f);
            }
        }
    }
}
