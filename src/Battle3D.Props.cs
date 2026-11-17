using System.Collections.Generic;
using UnityEngine;

public sealed partial class Battle3D
{
    void Loft(List<Vector3> verts,List<Color> colors,List<int> faces,Vector3 from,Vector3 to,
        float bottom,float top,Color tint,int sides,int seed,float roughness)
    {
        var axis=(to-from).normalized;
        var across=Vector3.Cross(axis,Mathf.Abs(axis.y)>.8f ? Vector3.forward : Vector3.up).normalized;
        var across2=Vector3.Cross(axis,across);
        var lower=new Vector3[sides];
        var upper=new Vector3[sides];
        for(int i=0;i<sides;i++)
        {
            float a=(i+(seed%7)*.14f)*Mathf.PI*2/sides;
            Vector3 radial=across*Mathf.Cos(a)+across2*Mathf.Sin(a);
            float bump=1+roughness*Mathf.Sin(i*2.3f+seed);
            lower[i]=from+radial*bottom*bump;
            upper[i]=to+radial*top*(2-bump);
        }
        for(int i=0;i<sides;i++)
        {
            int j=(i+1)%sides;
            Face(verts,colors,faces,lower[i],lower[j],upper[i],tint,seed+i);
            Face(verts,colors,faces,upper[i],lower[j],upper[j],tint,seed+i);
            Face(verts,colors,faces,to,upper[i],upper[j],tint,seed+i);
            Face(verts,colors,faces,from,lower[j],lower[i],tint,seed+i);
        }
    }

    static void Face(List<Vector3> verts,List<Color> colors,List<int> faces,
        Vector3 a,Vector3 b,Vector3 c,Color tint,int seed)
    {
        var n=Vector3.Cross(b-a,c-a).normalized;
        float light=.59f+.37f*Mathf.Max(0,Vector3.Dot(n,new Vector3(-.43f,.82f,-.37f)))+
            .11f*Mathf.PerlinNoise(seed*.37f+2,12);
        var color=tint*light;
        color.a=1;
        int index=verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c);
        colors.Add(color); colors.Add(color); colors.Add(color);
        faces.Add(index); faces.Add(index+1); faces.Add(index+2);
    }

    static bool IsTreeProp(string id)
    {
        return id.Contains("agac") || id.Contains("tree");
    }

    Transform BuildProp(string id,Transform parent)
    {
        bool tree=IsTreeProp(id);
        bool log=id.Contains("kutug")||id.Contains("log");
        bool dead=id.Contains("kuru")||id.Contains("dead");
        bool bush=id.Contains("cali")||id.Contains("bush")||id.Contains("shrub");
        bool rock=id.Contains("kaya")||id.Contains("rock")||id.Contains("boulder");
        bool hay=id.Contains("saman")||id.Contains("hay");
        bool ruin=id.Contains("yikik")||id.Contains("ruined");
        bool ship=id.Contains("gemi")||id.Contains("ship");
        bool boat=ship||id.Contains("kayik")||id.Contains("boat");
        if(!tree&&!log&&!bush&&!rock&&!hay&&!ruin&&!boat)return null;
        bool pine=id.Contains("pine")||id.Contains("conifer")||id.Contains("cam");
        int variant=(parent.GetInstanceID()&3);
        string key="decor|" + id + "|" + variant;
        Mesh mesh;
        if(!infantryMeshes.TryGetValue(key,out mesh))
        {
            var verts=new List<Vector3>();
            var colors=new List<Color>();
            var faces=new List<int>();
            var bark=Color.Lerp(new Color(.31f,.21f,.13f),new Color(.46f,.32f,.21f),variant/3f);
            var leaf=Color.Lerp(new Color(.24f,.38f,.17f),new Color(.46f,.53f,.24f),variant/3f);
            if(boat||ruin||hay)
            {
                var design=Joint(figureLibrary ? figureLibrary : landRoot,"Dekor şablonu",Vector3.zero);
                design.gameObject.SetActive(false);
                if(boat)
                {
                    Shape(design,"Tekne omurgası","torso",bark,new Vector3(0,.16f,0),new Vector3(.50f,.29f,.96f));
                    Part(design,"Güverte",bark*.76f,new Vector3(0,.293f,0),new Vector3(.36f,.036f,.64f));
                    for(int side=-1;side<=1;side+=2)
                    {
                        Part(design,"Tekne bordası",bark*.89f,new Vector3(side*.222f,.313f,0),new Vector3(.039f,.090f,.67f));
                        Part(design,"Tekne sırası",bark*1.14f,new Vector3(0,.327f,side*.18f),new Vector3(.41f,.054f,.101f));
                    }
                    if(ship)
                    {
                        Part(design,"Direk",bark*.80f,new Vector3(0,.56f,.05f),new Vector3(.043f,.68f,.052f));
                        Shape(design,"Yelken","shield",new Color(.77f,.74f,.60f),new Vector3(0,.68f,.069f),new Vector3(.42f,.39f,.038f));
                    }
                    else
                        Part(design,"Kürek",bark*1.17f,new Vector3(-.06f,.37f,-.07f),new Vector3(.64f,.024f,.061f))
                            .localRotation=Quaternion.Euler(0,18,12);
                }
                else if(ruin)
                {
                    var stone=new Color(.50f,.48f,.41f);
                    for(int i=0;i<6;i++)
                    {
                        Shape(design,"Yıkık taş","round",stone*(.77f+Noise01(i+variant*17)*.31f),
                            new Vector3(-.4f+i*.16f,.08f,.034f*Mathf.Sin(i*7)),new Vector3(.19f,.17f,.21f));
                        if(i<4) Part(design,"Kırık duvar sırası",stone*.88f,new Vector3(-.35f+i*.16f,.17f+(.07f*(3-i)),0),
                            new Vector3(.18f,.19f+.07f*(3-i),.16f)).localRotation=Quaternion.Euler(0,i*5,i*7);
                    }
                }
                else
                {
                    var straw=new Color(.64f,.52f,.26f);
                    for(int i=0;i<5;i++)
                    {
                        float angle=i*2.4f+variant;
                        Shape(design,"Saman kümesi","round",straw*(.86f+i*.061f),
                            new Vector3(Mathf.Cos(angle)*.18f,.15f+i*.055f,Mathf.Sin(angle)*.18f),
                            new Vector3(.48f-i*.044f,.29f,.39f-i*.036f));
                    }
                }
                mesh=BakeParts(design,design.GetComponentsInChildren<MeshRenderer>(true),key);
                Destroy(design.gameObject);
                var piece=Joint(parent,"3D dekor",Vector3.zero);
                AttachMesh(piece,mesh);
                return piece;
            }
            else if(log)
                Loft(verts,colors,faces,new Vector3(-.43f,.12f,-.11f),new Vector3(.42f,.14f,.13f),.145f,.105f,bark,8,variant,.14f);
            else if(tree)
            {
                var crown=new Vector3(.04f*variant,.89f+variant*.04f,0);
                Loft(verts,colors,faces,Vector3.zero,crown,.105f,.029f,bark,7,variant,.12f);
                for(int i=0;i<4;i++)
                {
                    float angle=i*1.57f+variant*.49f;
                    Loft(verts,colors,faces,new Vector3(Mathf.Cos(angle)*.19f,.013f,Mathf.Sin(angle)*.19f),
                        Vector3.up*.18f,.025f,.069f,bark*.84f,5,i,.15f);
                }
                if(pine&&!dead)
                {
                    for(int i=0;i<4;i++)
                    {
                        float radius=.34f-i*.061f;
                        var basePoint=Vector3.up*(.32f+i*.179f);
                        Loft(verts,colors,faces,basePoint,basePoint+Vector3.up*.08f,radius*.72f,radius,leaf*.82f,7,i+variant,.19f);
                        Loft(verts,colors,faces,basePoint+Vector3.up*.08f,basePoint+Vector3.up*(.42f-i*.04f),
                            radius,.028f,leaf*(1f+i*.045f),7,i+variant,.19f);
                    }
                }
                else
                for(int i=0;i<5;i++)
                {
                    float theta=i*2.399f+variant;
                    var start=new Vector3(.01f,.30f+i*.085f,0);
                    var end=start+new Vector3(Mathf.Cos(theta)*.27f,.23f+i*.018f,Mathf.Sin(theta)*.27f);
                    Loft(verts,colors,faces,start,end,.045f,.013f,bark,5,i+variant,.14f);
                    if(dead)
                    {
                        Loft(verts,colors,faces,Vector3.Lerp(start,end,.65f),end+new Vector3(-.04f,.13f,.10f),.018f,.005f,bark,5,i,.12f);
                        continue;
                    }
                    float radius=.19f+i*.014f;
                    Loft(verts,colors,faces,end-Vector3.up*.14f,end+Vector3.up*.17f,radius*.79f,radius,leaf,7,i+variant,.25f);
                    Loft(verts,colors,faces,end+Vector3.up*.17f,end+Vector3.up*.29f,radius,.035f,leaf*1.06f,7,i+variant,.25f);
                    var rim=end+new Vector3(Mathf.Cos(theta)*.15f,.065f,Mathf.Sin(theta)*.15f);
                    Loft(verts,colors,faces,rim-Vector3.up*.035f,rim+Vector3.up*.16f,radius*.51f,.027f,leaf*.92f,6,i+variant+7,.24f);
                }
            }
            else
            {
                var tint=rock ? Color.Lerp(new Color(.38f,.40f,.41f),new Color(.59f,.55f,.47f),variant/3f) : leaf;
                for(int i=0;i<3;i++)
                {
                    var at=new Vector3((i-1)*.16f,.015f,Mathf.Sin(i*4)*.13f);
                    float radius=.23f-.028f*i;
                    var edge=at+new Vector3(.04f,.15f+i*.025f,.02f);
                    Loft(verts,colors,faces,at,edge,radius*.86f,radius,tint,7,i+variant,.26f);
                    Loft(verts,colors,faces,edge,edge+new Vector3(-.04f,.18f,.05f),radius,.045f,tint,7,i+variant,.26f);
                }
            }
            mesh=new Mesh { name="Ayrıntılı doğal dekor" };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(faces,0);
            infantryMeshes.Add(key,mesh);
        }
        var prop=Joint(parent,"3D dekor",Vector3.zero);
        prop.gameObject.layer=Layer;
        prop.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        var material=Paint(new Color(.995f,1,1));
        material.SetInt("_Cull",0);
        prop.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
        return prop;
    }
}
