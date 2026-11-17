using System.Collections.Generic;
using UnityEngine;

public sealed partial class Battle3D
{
    Mesh flagMesh;
    Material flagMaterial;
    readonly List<Vector3> flagVertices=new List<Vector3>();
    readonly List<Color32> flagColors=new List<Color32>();
    readonly List<int> flagIndices=new List<int>();

    void SetupFortFlags()
    {
        flagMesh=new Mesh { name="Kale sancakları" };
        flagMesh.MarkDynamic();
        flagMaterial=new Material(shader);
        flagMaterial.SetInt("_Cull",0);
        flagMaterial.SetInt("_ZWrite",1);
        flagMaterial.renderQueue=2010;
        var host=new GameObject("Kale sancakları");
        host.layer=Layer;
        host.transform.SetParent(landRoot,false);
        host.AddComponent<MeshFilter>().sharedMesh=flagMesh;
        host.AddComponent<MeshRenderer>().sharedMaterial=flagMaterial;
    }

    void DrawFortFlags()
    {
        flagVertices.Clear();
        flagColors.Clear();
        flagIndices.Clear();
        foreach(var entry in models)
        {
            if(!entry.Key || !entry.Value.Root.gameObject.activeInHierarchy)continue;
            var fort=entry.Value;
            if(!fort.Gate && (!fort.Keep || (entry.Key.GetInstanceID()&3)!=0))continue;
            var owner=Read(Read(entry.Key,"_s"),"Owner");
            var name=owner==null ? "" : owner.ToString().ToLowerInvariant();
            var tint=name.Contains("blue")||name.Contains("player") ? new Color(.23f,.47f,.71f) :
                name.Contains("orange")||name.Contains("enemy")||name.Contains("red") ? new Color(.72f,.31f,.18f) :
                new Color(.66f,.53f,.29f);
            var anchor=MapPoint(fort.Root.TransformPoint(new Vector3(fort.Gate ? -.40f : -.32f,fort.Gate ? 1.51f : 1.28f,.22f)));
            float size=Mathf.Clamp(fort.Root.localScale.y*.48f,.45f,1.8f);
            AddFlag(anchor,size,tint,entry.Key.GetInstanceID());
        }
        flagMesh.Clear(false);
        if(flagVertices.Count==0)return;
        flagMesh.SetVertices(flagVertices);
        flagMesh.SetColors(flagColors);
        flagMesh.SetTriangles(flagIndices,0);
    }

    void AddFlag(Vector3 anchor,float size,Color tint,int seed)
    {
        const int steps=8;
        var mast=anchor+Vector3.up*size;
        var pole=new Color32(75,56,36,255);
        var edge=Vector3.right*(size*.028f);
        FlagQuad(anchor-edge,anchor+edge,mast-edge,mast+edge,pole);
        edge=Vector3.forward*(size*.028f);
        FlagQuad(anchor-edge,anchor+edge,mast-edge,mast+edge,pole);

        int first=flagVertices.Count;
        float time=Time.unscaledTime*(rainMesh ? 7f : 4.2f);
        var wind=new Vector3(.94f,0,.34f);
        var waveAxis=new Vector3(-.34f,0,.94f);
        for(int i=0;i<=steps;i++)
        {
            float t=i/(float)steps;
            float flutter=Mathf.Sin(time-t*9+(seed&15)*.22f);
            var top=mast+wind*(t*size*.91f)+waveAxis*(flutter*t*size*.19f);
            top.y-=t*size*.10f;
            var bottom=top-Vector3.up*(size*(.43f-(i==steps ? .10f : 0)));
            flagVertices.Add(top);
            flagVertices.Add(bottom);
            Color32 shade=tint*(.81f+.19f*flutter);
            shade.a=255;
            flagColors.Add(shade);
            flagColors.Add(shade);
            if(i==steps)continue;
            int k=first+i*2;
            flagIndices.Add(k);flagIndices.Add(k+2);flagIndices.Add(k+1);
            flagIndices.Add(k+1);flagIndices.Add(k+2);flagIndices.Add(k+3);
        }
    }

    void FlagQuad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color32 tint)
    {
        int i=flagVertices.Count;
        flagVertices.Add(a);flagVertices.Add(b);flagVertices.Add(c);flagVertices.Add(d);
        for(int n=0;n<4;n++)flagColors.Add(tint);
        flagIndices.Add(i);flagIndices.Add(i+1);flagIndices.Add(i+2);
        flagIndices.Add(i+2);flagIndices.Add(i+1);flagIndices.Add(i+3);
    }

    void ReleaseFortFlags()
    {
        if(flagMesh)Destroy(flagMesh);
        if(flagMaterial)Destroy(flagMaterial);
        flagMesh=null;
        flagMaterial=null;
    }
}
