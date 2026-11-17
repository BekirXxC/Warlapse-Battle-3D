using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class Battle3D
{
    readonly List<Mesh> landscapeMeshes=new List<Mesh>();
    Material groundDetailMaterial;

    void BuildGroundDetail(object world,Color[] palette,float[] water,float[] bare,float[] rocky)
    {
        var buildingCover=new bool[palette.Length];
        var protectedRock=new bool[palette.Length];
        foreach(var structure in Read(world,"Structures") as IEnumerable ?? new object[0])
        {
            if(IsHighGround(structure))continue;
            var footprint=Read(structure,"Footprint");
            if(!(footprint is Rect))continue;
            MarkLandscapeArea(buildingCover,(Rect)footprint,.17f);
            MarkLandscapeArea(protectedRock,(Rect)footprint,2.0f);
        }
        foreach(var unit in Read(world,"Units") as IEnumerable ?? new object[0])
            MarkLandscapePoint(protectedRock,Read(unit,"Position"),2.4f);
        foreach(var zone in Read(world,"Zones") as IEnumerable ?? new object[0])
            MarkLandscapePoint(protectedRock,Read(zone,"Position"),3.8f);
        foreach(var prop in Read(Read(battle,"_level"),"decor") as IEnumerable ?? new object[0])
            MarkLandscapePoint(protectedRock,Read(prop,"pos"),1.45f);

        groundDetailMaterial=new Material(Paint(Color.white));
        groundDetailMaterial.SetInt("_Cull",0);
        float spacing=Mathf.Max(.40f,Mathf.Sqrt(4*half.x*half.y/28500f));
        int columns=Mathf.CeilToInt(half.x*2/spacing), rows=Mathf.CeilToInt(half.y*2/spacing);
        const int chunk=24;
        var vertices=new List<Vector3>();
        var colors=new List<Color32>();
        var faces=new List<int>();
        var tufts=new List<GrassTuft>();
        for(int z0=0;z0<rows;z0+=chunk)
        for(int x0=0;x0<columns;x0+=chunk)
        {
            vertices.Clear();
            colors.Clear();
            faces.Clear();
            tufts.Clear();
            int zEnd=Mathf.Min(z0+chunk,rows), xEnd=Mathf.Min(x0+chunk,columns);
            for(int z=z0;z<zEnd;z++)
            for(int x=x0;x<xEnd;x++)
            {
                int seed=x*7307+z*2909;
                float wx=(x+.13f+Noise01(seed+31)*.74f)*spacing-half.x;
                float wz=(z+.13f+Noise01(seed+83)*.74f)*spacing-half.y;
                if(wx>half.x-.11f || wz>half.y-.11f)continue;
                var point=new Vector2(wx,wz);
                int at=LandscapeIndex(point);
                if(buildingCover[at] || bare[at]>.20f || water[at]>.32f)continue;
                var source=palette[at];
                bool shore=water[at]>.09f;
                if(!shore && !GreenGround(source))continue;
                float field=Mathf.PerlinNoise(wx*.64f+19,wz*.61f+33);
                float coverage=Noise01(seed+13);
                if(coverage>(shore ? .35f : .77f+.23f*field))continue;
                int first=vertices.Count;
                int blades=AddGrassTuft(vertices,colors,faces,point,source,seed,field,spacing,shore);
                tufts.Add(new GrassTuft{Center=point,Start=first,BladeCount=blades,End=vertices.Count});
            }
            var grass=AddLandscapeMesh("Çimen ve kır çiçekleri",vertices,colors,faces,groundDetailMaterial,true);
            if(grass)RegisterGrassPatch(grass,vertices,tufts);
        }
        BuildScatteredRocks(palette,water,bare,rocky,protectedRock);
    }

    static bool GreenGround(Color color)
    {
        return color.g>color.r*1.025f && color.g>color.b*1.08f;
    }

    int LandscapeIndex(Vector2 point)
    {
        int x=Mathf.Clamp(Mathf.RoundToInt((point.x+half.x)*gridX/(half.x*2)),0,gridX);
        int z=Mathf.Clamp(Mathf.RoundToInt((point.y+half.y)*gridZ/(half.y*2)),0,gridZ);
        return z*(gridX+1)+x;
    }

    void MarkLandscapePoint(bool[] mask,object point,float radius)
    {
        if(point is Vector2)
        {
            var p=(Vector2)point;
            MarkLandscapeArea(mask,new Rect(p.x,p.y,0,0),radius);
        }
    }

    void MarkLandscapeArea(bool[] mask,Rect area,float pad)
    {
        var min=area.min-Vector2.one*pad;
        var max=area.max+Vector2.one*pad;
        if(max.x< -half.x || min.x>half.x || max.y< -half.y || min.y>half.y)return;
        int first=LandscapeIndex(min), last=LandscapeIndex(max);
        int width=gridX+1;
        for(int z=first/width;z<=last/width;z++)
        for(int x=first%width;x<=last%width;x++)
            mask[z*width+x]=true;
    }

    int AddGrassTuft(List<Vector3> vertices,List<Color32> colors,List<int> faces,Vector2 point,
        Color source,int seed,float field,float spacing,bool shore,float surfaceY=float.NaN)
    {
        float dryness=Mathf.Clamp01((source.r/Mathf.Max(.025f,source.g)-.58f)*2.2f);
        var green=Color.Lerp(new Color(.22f,.39f,.12f),new Color(.40f,.47f,.19f),dryness);
        float species=Noise01(seed+709);
        if(!shore && species>.77f)
            green=Color.Lerp(green,new Color(.46f,.48f,.24f),.68f);
        else if(!shore && species<.18f)
            green=Color.Lerp(green,new Color(.13f,.36f,.23f),.65f);
        var baseTint=Color.Lerp(source,green,.60f)*.69f;
        var tipTint=Color.Lerp(source,green*1.29f,.68f);
        if(shore)tipTint=Color.Lerp(tipTint,new Color(.55f,.54f,.27f),.53f);
        int blades=shore ? 4 : field>.58f ? 6 : 4;
        float direction=Noise01(seed+17)*Mathf.PI*2;
        for(int blade=0;blade<blades;blade++)
        {
            float seedling=Noise01(seed+blade*117+43);
            float angle=direction+blade*2.39996f;
            var side=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            var bend=new Vector3(side.z,0,-side.x);
            float spread=Mathf.Min(.24f,spacing*.39f)*(.32f+.68f*seedling);
            var foot=new Vector3(point.x,0,point.y)+bend*spread;
            foot.y=(float.IsNaN(surfaceY) ? GroundHeight(foot.x,foot.z) : surfaceY)+.014f;
            float height=shore ? .33f+seedling*.25f : .16f+field*.18f+seedling*.20f;
            if(!shore)height*=Mathf.Lerp(.76f,1.33f,species);
            float width=(.028f+seedling*.026f)*(shore ? .71f : Mathf.Lerp(1.42f,.65f,species));
            var middle=foot+bend*(height*.19f)+Vector3.up*(height*.58f);
            var top=foot+bend*(height*(.29f+seedling*.31f))+Vector3.up*height;
            float variation=.86f+seedling*.25f;
            Color32 low=baseTint*variation, mid=Color.Lerp(baseTint,tipTint,.68f)*variation, high=tipTint*variation;
            int i=vertices.Count;
            vertices.Add(foot-side*width); vertices.Add(foot+side*width);
            vertices.Add(middle-side*width*.49f); vertices.Add(middle+side*width*.49f); vertices.Add(top);
            colors.Add(low); colors.Add(low); colors.Add(mid); colors.Add(mid); colors.Add(high);
            AddSkyQuad(faces,i,i+1,i+2,i+3);
            faces.Add(i+2); faces.Add(i+3); faces.Add(i+4);
        }
        if(shore || !float.IsNaN(surfaceY) || Noise01(seed+597)<.944f || field<.45f)return blades;
        var origin=new Vector3(point.x,GroundHeight(point.x,point.y),point.y);
        var flower=origin+new Vector3(.03f,.29f+field*.12f,-.02f);
        float bloom=Noise01(seed+365);
        var petals=bloom>.74f ? new Color(.78f,.39f,.32f) : bloom>.53f ? new Color(.65f,.56f,.82f) :
            bloom>.28f ? new Color(.88f,.78f,.34f) : new Color(.86f,.83f,.71f);
        AddGroundTriangle(vertices,colors,faces,origin-Vector3.right*.017f,origin+Vector3.right*.017f,flower,tipTint);
        for(int i=0;i<4;i++)
        {
            float angle=i*Mathf.PI*.5f+direction;
            var outward=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            var across=new Vector3(-outward.z,0,outward.x);
            AddGroundTriangle(vertices,colors,faces,flower,flower+outward*.066f+across*.034f+Vector3.up*.016f,
                flower+outward*.066f-across*.034f+Vector3.up*.016f,petals);
        }
        return blades;
    }

    static void AddGroundTriangle(List<Vector3> vertices,List<Color32> colors,List<int> faces,
        Vector3 a,Vector3 b,Vector3 c,Color32 color)
    {
        int i=vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c);
        colors.Add(color); colors.Add(color); colors.Add(color);
        faces.Add(i); faces.Add(i+1); faces.Add(i+2);
    }

    void BuildScatteredRocks(Color[] palette,float[] water,float[] bare,float[] rocky,bool[] protectedRock)
    {
        var vertices=new List<Vector3>();
        var colors=new List<Color>();
        var faces=new List<int>();
        const float step=3.65f;
        for(int z=0;z<Mathf.FloorToInt(half.y*2/step);z++)
        for(int x=0;x<Mathf.FloorToInt(half.x*2/step);x++)
        {
            int seed=x*1399+z*1481;
            var point=new Vector2((x+.21f+Noise01(seed+5)*.58f)*step-half.x,
                (z+.21f+Noise01(seed+9)*.58f)*step-half.y);
            int at=LandscapeIndex(point);
            if(protectedRock[at] || water[at]>.02f || bare[at]>.18f)continue;
            bool hill=rocky[at]>.56f;
            float edge=Mathf.Min(half.x-Mathf.Abs(point.x),half.y-Mathf.Abs(point.y));
            if((!hill && (edge>8 || !GreenGround(palette[at]))) || Noise01(seed+19)>(hill ? .69f : .33f))continue;
            var rock=Color.Lerp(new Color(.48f,.46f,.39f),palette[at],.34f);
            for(int i=0;i<(hill ? 5 : 3);i++)
            {
                int key=seed+i*337;
                float radius=(i==0 ? .39f : .15f)*(1+Noise01(key+28)*.66f)*(hill ? 1.44f : 1);
                float angle=Noise01(key+47)*Mathf.PI*2;
                var p=point+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(i*.22f);
                int footing=LandscapeIndex(p);
                if(protectedRock[footing] || water[footing]>.02f || bare[footing]>.18f)continue;
                var bottom=new Vector3(p.x,GroundHeight(p.x,p.y)-radius*.15f,p.y);
                var top=bottom+new Vector3(Mathf.Cos(angle)*radius*.29f,radius*(.51f+Noise01(key+37)*.47f),
                    Mathf.Sin(angle)*radius*.28f);
                Loft(vertices,colors,faces,bottom,top,radius,radius*.53f,rock*(.84f+Noise01(key+7)*.26f),7,key,.23f);
            }
        }
        var tints=new List<Color32>(colors.Count);
        foreach(var color in colors)tints.Add(color);
        AddLandscapeMesh("Kaya kümeleri",vertices,tints,faces,groundDetailMaterial);
    }

    Mesh AddLandscapeMesh(string label,List<Vector3> vertices,List<Color32> colors,List<int> faces,Material material,
        bool deformable=false)
    {
        if(vertices.Count==0)return null;
        var mesh=new Mesh { name=label,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32 };
        if(deformable)mesh.MarkDynamic();
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(faces,0);
        if(!deformable)mesh.UploadMeshData(true);
        landscapeMeshes.Add(mesh);
        var host=new GameObject(label);
        host.layer=Layer;
        host.transform.SetParent(landRoot,false);
        host.AddComponent<MeshFilter>().sharedMesh=mesh;
        host.AddComponent<MeshRenderer>().sharedMaterial=material;
        return mesh;
    }

    void ReleaseLandscape()
    {
        ReleaseGrassTrampling();
        foreach(var mesh in landscapeMeshes)Destroy(mesh);
        landscapeMeshes.Clear();
        if(groundDetailMaterial)Destroy(groundDetailMaterial);
        groundDetailMaterial=null;
    }
}
