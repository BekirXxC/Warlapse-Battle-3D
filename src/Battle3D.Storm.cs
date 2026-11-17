using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class Battle3D
{
    Component weatherController;
    Mesh boltMesh;
    Material boltMaterial;
    GameObject boltObject;
    float stormLight, lastWeatherFlash;
    readonly List<Vector3> boltVertices=new List<Vector3>();
    readonly List<int> boltIndices=new List<int>();

    void SetupStorm()
    {
        if (Convert.ToInt32(Read(Read(battle,"_level"),"weather"))!=5)return;
        var type=battleType.Assembly.GetType("Warlapse.Battle.WeatherManager");
        weatherController=type==null ? null : FindAnyObjectByType(type) as Component;
        if(!weatherController)return;
        boltMesh=new Mesh { name="Dallanan şimşek" };
        boltMesh.MarkDynamic();
        boltMaterial=new Material(shader);
        boltMaterial.SetInt("_Cull",0);
        boltMaterial.SetInt("_ZWrite",0);
        boltMaterial.SetInt("_SrcBlend",5);
        boltMaterial.SetInt("_DstBlend",1);
        boltMaterial.renderQueue=3050;
        boltObject=new GameObject("3D şimşek");
        boltObject.layer=Layer;
        boltObject.transform.SetParent(landRoot,false);
        boltObject.AddComponent<MeshFilter>().sharedMesh=boltMesh;
        boltObject.AddComponent<MeshRenderer>().sharedMaterial=boltMaterial;
        boltObject.SetActive(false);
    }

    void BoltSegment(Vector3 a,Vector3 b,float width)
    {
        var normal=Vector3.Cross(b-a,view.transform.forward).normalized*width;
        int i=boltVertices.Count;
        boltVertices.Add(a-normal);
        boltVertices.Add(a+normal);
        boltVertices.Add(b-normal);
        boltVertices.Add(b+normal);
        boltIndices.Add(i);
        boltIndices.Add(i+1);
        boltIndices.Add(i+2);
        boltIndices.Add(i+2);
        boltIndices.Add(i+1);
        boltIndices.Add(i+3);
    }

    void BuildBolt()
    {
        boltVertices.Clear();
        boltIndices.Clear();
        Vector3 eye=MapPoint(view.transform.position);
        Vector3 heading=view.transform.forward;
        heading.y=0;
        if(heading.sqrMagnitude<.05f)heading=Vector3.forward;
        heading.Normalize();
        Vector3 across=new Vector3(heading.z,0,-heading.x);
        var anchor=eye+heading*RandomRange(55,94)+across*RandomRange(-26,26);
        var cloud=new Vector3(anchor.x,Mathf.Max(38,eye.y*.55f+28),anchor.z);
        var ground=new Vector3(anchor.x+RandomRange(-8,8),GroundHeight(anchor.x,anchor.z)+.15f,anchor.z+RandomRange(-4,4));
        var previous=cloud;
        for(int i=1;i<=12;i++)
        {
            float t=i/12f;
            var point=Vector3.Lerp(cloud,ground,t);
            if(i<12)point+=across*RandomRange(-2.7f,2.7f)+heading*RandomRange(-1.3f,1.3f);
            BoltSegment(previous,point,Mathf.Lerp(.22f,.07f,t));
            if(i%3==1)
            {
                var fork=point+across*RandomRange(-6,6)+Vector3.down*4;
                BoltSegment(previous,fork,.065f);
                BoltSegment(fork,fork+across*RandomRange(-3,3)+Vector3.down*3,.045f);
            }
            previous=point;
        }
        boltMesh.Clear(false);
        boltMesh.SetVertices(boltVertices);
        boltMesh.SetTriangles(boltIndices,0);
    }

    void DrawStorm()
    {
        if(!boltObject||!weatherController)return;
        float flash=Mathf.Clamp01(Convert.ToSingle(Read(weatherController,"_flashA")));
        if(flash>.025f && flash>lastWeatherFlash+.045f)BuildBolt();
        lastWeatherFlash=flash;
        stormLight=Mathf.MoveTowards(stormLight,flash,Time.unscaledDeltaTime*(flash>stormLight ? 15 : 3.5f));
        boltObject.SetActive(stormLight>.015f && boltMesh.vertexCount>0);
        boltMaterial.color=new Color(.77f,.87f,1,Mathf.Clamp01(stormLight*1.7f));
        if(terrainMaterial)terrainMaterial.color=Color.Lerp(terrainBaseTint,new Color(1.12f,1.17f,1.23f),stormLight*.75f);
        var flashColor=Color.Lerp(Color.white,new Color(1.28f,1.34f,1.41f),stormLight*.86f);
        foreach(var material in skyMaterials)material.color=flashColor;
    }

    void ReleaseStorm()
    {
        if(boltMesh)Destroy(boltMesh);
        if(boltMaterial)Destroy(boltMaterial);
        boltMesh=null;
        boltMaterial=null;
        boltObject=null;
        weatherController=null;
        stormLight=lastWeatherFlash=0;
    }
}

