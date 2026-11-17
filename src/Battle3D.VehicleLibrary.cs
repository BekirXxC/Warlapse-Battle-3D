using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using UnityEngine;

public sealed partial class Battle3D
{
    sealed class VehicleGeometry
    {
        public Vector3[] Vertices, Normals;
        public Color32[] Colors;
        public int[] Faces;
    }

    Dictionary<string,VehicleGeometry> vehicleLibrary;

    void LoadVehicleLibrary()
    {
        vehicleLibrary=new Dictionary<string,VehicleGeometry>(StringComparer.Ordinal);
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Warlapse.Vehicles"))
        {
            if(stream==null)return;
            using(var zipped=new GZipStream(stream,CompressionMode.Decompress))
            using(var reader=new BinaryReader(zipped))
            {
                if(Encoding.ASCII.GetString(reader.ReadBytes(4))!="W3V1")
                    throw new InvalidDataException("Unexpected vehicle library format.");
                int models=reader.ReadInt32();
                if(models<0 || models>512)throw new InvalidDataException("Invalid vehicle count.");
                int[] sample=cube.triangles;
                Vector3[] sampleVertices=cube.vertices;
                bool reverse=Vector3.Dot(Vector3.Cross(sampleVertices[sample[1]]-sampleVertices[sample[0]],
                    sampleVertices[sample[2]]-sampleVertices[sample[0]]),cube.normals[sample[0]])<0;
                for(int i=0;i<models;i++)
                {
                    string name=Encoding.ASCII.GetString(reader.ReadBytes(reader.ReadByte()));
                    int count=reader.ReadInt32();
                    if(count<3 || count>90000 || count%3!=0)
                        throw new InvalidDataException("Invalid vehicle geometry for "+name);
                    var entry=new VehicleGeometry { Vertices=new Vector3[count], Normals=new Vector3[count],
                        Colors=new Color32[count], Faces=new int[count] };
                    for(int v=0;v<count;v++)
                    {
                        entry.Vertices[v]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                        entry.Normals[v]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                        entry.Colors[v]=new Color32(reader.ReadByte(),reader.ReadByte(),reader.ReadByte(),reader.ReadByte());
                    }
                    for(int v=0;v<count;v+=3)
                    {
                        entry.Faces[v]=v;
                        entry.Faces[v+1]=v+(reverse ? 2 : 1);
                        entry.Faces[v+2]=v+(reverse ? 1 : 2);
                    }
                    vehicleLibrary.Add(name,entry);
                }
            }
        }
    }

    bool PlaceVehicle(Transform parent,string id,Color tint)
    {
        if(vehicleLibrary==null)
        {
            try{LoadVehicleLibrary();}
            catch(Exception error)
            {
                vehicleLibrary.Clear();
                Debug.LogWarning("Warlapse 3D vehicle library unavailable: "+error.Message);
            }
        }
        VehicleGeometry design;
        if(!vehicleLibrary.TryGetValue(id,out design))return false;
        var key="detailed vehicle|"+id+"|"+(Color32)tint;
        Mesh mesh;
        if(!infantryMeshes.TryGetValue(key,out mesh))
        {
            var colors=new Color32[design.Vertices.Length];
            var sun=new Vector3(-.48f,.82f,.32f).normalized;
            for(int i=0;i<colors.Length;i++)
            {
                var normal=design.Normals[i];
                var shade=.53f+Mathf.Max(0,Vector3.Dot(sun,normal))*.46f;
                var light=Color.Lerp(new Color(.76f,.83f,.92f),new Color(1.07f,1.02f,.92f),shade);
                Color surface=design.Colors[i];
                if(design.Colors[i].a!=0)surface=Color.Lerp(surface,tint,.25f);
                colors[i]=(Color32)(surface*light*shade);
                colors[i].a=255;
            }
            mesh=new Mesh { name=key, indexFormat=colors.Length>65535 ?
                UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.vertices=design.Vertices;
            mesh.normals=design.Normals;
            mesh.colors32=colors;
            mesh.triangles=design.Faces;
            infantryMeshes.Add(key,mesh);
        }
        AttachMesh(parent,mesh);
        return true;
    }
}
