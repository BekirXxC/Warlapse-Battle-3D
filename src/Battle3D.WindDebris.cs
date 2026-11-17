using System.Collections.Generic;
using UnityEngine;

public sealed partial class Battle3D
{
    Mesh debrisMesh;
    Material debrisMaterial;
    Vector3[] debrisPositions;
    readonly List<Vector3> debrisVertices=new List<Vector3>();
    readonly List<Color32> debrisColors=new List<Color32>();
    readonly List<int> debrisTriangles=new List<int>();

    Vector3 RainWind(float time)
    {
        float gust=Mathf.Sin(time*.73f)*1.9f+Mathf.Sin(time*1.93f+.8f)*.75f;
        return new Vector3(2.1f+rainStrength*3.7f+gust,0,1.1f+gust*.46f);
    }

    void SetupWindDebris()
    {
        int count=Mathf.RoundToInt(Mathf.Lerp(36,104,Mathf.InverseLerp(.18f,1.4f,rainStrength)));
        debrisPositions=new Vector3[count];
        for(int i=0;i<count;i++)debrisPositions[i].y=float.NegativeInfinity;
        debrisMesh=new Mesh{name="Rüzgârdaki yapraklar"};
        debrisMesh.MarkDynamic();
        debrisMaterial=CreateWorldEffectMaterial(2988);
        var holder=Joint(landRoot,"Rüzgârdaki yapraklar",Vector3.zero);
        holder.gameObject.layer=Layer;
        holder.gameObject.AddComponent<MeshFilter>().sharedMesh=debrisMesh;
        holder.gameObject.AddComponent<MeshRenderer>().sharedMaterial=debrisMaterial;
    }

    void DrawWindDebris()
    {
        if(!rainMesh)return;
        var eye=MapPoint(view.transform.position);
        if(eye.y-GroundHeight(eye.x,eye.z)>22)
        {
            if(debrisMesh && debrisMesh.vertexCount>0)debrisMesh.Clear(false);
            return;
        }
        if(!debrisMesh)SetupWindDebris();
        debrisVertices.Clear(); debrisColors.Clear(); debrisTriangles.Clear();
        float time=Time.unscaledTime, dt=Mathf.Min(.05f,Time.unscaledDeltaTime);
        var wind=RainWind(time);
        for(int i=0;i<debrisPositions.Length;i++)
        {
            var point=debrisPositions[i];
            float phase=time*(1.4f+Noise01(i*11)*2)+i*1.27f;
            float reach=13+Noise01(i+17)*7;
            if(float.IsNegativeInfinity(point.y) || Mathf.Abs(point.x-eye.x)>reach || Mathf.Abs(point.z-eye.z)>reach)
            {
                point.x=eye.x+RandomRange(-reach,reach);
                point.z=eye.z+RandomRange(-reach,reach);
                point.y=GroundHeight(point.x,point.z)+RandomRange(.09f,1.55f);
            }
            else
            {
                var drift=wind*(.28f+Noise01(i*23)*.50f);
                drift.z+=Mathf.Cos(phase)*.70f;
                point+=drift*dt;
                float ground=GroundHeight(point.x,point.z);
                float desired=ground+.18f+Noise01(i*31)*.96f+Mathf.Sin(phase)*.18f;
                point.y=Mathf.Max(ground+.06f,Mathf.Lerp(point.y,desired,dt*1.8f));
            }
            debrisPositions[i]=point;
            float roof=RainSurface(point.x,point.z);
            if(roof>point.y+.13f || Mathf.Abs(point.x)>half.x || Mathf.Abs(point.z)>half.y)continue;
            float distance=(point-eye).magnitude;
            float opacity=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,1.1f,distance))*
                (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(11,19,distance)));
            if(opacity<=.01f)continue;
            float size=.026f+Noise01(i*19)*.050f;
            var spin=Quaternion.Euler(phase*49,i*27+time*75,phase*112);
            var length=spin*Vector3.forward*size;
            var width=spin*Vector3.right*size*.48f;
            var fold=spin*Vector3.up*size*.18f;
            byte tint=(byte)(46+Noise01(i*13)*51),alpha=(byte)(opacity*230);
            AddGroundTriangle(debrisVertices,debrisColors,debrisTriangles,point-length,point-width,
                point+length,new Color32(tint,(byte)(tint*.81f),30,alpha));
            AddGroundTriangle(debrisVertices,debrisColors,debrisTriangles,point-length,point+width+fold,
                point+length,new Color32((byte)(tint+16),(byte)(tint*.96f),35,alpha));
        }
        debrisMesh.Clear(false);
        debrisMesh.SetVertices(debrisVertices);
        debrisMesh.SetColors(debrisColors);
        debrisMesh.SetTriangles(debrisTriangles,0);
    }

    void ReleaseWindDebris()
    {
        if(debrisMesh)Destroy(debrisMesh);
        if(debrisMaterial)Destroy(debrisMaterial);
        debrisMesh=null;
        debrisMaterial=null;
        debrisPositions=null;
    }
}
