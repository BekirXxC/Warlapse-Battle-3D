using System.Collections.Generic;
using UnityEngine;

public sealed partial class Battle3D
{
    Mesh waterMesh;
    Material waterMaterial;
    Color32[] waterColors;
    Vector3[] waterPoints;
    float[] shoreWeights;
    float waterUpdate;

    void BuildWater(Vector3[] ground,int[] faces,float[] coverage,Vector3[] rim,int[] rimFaces,float[] rimCoverage)
    {
        var vertices=new List<Vector3>();
        var shore=new List<float>();
        var indices=new List<int>();
        ClipWaterSurface(ground,faces,coverage,vertices,shore,indices);
        ClipWaterSurface(rim,rimFaces,rimCoverage,vertices,shore,indices);
        if(vertices.Count==0)return;
        waterPoints=vertices.ToArray();
        shoreWeights=shore.ToArray();
        waterColors=new Color32[waterPoints.Length];
        waterMesh=new Mesh { name="Kıyı ve dalga yüzeyi",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32 };
        waterMesh.MarkDynamic();
        waterMesh.vertices=waterPoints;
        waterMesh.SetTriangles(indices,0);
        waterMaterial=new Material(shader);
        waterMaterial.SetInt("_Cull",0);
        waterMaterial.SetInt("_ZWrite",0);
        waterMaterial.SetInt("_SrcBlend",5);
        waterMaterial.SetInt("_DstBlend",10);
        waterMaterial.renderQueue=2970;
        var surface=new GameObject("Hareketli su");
        surface.layer=Layer;
        surface.transform.SetParent(landRoot,false);
        surface.AddComponent<MeshFilter>().sharedMesh=waterMesh;
        surface.AddComponent<MeshRenderer>().sharedMaterial=waterMaterial;
        waterUpdate=0;
    }

    static void ClipWaterSurface(Vector3[] ground,int[] faces,float[] coverage,
        List<Vector3> vertices,List<float> shore,List<int> indices)
    {
        var mapped=new Dictionary<int,int>();
        var crossings=new Dictionary<long,int>();
        const float edge=.42f;
        var clipped=new int[4];
        for(int i=0;i<faces.Length;i+=3)
        {
            int count=0;
            for(int j=0;j<3;j++)
            {
                int from=faces[i+(j+2)%3], to=faces[i+j], index;
                bool start=coverage[from]>=edge, end=coverage[to]>=edge;
                if(start!=end)
                {
                    long key=((long)Mathf.Min(from,to)<<32)|(uint)Mathf.Max(from,to);
                    if(!crossings.TryGetValue(key,out index))
                    {
                        index=vertices.Count;
                        crossings[key]=index;
                        float t=Mathf.InverseLerp(coverage[from],coverage[to],edge);
                        vertices.Add(Vector3.Lerp(ground[from],ground[to],t)+Vector3.up*.042f);
                        shore.Add(1);
                    }
                    clipped[count++]=index;
                }
                if(!end)continue;
                if (!mapped.TryGetValue(to,out index))
                {
                    index=vertices.Count;
                    mapped.Add(to,index);
                    vertices.Add(ground[to]+Vector3.up*.038f);
                    shore.Add(1-Mathf.InverseLerp(edge,.96f,coverage[to]));
                }
                clipped[count++]=index;
            }
            for(int j=1;j+1<count;j++)
            {
                indices.Add(clipped[0]);indices.Add(clipped[j]);indices.Add(clipped[j+1]);
            }
        }
    }

    void DrawWater()
    {
        float time=Time.unscaledTime;
        if (!waterMesh || time<waterUpdate)return;
        waterUpdate=time+.046f;
        for(int i=0;i<waterPoints.Length;i++)
        {
            var p=waterPoints[i];
            float wave=.5f+.5f*Mathf.Sin(p.x*.52f+p.z*.84f-time*1.7f);
            float ripple=.5f+.5f*Mathf.Sin(p.x*1.2f-p.z*.49f+time*.91f);
            float detail=.5f+.5f*Mathf.Sin(p.x*2.12f+p.z*1.3f-time*2.27f);
            float glint=wave*wave*ripple*(.64f+.36f*detail);
            float shore=shoreWeights[i];
            float sweep=.62f+.16f*Mathf.Sin(time*2.1f+p.x*.23f+p.z*.32f);
            float crest=Mathf.Max(0,1-Mathf.Abs(shore-sweep)*5.1f);
            float foam=Mathf.Clamp01(crest*(.43f+.57f*detail)+Mathf.Pow(shore,3)*.20f);
            waterColors[i]=new Color32((byte)(81+glint*69+foam*89),(byte)(133+glint*65+foam*57),
                (byte)(165+glint*49+foam*51),(byte)(32+glint*72+foam*105));
        }
        waterMesh.colors32=waterColors;
    }

    void ReleaseWater()
    {
        if(waterMesh)Destroy(waterMesh);
        if(waterMaterial)Destroy(waterMaterial);
        waterMesh=null;
        waterMaterial=null;
        waterPoints=null;
        waterColors=null;
        shoreWeights=null;
    }
}
