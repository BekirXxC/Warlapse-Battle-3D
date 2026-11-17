using System.Collections.Generic;
using UnityEngine;

public sealed partial class Battle3D
{
    Mesh torchMesh;
    Material torchMaterial;
    readonly List<Vector3> torchVertices=new List<Vector3>();
    readonly List<Color32> torchColors=new List<Color32>();
    readonly List<int> torchIndices=new List<int>();

    void SetupTorches()
    {
        torchMesh=new Mesh { name="Kapı meşaleleri" };
        torchMesh.MarkDynamic();
        torchMaterial=new Material(shader);
        torchMaterial.SetInt("_Cull",0);
        torchMaterial.SetInt("_ZWrite",0);
        torchMaterial.SetInt("_SrcBlend",5);
        torchMaterial.SetInt("_DstBlend",10);
        torchMaterial.renderQueue=2960;
        var host=new GameObject("Kapı meşaleleri");
        host.layer=Layer;
        host.transform.SetParent(landRoot,false);
        host.AddComponent<MeshFilter>().sharedMesh=torchMesh;
        host.AddComponent<MeshRenderer>().sharedMaterial=torchMaterial;
    }

    void DrawTorches()
    {
        torchVertices.Clear(); torchColors.Clear(); torchIndices.Clear();
        foreach(var entry in models)
        {
            var fort=entry.Value;
            if(!entry.Key || !fort.Gate || !fort.Root.gameObject.activeInHierarchy)continue;
            float size=Mathf.Clamp(fort.Root.localScale.y*.17f,.16f,.52f);
            for(int side=-1;side<=1;side+=2)
            for(int face=-1;face<=1;face+=2)
            {
                var point=MapPoint(fort.Root.TransformPoint(new Vector3(side*.40f,.67f,face*.51f)));
                float phase=Time.unscaledTime*12+side*3+face*2+(entry.Key.GetInstanceID()&7);
                Flame(point,size,phase);
                TorchLight(fort.Root,side,face,phase);
            }
        }
        torchMesh.Clear(false);
        if(torchVertices.Count==0)return;
        torchMesh.SetVertices(torchVertices);
        torchMesh.SetColors(torchColors);
        torchMesh.SetTriangles(torchIndices,0);
    }

    void TorchLight(Transform gate,int side,int face,float phase)
    {
        const int segments=14;
        float x=side*.40f;
        float depth=face*.468f;
        float flicker=.69f+Mathf.Sin(phase)*.21f+Mathf.Sin(phase*1.67f)*.10f;
        int start=torchVertices.Count;
        torchVertices.Add(MapPoint(gate.TransformPoint(new Vector3(x,.76f,depth))));
        torchColors.Add(new Color32(255,142,55,(byte)(128*flicker)));
        for(int i=0;i<segments;i++)
        {
            float angle=i*Mathf.PI*2/segments;
            float xx=x+Mathf.Cos(angle)*.133f;
            float yy=.76f+Mathf.Sin(angle)*(.30f+.025f*flicker);
            torchVertices.Add(MapPoint(gate.TransformPoint(new Vector3(xx,yy,depth))));
            torchColors.Add(new Color32(255,137,48,0));
            torchIndices.Add(start);
            torchIndices.Add(start+1+i);
            torchIndices.Add(start+1+(i+1)%segments);
        }
    }
    void Flame(Vector3 p,float size,float phase)
    {
        Vector3 right=landRoot.InverseTransformVector(view.transform.right*size);
        Vector3 up=Vector3.up*size;
        var tip=p+up*(1.1f+Mathf.Sin(phase)*.22f)+right*(.16f*Mathf.Sin(phase*.82f));
        TorchFace(p-right*.40f,p+right*.40f,tip,new Color32(239,108,30,206),new Color32(255,151,47,20));
        TorchFace(p-right*.22f,p+right*.22f,Vector3.Lerp(p,tip,.74f),new Color32(255,226,135,246),new Color32(255,180,51,100));
        TorchFace(p-right*.62f+up*.25f,p+right*.62f+up*.25f,p-up*.40f,new Color32(255,150,53,23),new Color32(255,153,51,0));
    }

    void TorchFace(Vector3 a,Vector3 b,Vector3 c,Color32 lower,Color32 upper)
    {
        int i=torchVertices.Count;
        torchVertices.Add(a);torchVertices.Add(b);torchVertices.Add(c);
        torchColors.Add(lower);torchColors.Add(lower);torchColors.Add(upper);
        torchIndices.Add(i);torchIndices.Add(i+1);torchIndices.Add(i+2);
    }

    void ReleaseTorches()
    {
        if(torchMesh)Destroy(torchMesh);
        if(torchMaterial)Destroy(torchMaterial);
        torchMesh=null;
        torchMaterial=null;
    }
}

