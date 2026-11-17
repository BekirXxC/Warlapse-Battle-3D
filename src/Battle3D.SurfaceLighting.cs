using System.Collections.Generic;
using UnityEngine;

public sealed partial class Battle3D
{
    readonly Dictionary<Vector3Int,float> surfaceVisibility=new Dictionary<Vector3Int,float>();

    void UpdateSurfaceLighting()
    {
        if(Time.unscaledTime>=nextLightScan)
        {
            nextLightScan=Time.unscaledTime+.65f;
            RefreshLitSurfaces();
        }
        int remaining=10000;
        surfaceVisibility.Clear();
        int checkedCount=0;
        while(lightingQueue.Count>0 && remaining>0 && checkedCount<lightingQueue.Count)
        {
            if(lightCursor>=lightingQueue.Count)lightCursor=0;
            var surface=lightingQueue[lightCursor++];
            checkedCount++;
            if(!surface.Filter || !surface.Filter.gameObject.activeInHierarchy ||
                surface.Filter.sharedMesh!=surface.Shaded)continue;
            var transform=surface.Filter.transform;
            if(Time.unscaledTime-surface.LastShade<1.1f &&
                (transform.position-surface.LastPosition).sqrMagnitude<.0004f &&
                Quaternion.Angle(transform.rotation,surface.LastRotation)<.6f &&
                (transform.lossyScale-surface.LastScale).sqrMagnitude<.0001f &&
                (view.transform.position-surface.LastEye).sqrMagnitude<.0225f &&
                Mathf.Abs(stormLight-surface.LastStorm)<.025f)continue;
            ShadeSurface(surface);
            remaining-=surface.Geometry.Points.Length;
        }
    }

    void RefreshLitSurfaces()
    {
        var expired=new List<MeshFilter>();
        foreach(var entry in litSurfaces)
        {
            var filter=entry.Key;
            if(!filter || !filter.gameObject.activeInHierarchy || filter.sharedMesh!=entry.Value.Shaded ||
                (filter.transform.position-view.transform.position).sqrMagnitude>55*55)
            {
                RestoreSurface(entry.Value);
                expired.Add(filter);
            }
        }
        foreach(var item in expired)litSurfaces.Remove(item);
        var filters=new List<MeshFilter>(root.GetComponentsInChildren<MeshFilter>());
        var eye=view.transform.position;
        filters.Sort((a,b)=>(a.transform.position-eye).sqrMagnitude.CompareTo((b.transform.position-eye).sqrMagnitude));
        int vertices=0;
        foreach(var filter in filters)
        {
            if(vertices>160000)break;
            SurfaceLight found;
            if(litSurfaces.TryGetValue(filter,out found)){vertices+=found.Geometry.Points.Length;continue;}
            var mesh=filter.sharedMesh;
            var renderer=filter.GetComponent<MeshRenderer>();
            if(!mesh || !renderer || !renderer.enabled || !mesh.isReadable || mesh.vertexCount>38000 ||
                mesh.vertexCount==0 || mesh.name=="Çimen ve kır çiçekleri" || mesh.name=="Pencere ışığı" ||
                mesh==shadowReceiver || mesh==bloodMesh || mesh==debrisMesh ||
                filter.gameObject.layer!=Layer || skyMeshes.Contains(mesh) ||
                mesh==terrainMesh || mesh==contactMesh || mesh==rainMesh || mesh==waterMesh || mesh==hazeMesh || mesh==boltMesh ||
                (filter.transform.position-eye).sqrMagnitude>48*48)continue;
            LightGeometry geometry;
            if(!lightGeometry.TryGetValue(mesh,out geometry))
            {
                var normals=mesh.normals;
                if(normals.Length!=mesh.vertexCount)
                {
                    var temporary=Instantiate(mesh);
                    temporary.RecalculateNormals();
                    normals=temporary.normals;
                    Destroy(temporary);
                }
                var colors=mesh.colors32;
                if(colors.Length!=mesh.vertexCount)continue;
                string label=mesh.name.ToLowerInvariant();
                geometry=new LightGeometry { Points=mesh.vertices,Normals=normals,Colors=colors,
                    MetalParts=label.StartsWith("infantry|") || label.StartsWith("horse|") ||
                        label.StartsWith("held|") || label.Contains("vehicle") };
                lightGeometry.Add(mesh,geometry);
            }
            var shaded=Instantiate(mesh);
            shaded.name=mesh.name+" | gelişmiş ışık";
            shaded.MarkDynamic();
            var item=new SurfaceLight{Filter=filter,Original=mesh,Shaded=shaded,
                Geometry=geometry,Colors=new Color32[mesh.vertexCount]};
            litSurfaces.Add(filter,item);
            filter.sharedMesh=shaded;
            ShadeSurface(item);
            vertices+=mesh.vertexCount;
        }
        lightingQueue.Clear();
        foreach(var surface in litSurfaces.Values)lightingQueue.Add(surface);
    }

    void ShadeSurface(SurfaceLight surface)
    {
        var matrix=surface.Filter.transform.localToWorldMatrix;
        var normals=matrix.inverse.transpose;
        var geometry=surface.Geometry;
        var eye=view.transform.position;
        var position=MapPoint(surface.Filter.transform.position);
        float ground=GroundHeight(position.x,position.z);
        float wet=rainMesh ? .22f : 0;
        for(int i=0;i<geometry.Points.Length;i++)
        {
            var world=matrix.MultiplyPoint3x4(geometry.Points[i]);
            var normal=normals.MultiplyVector(geometry.Normals[i]).normalized;
            var looking=(eye-world).normalized;
            float lambert=Mathf.Max(0,Vector3.Dot(normal,sunDirection));
            float visible=lambert>.06f ? SurfaceSunVisibility(world,normal) : 1;
            Color original=geometry.Colors[i];
            float high=Mathf.Max(original.r,Mathf.Max(original.g,original.b));
            float low=Mathf.Min(original.r,Mathf.Min(original.g,original.b));
            float metallic=geometry.MetalParts ? Mathf.Clamp01(1-(high-low)*9)*Mathf.InverseLerp(.12f,.46f,high) : 0;
            var halfAngle=(looking+sunDirection).normalized;
            float specular=Mathf.Pow(Mathf.Max(0,Vector3.Dot(normal,halfAngle)),metallic>.3f ? 28 : 16);
            float rim=Mathf.Pow(1-Mathf.Abs(Vector3.Dot(normal,looking)),4);
            float sky=Mathf.Clamp01(normal.y*.5f+.5f);
            float contact=Mathf.Clamp01((world.y-ground)*1.4f);
            float diffuse=.59f+sky*.13f+lambert*visible*sunContrast*.53f+contact*.07f;
            var tint=original*diffuse;
            tint+=new Color(.19f,.25f,.33f)*high*sky*(1-lambert*visible)*.14f;
            tint+=new Color(.91f,.96f,1)*(metallic*.20f+wet*.14f)*rim*(.45f+sky*.55f);
            tint+=new Color(1,.91f,.75f)*specular*visible*(.035f+metallic*.62f+wet*.23f)*(rainMesh ? .40f : 1);
            tint=Color.Lerp(tint,tint+new Color(.16f,.21f,.28f),stormLight*.43f);
            tint.a=original.a;
            surface.Colors[i]=tint;
        }
        surface.Shaded.colors32=surface.Colors;
        surface.LastPosition=surface.Filter.transform.position;
        surface.LastRotation=surface.Filter.transform.rotation;
        surface.LastScale=surface.Filter.transform.lossyScale;
        surface.LastEye=eye;
        surface.LastStorm=stormLight;
        surface.LastShade=Time.unscaledTime;
    }

    float SurfaceSunVisibility(Vector3 position,Vector3 normal)
    {
        var p=MapPoint(position+normal*.10f);
        var key=new Vector3Int(Mathf.RoundToInt(p.x*2.5f),Mathf.RoundToInt(p.y*3),Mathf.RoundToInt(p.z*2.5f));
        float result;
        if(surfaceVisibility.TryGetValue(key,out result))return result;
        result=1;
        var ray=new Vector3(sunDirection.x/MapScale,sunDirection.y,sunDirection.z/MapScale);
        for(float distance=.65f;distance<14;distance*=1.8f)
        {
            var at=p+ray*distance;
            if(RainSurface(at.x,at.z)>at.y+.11f){result=.18f;break;}
        }
        surfaceVisibility[key]=result;
        return result;
    }

    void RestoreSurface(SurfaceLight surface)
    {
        if(surface.Filter && surface.Filter.sharedMesh==surface.Shaded)
            surface.Filter.sharedMesh=surface.Original;
        if(surface.Shaded)Destroy(surface.Shaded);
    }
}
