using System.Collections.Generic;
using UnityEngine;

public sealed partial class Battle3D
{
    enum Fieldwork { None, Bank, Redoubt, Trench, Stakes, Wire, Barricade }

    static Fieldwork FieldworkFor(string id)
    {
        if(id.Contains("staked_ditch") || id.Contains("stake_line")) return Fieldwork.Stakes;
        if(id.Contains("barbedwire") || id.Contains("barbed_wire") || id.Contains("wire_fence")) return Fieldwork.Wire;
        if(id.Contains("trench") || id.Contains("ditch")) return Fieldwork.Trench;
        if(id.Contains("redoubt")) return Fieldwork.Redoubt;
        if(id.Contains("bastion") || id.Contains("rampart") || id.Contains("front_wall") ||
            id.Contains("frontline_wall") || id.Contains("field_wall") || id.Contains("field_fort") ||
            id.Contains("sandbag")) return Fieldwork.Bank;
        if(id.Contains("barricade")) return Fieldwork.Barricade;
        return Fieldwork.None;
    }

    static float FieldworkHeight(Fieldwork form,float width)
    {
        switch(form)
        {
            case Fieldwork.Wire: return Mathf.Clamp(width*.24f,.26f,.51f);
            case Fieldwork.Trench: return Mathf.Clamp(width*.28f,.27f,.57f);
            case Fieldwork.Stakes: return Mathf.Clamp(width*.31f,.28f,.63f);
            default: return Mathf.Clamp(width*.38f,.33f,.88f);
        }
    }

    void MakeFieldwork(Transform root,Fieldwork form,Color team)
    {
        var earth=Color.Lerp(new Color(.43f,.36f,.26f),team,.11f);
        var cut=earth*.70f;
        var sack=new Color(.63f,.55f,.40f);
        var wood=new Color(.31f,.23f,.15f);
        var iron=new Color(.26f,.28f,.27f);
        if(form==Fieldwork.Bank || form==Fieldwork.Redoubt)
        {
            bool redoubt=form==Fieldwork.Redoubt;
            // The long edge is along local Z. Its interior remains open behind the bank.
            EarthBank(root,earth,new Vector3(.16f,0,0),redoubt ? .56f : .64f,.98f,.66f);
            for(int i=0;i<6;i++)
            {
                float z=-.41f+i*.164f;
                Shape(root,"Set üstü kum torbası","round",sack*(.85f+Noise01(i*31)*.16f),
                    new Vector3(.19f,.687f,z),new Vector3(.245f,.15f,.177f));
                if(!redoubt)
                    Stake(root,wood,new Vector3(.41f,.16f,z),.39f,27);
            }
            if(redoubt)
                for(int side=-1;side<=1;side+=2)
                {
                    var wing=Joint(root,"Toprak tabya yanı",new Vector3(-.21f,0,side*.37f));
                    wing.localRotation=Quaternion.Euler(0,90,0);
                    EarthBank(wing,earth,Vector3.zero,.20f,.43f,.43f);
                    Shape(wing,"Tabya köşesi torbası","round",sack,new Vector3(0,.455f,.03f),new Vector3(.19f,.13f,.35f));
                }
            return;
        }
        if(form==Fieldwork.Barricade)
        {
            Part(root,"Barikat alt dolgusu",earth,new Vector3(0,.16f,0),new Vector3(.51f,.38f,.98f));
            for(int i=0;i<6;i++)
            {
                float z=-.405f+i*.163f;
                Part(root,"Barikat tahtası",wood*(.86f+Noise01(i*83)*.25f),
                    new Vector3(0,.32f,z),new Vector3(.13f,.65f,.14f)).localRotation=Quaternion.Euler(i%2*8,0,14);
                Shape(root,"Barikat torbası","round",sack,new Vector3(-.20f,.28f,z),new Vector3(.27f,.22f,.18f));
            }
            Part(root,"Barikat bağlantısı",wood*.79f,new Vector3(.11f,.49f,0),new Vector3(.12f,.104f,.97f));
            return;
        }
        if(form==Fieldwork.Wire)
        {
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*.27f;
                for(int i=0;i<4;i++)
                    Stake(root,wood,new Vector3(x,.33f,-.425f+i*.283f),.71f,side*9);
                for(int row=0;row<3;row++)
                    Part(root,"Gergi teli",iron,new Vector3(x,.21f+row*.17f,0),new Vector3(.019f,.021f,.99f));
                for(int i=0;i<8;i++)
                {
                    float z=-.43f+i*.121f;
                    Part(root,"Çapraz tel",iron,new Vector3(x,.36f,z),new Vector3(.021f,.023f,.19f))
                        .localRotation=Quaternion.Euler((i%2==0 ? 1 : -1)*53,0,0);
                }
            }
            return;
        }
        bool stakes=form==Fieldwork.Stakes;
        for(int side=-1;side<=1;side+=2)
        {
            EarthBank(root,earth,new Vector3(side*.36f,0,0),.26f,.99f,stakes ? .32f : .48f);
            Part(root,"Siper iç yüzü",cut,new Vector3(side*.231f,.16f,0),new Vector3(.043f,.32f,.97f));
            for(int i=0;i<6;i++)
            {
                float z=-.414f+i*.165f;
                if(stakes)
                    Stake(root,wood,new Vector3(side*.24f,.28f,z),.64f,side*22);
                else
                    Shape(root,"Siper kum torbası","round",sack*(.84f+Noise01(i*57+side)*.18f),
                        new Vector3(side*.352f,.50f,z),new Vector3(.19f,.16f,.178f));
            }
        }
    }

    void EarthBank(Transform parent,Color color,Vector3 at,float width,float length,float height)
    {
        var bank=Joint(parent,"Alçak toprak set",at);
        Part(bank,"Toprak altlığı",color*.77f,new Vector3(0,-.065f,0),new Vector3(width,.21f,length));
        Shape(bank,"Eğimli toprak gövdesi","gable",color,
            new Vector3(0,height*.45f,0),new Vector3(width,height*.93f,length));
        Part(bank,"Set sırtı",color*.94f,new Vector3(0,height*.86f,0),new Vector3(width*.37f,height*.17f,length*.97f));
    }

    void Stake(Transform parent,Color wood,Vector3 center,float height,float lean)
    {
        var post=Joint(parent,"Kazık",center);
        post.localRotation=Quaternion.Euler(0,0,lean);
        Part(post,"Ahşap kazık",wood,Vector3.zero,new Vector3(.062f,height*.83f,.059f));
        Shape(post,"Sivri kazık ucu","cone",wood*1.12f,new Vector3(0,height*.48f,0),
            new Vector3(.066f,height*.18f,.066f));
    }

    struct FieldPlacement
    {
        public Model Model;
        public Rect Footprint;
        public object Owner;
    }

    readonly List<FieldPlacement> fieldPlacements = new List<FieldPlacement>();
    int fieldSignature;

    void OrientFieldworks()
    {
        int signature=0;
        fieldPlacements.Clear();
        foreach(var pair in models)
        {
            var actor=pair.Key;
            var model=pair.Value;
            Rect footprint;
            object owner;
            if(!actor || model.Field==Fieldwork.None || !actor.gameObject.activeInHierarchy ||
                !TryFort(actor,out footprint,out owner)) continue;
            fieldPlacements.Add(new FieldPlacement { Model=model, Footprint=footprint, Owner=owner });
            signature=unchecked(signature*397 ^ actor.GetInstanceID() ^ footprint.GetHashCode() ^
                (owner==null ? 0 : owner.GetHashCode()));
        }
        if(signature==fieldSignature)return;
        fieldSignature=signature;
        foreach(var section in fieldPlacements)
        {
            var area=section.Footprint;
            var best=Vector2.zero;
            float maxReach=Mathf.Max(area.width,area.height)*1.75f;
            float distance=maxReach*maxReach;
            foreach(var candidate in fieldPlacements)
            {
                if(candidate.Model==section.Model || candidate.Model.Field!=section.Model.Field ||
                    !Equals(candidate.Owner,section.Owner))continue;
                var delta=candidate.Footprint.center-area.center;
                float score=delta.sqrMagnitude;
                if(score<.01f || score>=distance)continue;
                distance=score;
                best=delta;
            }
            if(best.sqrMagnitude<.01f)continue;
            // A line has no signed heading. Canonicalize it so adjacent cells face the same way.
            if(best.y<-.01f || Mathf.Abs(best.y)<=.01f && best.x<0)best=-best;
            section.Model.Root.localRotation=Quaternion.Euler(0,Mathf.Atan2(best.x,best.y)*Mathf.Rad2Deg,0);
        }
    }
}
