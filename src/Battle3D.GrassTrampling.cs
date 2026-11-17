using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class Battle3D
{
    sealed class GrassPatch
    {
        public Mesh Mesh;
        public Vector3[] Rest, Vertices;
    }

    sealed class GrassTuft
    {
        public GrassPatch Patch;
        public Vector2 Center, Direction;
        public int Start, End, BladeCount;
        public float Pressure, LastPressed;
    }

    readonly Dictionary<Vector2Int,List<GrassTuft>> grassCells=new Dictionary<Vector2Int,List<GrassTuft>>();
    readonly List<GrassTuft> pressedGrass=new List<GrassTuft>();
    readonly HashSet<GrassPatch> changedGrass=new HashSet<GrassPatch>();
    float nextGrassUpdate, previousGrassUpdate;

    static Vector2Int GrassCell(Vector2 point)
    {
        return new Vector2Int(Mathf.FloorToInt(point.x),Mathf.FloorToInt(point.y));
    }

    void RegisterGrassPatch(Mesh mesh,List<Vector3> vertices,List<GrassTuft> tufts)
    {
        var patch=new GrassPatch{Mesh=mesh,Rest=vertices.ToArray(),Vertices=vertices.ToArray()};
        var bounds=mesh.bounds;
        bounds.Expand(.60f);
        mesh.bounds=bounds;
        foreach(var tuft in tufts)
        {
            tuft.Patch=patch;
            var cell=GrassCell(tuft.Center);
            List<GrassTuft> nearby;
            if(!grassCells.TryGetValue(cell,out nearby))
                grassCells.Add(cell,nearby=new List<GrassTuft>());
            nearby.Add(tuft);
        }
    }

    void StampGrass(Vector2 position,Vector2 direction,float radius,float now)
    {
        var min=GrassCell(position-Vector2.one*radius);
        var max=GrassCell(position+Vector2.one*radius);
        for(int z=min.y;z<=max.y;z++)
        for(int x=min.x;x<=max.x;x++)
        {
            List<GrassTuft> nearby;
            if(!grassCells.TryGetValue(new Vector2Int(x,z),out nearby))continue;
            foreach(var tuft in nearby)
            {
                float distance=(tuft.Center-position).magnitude;
                if(distance>=radius)continue;
                float pressure=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(radius*.36f,radius,distance));
                if(tuft.Pressure<=0)pressedGrass.Add(tuft);
                if(pressure>=tuft.Pressure*.92f)
                {
                    tuft.Direction=direction;
                    tuft.Pressure=Mathf.Max(tuft.Pressure,pressure);
                    changedGrass.Add(tuft.Patch);
                }
                tuft.LastPressed=now;
            }
        }
    }

    void StampFootsteps(Person person,float now)
    {
        float angle=person.Heading*Mathf.Deg2Rad;
        var direction=new Vector2(Mathf.Sin(angle),Mathf.Cos(angle));
        var side=new Vector2(direction.y,-direction.x);
        float step=Mathf.Sin(person.Phase);
        bool mounted=person.Rig.Legs.Length==4;
        float radius=mounted ? .73f : .48f;
        StampGrass(person.Position+side*(step<0 ? -.12f : .12f)+direction*(Mathf.Abs(step)*.19f),
            direction,radius,now);
    }

    void UpdateGrassTrampling()
    {
        float now=Time.time;
        if(grassCells.Count==0 || now<nextGrassUpdate)return;
        float dt=Mathf.Clamp(now-previousGrassUpdate,0,.18f);
        previousGrassUpdate=now;
        nextGrassUpdate=now+.085f;
        var eye=MapPoint(view.transform.position);
        var center=new Vector2(eye.x,eye.z);
        if(followed!=null && followed.Ready)StampFootsteps(followed,now);
        if(eye.y<12)
        {
            int remaining=16;
            foreach(var person in soldiers)
            {
                if(remaining==0)break;
                if(person==followed || person.Motion<.12f || (person.Position-center).sqrMagnitude>8*8)continue;
                StampFootsteps(person,now);
                remaining--;
            }
        }
        for(int i=pressedGrass.Count-1;i>=0;i--)
        {
            var tuft=pressedGrass[i];
            if(now-tuft.LastPressed>.35f)
            {
                tuft.Pressure=Mathf.Max(0,tuft.Pressure-dt*.34f);
                changedGrass.Add(tuft.Patch);
            }
            if(changedGrass.Contains(tuft.Patch))BendGrass(tuft);
            if(tuft.Pressure>0)continue;
            pressedGrass[i]=pressedGrass[pressedGrass.Count-1];
            pressedGrass.RemoveAt(pressedGrass.Count-1);
        }
        foreach(var patch in changedGrass)
            patch.Mesh.SetVertices(patch.Vertices,0,patch.Vertices.Length,MeshUpdateFlags.DontRecalculateBounds);
        changedGrass.Clear();
    }

    static void BendGrass(GrassTuft tuft)
    {
        var patch=tuft.Patch;
        var lean=new Vector3(tuft.Direction.x,0,tuft.Direction.y)*tuft.Pressure*.23f;
        int bladesEnd=tuft.Start+tuft.BladeCount*5;
        for(int i=tuft.Start;i<tuft.End;i++)
        {
            Vector3 vertex=patch.Rest[i];
            int part=i<bladesEnd ? (i-tuft.Start)%5 : 4;
            if(part>=2)
            {
                int root=i<bladesEnd ? i-part : bladesEnd;
                float height=Mathf.Max(0,vertex.y-patch.Rest[root].y);
                vertex+=lean*(part==4 ? 1 : .48f);
                vertex.y-=height*tuft.Pressure*.83f;
            }
            patch.Vertices[i]=vertex;
        }
    }

    void ReleaseGrassTrampling()
    {
        grassCells.Clear();
        pressedGrass.Clear();
        changedGrass.Clear();
        nextGrassUpdate=previousGrassUpdate=0;
    }
}
