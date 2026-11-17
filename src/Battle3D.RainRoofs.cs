using System.Collections.Generic;
using UnityEngine;

public sealed partial class Battle3D
{
    struct RainRoof
    {
        public Vector3 Center;
        public Vector2 Axis, Extents;
        public float Top;
    }

    readonly Dictionary<Vector2Int,List<RainRoof>> rainRoofs=new Dictionary<Vector2Int,List<RainRoof>>();
    readonly Dictionary<Transform,List<RainRoof>> curtainRoofs=new Dictionary<Transform,List<RainRoof>>();
    float nextRoofUpdate;
    const float RoofCell=4;

    void AddRainRoof(RainRoof roof)
    {
        float rx=Mathf.Abs(roof.Axis.x)*roof.Extents.y+Mathf.Abs(roof.Axis.y)*roof.Extents.x;
        float rz=Mathf.Abs(roof.Axis.y)*roof.Extents.y+Mathf.Abs(roof.Axis.x)*roof.Extents.x;
        for(int z=Mathf.FloorToInt((roof.Center.z-rz)/RoofCell);z<=Mathf.FloorToInt((roof.Center.z+rz)/RoofCell);z++)
        for(int x=Mathf.FloorToInt((roof.Center.x-rx)/RoofCell);x<=Mathf.FloorToInt((roof.Center.x+rx)/RoofCell);x++)
        {
            var key=new Vector2Int(x,z);
            List<RainRoof> list;
            if(!rainRoofs.TryGetValue(key,out list))rainRoofs[key]=list=new List<RainRoof>();
            list.Add(roof);
        }
    }

    void RefreshRainRoofs()
    {
        if(Time.unscaledTime<nextRoofUpdate)return;
        nextRoofUpdate=Time.unscaledTime+.24f;
        rainRoofs.Clear();
        foreach(var entry in models)
        {
            if(!entry.Key||entry.Key.GetType()!=structureType||!entry.Value.Root.gameObject.activeInHierarchy)continue;
            var model=entry.Value;
            if(model.Relief || model.Field!=Fieldwork.None)continue;
            var scale=model.Root.localScale;
            var axis3=model.Root.forward;
            var axis=new Vector2(axis3.x,axis3.z).normalized;
            var center=model.Root.localPosition;
            // The gate has two solid towers and an uncovered corridor on the exterior approaches.
            if(model.Gate)
            {
                var across=new Vector3(axis.y,0,-axis.x);
                for(int side=-1;side<=1;side+=2)
                    AddRainRoof(new RainRoof{Center=center+across*(side*.40f*scale.x),Axis=axis,
                        Extents=new Vector2(.17f*scale.x,.48f*scale.z),Top=center.y+1.33f*scale.y});
                AddRainRoof(new RainRoof{Center=center,Axis=axis,Extents=new Vector2(.30f*scale.x,.40f*scale.z),
                    Top=center.y+1.08f*scale.y});
                continue;
            }
            AddRainRoof(new RainRoof{Center=center,Axis=axis,Extents=new Vector2(scale.x*.46f,scale.z*.44f),
                Top=center.y+scale.y*(model.Fort ? 1.06f : .96f)});
        }
        foreach(var entry in curtainRoofs)
            if(entry.Key)foreach(var roof in entry.Value)AddRainRoof(roof);
    }

    float RainSurface(float x,float z)
    {
        float ground=GroundHeight(x,z);
        List<RainRoof> list;
        if(!rainRoofs.TryGetValue(new Vector2Int(Mathf.FloorToInt(x/RoofCell),Mathf.FloorToInt(z/RoofCell)),out list))return ground;
        foreach(var roof in list)
        {
            float dx=x-roof.Center.x,dz=z-roof.Center.z;
            if(Mathf.Abs(dx*roof.Axis.x+dz*roof.Axis.y)<=roof.Extents.y &&
                Mathf.Abs(dx*roof.Axis.y-dz*roof.Axis.x)<=roof.Extents.x)ground=Mathf.Max(ground,roof.Top);
        }
        return ground;
    }
}
