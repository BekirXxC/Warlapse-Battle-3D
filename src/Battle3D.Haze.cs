using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class Battle3D
{
    Mesh hazeMesh;
    Material hazeMaterial;
    Transform hazeVolume;

    void SetupHaze()
    {
        var level=Read(battle,"_level");
        int weather=Convert.ToInt32(Read(level,"weather"));
        if(weather!=2 && weather!=4 && weather!=5)return;
        float scale=Mathf.Clamp(Mathf.Max(half.x,half.y)/83,.72f,2.2f);
        float intensity=weather==5 ? .46f : .84f;
        const int shells=22;

        float[] floors={-3,0,5,12,23,43,72};
        float[] density={1,1,.86f,.63f,.36f,.16f,0};
        const int sides=56;
        var vertices=new List<Vector3>();
        var colors=new List<Color32>();
        var faces=new List<int>();
        for(int shell=shells-1;shell>=0;shell--)
        {
            int start=vertices.Count;
            float fraction=shell/(float)(shells-1);
            float radius=Mathf.Lerp(8,115,Mathf.Pow(fraction,1.48f))*scale;
            for(int h=0;h<floors.Length;h++)
            for(int i=0;i<=sides;i++)
            {
                float theta=(i%sides)*Mathf.PI*2/sides;
                vertices.Add(new Vector3(Mathf.Cos(theta)*radius,floors[h]*scale,Mathf.Sin(theta)*radius));
                float variation=.90f+.10f*Mathf.PerlinNoise(13+Mathf.Cos(theta)*1.8f+shell*.07f,9+Mathf.Sin(theta)*1.8f+h*.15f);
                colors.Add(new Color32(151,166,178,(byte)(Mathf.Lerp(5,24,fraction)*density[h]*intensity*variation)));
                if(i==sides||h==floors.Length-1)continue;
                int a=start+h*(sides+1)+i,b=a+sides+1;
                faces.Add(a);faces.Add(a+1);faces.Add(b);
                faces.Add(b);faces.Add(a+1);faces.Add(b+1);
            }
        }
        hazeMesh=new Mesh{name="Mesafeye yayılan alçak pus"};
        hazeMesh.SetVertices(vertices);
        hazeMesh.SetColors(colors);
        hazeMesh.SetTriangles(faces,0);
        hazeMaterial=new Material(shader);
        hazeMaterial.SetInt("_Cull",0);
        hazeMaterial.SetInt("_ZWrite",0);
        hazeMaterial.SetInt("_SrcBlend",5);
        hazeMaterial.SetInt("_DstBlend",10);
        hazeMaterial.renderQueue=3000;
        var host=new GameObject("3D pus");
        host.layer=Layer;
        hazeVolume=host.transform;
        hazeVolume.SetParent(landRoot,false);
        host.AddComponent<MeshFilter>().sharedMesh=hazeMesh;
        host.AddComponent<MeshRenderer>().sharedMaterial=hazeMaterial;
    }

    void DrawHaze()
    {
        if(!hazeVolume)return;
        var eye=MapPoint(view.transform.position);
        hazeVolume.localPosition=new Vector3(eye.x,0,eye.z);
    }

    void ReleaseHaze()
    {
        if(hazeMesh)Destroy(hazeMesh);
        if(hazeMaterial)Destroy(hazeMaterial);
        hazeMesh=null;
        hazeMaterial=null;
        hazeVolume=null;
    }
}

