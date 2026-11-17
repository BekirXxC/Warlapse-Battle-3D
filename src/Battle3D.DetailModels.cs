using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class Battle3D
{
    readonly Dictionary<string,Mesh> detailShapes=new Dictionary<string,Mesh>();

    void ReleaseDetailShapes()
    {
        foreach(var mesh in detailShapes.Values) Destroy(mesh);
        detailShapes.Clear();
    }

    Transform Shape(Transform parent,string label,string profile,Color color,Vector3 pos,Vector3 dimensions)
    {
        Mesh mesh;
        if (!detailShapes.TryGetValue(profile,out mesh))
        {
            var v=new List<Vector3>(); var c=new List<Color>(); var f=new List<int>();
            if (profile=="gable")
            {
                var a=new Vector3(-.5f,-.5f,-.5f); var b=new Vector3(.5f,-.5f,-.5f);
                var d=new Vector3(-.5f,-.5f,.5f); var e=new Vector3(.5f,-.5f,.5f);
                var ridgeA=new Vector3(0,.5f,-.5f); var ridgeB=new Vector3(0,.5f,.5f);
                Face(v,c,f,a,ridgeA,b,Color.white,1); Face(v,c,f,d,e,ridgeB,Color.white,2);
                Face(v,c,f,a,d,ridgeA,Color.white,3); Face(v,c,f,d,ridgeB,ridgeA,Color.white,3);
                Face(v,c,f,b,ridgeA,e,Color.white,4); Face(v,c,f,e,ridgeA,ridgeB,Color.white,4);
                Face(v,c,f,a,b,d,Color.white,5); Face(v,c,f,b,e,d,Color.white,5);
            }
            else
            {
                float[] heights, radii;
                int sides=8;
                switch(profile)
                {
                    case "helmet": heights=new[]{-.5f,-.32f,-.11f,.19f,.38f,.50f}; radii=new[]{.45f,.50f,.49f,.44f,.35f,.18f}; sides=12; break;
                    case "head": heights=new[]{-.50f,-.38f,-.18f,.11f,.34f,.50f}; radii=new[]{.26f,.35f,.45f,.50f,.45f,.29f}; sides=12; break;
                    case "skirt": heights=new[]{-.5f,-.38f,.50f}; radii=new[]{.50f,.49f,.34f}; break;
                    case "torso": heights=new[]{-.5f,-.30f,.29f,.50f}; radii=new[]{.38f,.42f,.50f,.40f}; break;
                    case "shield": heights=new[]{-.50f,-.28f,.31f,.50f}; radii=new[]{.20f,.47f,.50f,.43f}; sides=6; break;
                    case "cone": heights=new[]{-.50f,-.30f,.50f}; radii=new[]{.44f,.50f,.025f}; sides=7; break;
                    case "round": heights=new[]{-.5f,-.28f,.28f,.50f}; radii=new[]{.34f,.50f,.50f,.34f}; break;
                    case "hoof": heights=new[]{-.5f,.25f,.5f}; radii=new[]{.48f,.44f,.35f}; sides=6; break;
                    default: heights=new[]{-.5f,.5f}; radii=new[]{.50f,.39f}; sides=8; break;
                }
                for(int i=0;i<sides;i++)
                {
                    float a=(i+.5f)*Mathf.PI*2/sides,b=a+Mathf.PI*2/sides;
                    var left=new Vector3(Mathf.Cos(a),0,-Mathf.Sin(a));
                    var right=new Vector3(Mathf.Cos(b),0,-Mathf.Sin(b));
                    for(int j=0;j<heights.Length-1;j++)
                    {
                        var u=left*radii[j]+Vector3.up*heights[j];
                        var w=right*radii[j]+Vector3.up*heights[j];
                        var x=left*radii[j+1]+Vector3.up*heights[j+1];
                        var y=right*radii[j+1]+Vector3.up*heights[j+1];
                        Face(v,c,f,u,w,x,Color.white,3+i); Face(v,c,f,x,w,y,Color.white,3+i);
                    }
                    int last=heights.Length-1;
                    var floor=Vector3.up*heights[0]; var ceiling=Vector3.up*heights[last];
                    Face(v,c,f,floor,right*radii[0]+floor,left*radii[0]+floor,Color.white,1);
                    Face(v,c,f,ceiling,left*radii[last]+ceiling,right*radii[last]+ceiling,Color.white,2);
                }
            }
            mesh=new Mesh{name=profile};
            mesh.SetVertices(v); mesh.SetColors(c); mesh.SetTriangles(f,0);
            detailShapes.Add(profile,mesh);
        }
        var part=Part(parent,label,color,pos,dimensions);
        part.GetComponent<MeshFilter>().sharedMesh=mesh;
        return part;
    }

    static string SoldierKit(string id,string role,int era)
    {
        if(role=="Crew") return "Crew";
        if(id.Contains("crossbow")) return "Crossbow";
        if(id.Contains("archer")||id.Contains("bow")||id.Contains("slinger")) return "Archer";
        if(id.Contains("pikeman")||id.Contains("spear")||id.Contains("lancer")||id.Contains("halberd")) return "Pike";
        if(id.Contains("riotshield")||id.Contains("scrapshield")) return "Riot";
        if(id.Contains("robot")||id.Contains("plasmalance")||id.Contains("blaster")) return "Plasma";
        if(id.Contains("flamethrower")) return "Flame";
        if(id.Contains("knight")||id.Contains("guard")||id.Contains("heavy")||id.Contains("hoplite")) return "Guard";
        if(id.Contains("engineer")||id.Contains("medic")||role=="Support") return "Support";
        if(id.Contains("sniper")||id.Contains("scout")||id.Contains("skirmish")) return "Scout";
        if(era<=2 && role=="Ranged") return "Archer";
        if(id.Contains("grenadier")||id.Contains("musket")||id.Contains("rifle")||
            id.Contains("dragoon")||role=="Ranged"||era>=4) return "Rifle";
        if(id.Contains("sword")||id.Contains("axe")) return "Blade";
        return era<=2 ? "Blade" : "Rifle";
    }

    void Human(Transform parent,Color uniform,string kit,Outfit outfit,int variant,
        out Transform[] legs,out Transform[] knees,out Transform[] arms)
    {
        bool old=outfit<=Outfit.Gunpowder;
        bool lowTech=outfit<=Outfit.Medieval;
        var team=uniform;
        var neutral=old ? new Color(.45f,.39f,.29f) : new Color(.39f,.40f,.31f);
        uniform=Color.Lerp(neutral,uniform,old ? .63f : .45f);
        var skin=Color.Lerp(new Color(.79f,.57f,.42f),new Color(.46f,.30f,.21f),variant*.44f);
        var boot=new Color(.18f,.15f,.12f);
        var iron=Color.Lerp(new Color(.49f,.53f,.55f),uniform,.16f);
        var wood=new Color(.31f,.20f,.11f);
        var cloth=Color.Lerp(uniform,new Color(.39f,.40f,.32f),.34f);
        var strap=old ? wood : Color.Lerp(wood,cloth,.45f);
        legs=new Transform[2]; knees=new Transform[2]; arms=new Transform[2];
        for(int i=0;i<2;i++)
        {
            int side=i==0 ? -1 : 1;
            var leg=legs[i]=Joint(parent,"Kalça",new Vector3(.12f*side,.72f,0));
            var knee=knees[i]=Joint(leg,"Diz",new Vector3(0,-.30f,0));
            var arm=arms[i]=Joint(parent,"Omuz",new Vector3(.26f*side,1.17f,.005f));
            Shape(leg,"Pantolon","torso",cloth*.74f,new Vector3(0,-.151f,0),new Vector3(.19f,.36f,.22f));
            Shape(knee,"Baldır","round",outfit==Outfit.Hide||outfit==Outfit.Bronze ? skin : cloth*.65f,new Vector3(0,-.139f,0),new Vector3(.16f,.32f,.18f));
            if((outfit==Outfit.Medieval && kit=="Guard") || outfit==Outfit.Modern || outfit==Outfit.Future)
                Shape(knee,"Diz koruması","round",old ? iron*.77f : cloth*.53f,new Vector3(0,-.015f,.088f),new Vector3(.127f,.12f,.065f));
            if(outfit==Outfit.GreatWar || outfit==Outfit.Gunpowder)
                Shape(knee,"Bacak sargısı","taper",strap,new Vector3(0,-.200f,.018f),new Vector3(.168f,.247f,.174f));
            Shape(knee,"Bot","hoof",boot,new Vector3(0,-.337f,.049f),new Vector3(.19f,.145f,.28f));
            if((outfit==Outfit.Medieval && kit=="Guard") || outfit==Outfit.Future)
                Shape(arm,"Omuz zırhı","round",iron,new Vector3(0,-.04f,0),new Vector3(.208f,.18f,.24f));
            else if(outfit==Outfit.Gunpowder)
                Part(arm,"Omuz apoleti",Color.Lerp(team,iron,.38f),new Vector3(0,.035f,.008f),new Vector3(.169f,.068f,.192f));
            Part(arm,"Birlik işareti",team*.91f,new Vector3(side*.087f,-.109f,.018f),new Vector3(.022f,.060f,.087f));
            Shape(arm,"Kol","torso",outfit==Outfit.Hide||outfit==Outfit.Bronze ? skin : cloth,new Vector3(0,-.21f,.018f),new Vector3(.155f,.32f,.18f));
            Shape(arm,"Bilek","round",strap,new Vector3(0,-.348f,.075f),new Vector3(.127f,.103f,.14f));
            Shape(arm,"El","round",old ? skin : boot,new Vector3(0,-.435f,.115f),new Vector3(.12f,.13f,.14f));
        }
        Shape(parent,"Üniforma","torso",uniform,new Vector3(0,.986f,0),new Vector3(.52f,.51f,.34f));
        Shape(parent,"Kemer","torso",strap,new Vector3(0,.776f,0),new Vector3(.50f,.085f,.35f));
        Part(parent,"Kemer tokası",iron,new Vector3(0,.779f,.165f),new Vector3(.064f,.044f,.029f));
        Shape(parent,"Baş","head",skin,new Vector3(0,1.384f,.025f),new Vector3(.255f,.285f,.24f));
        DressSoldier(parent,outfit,kit,variant,team,uniform,cloth*.74f,iron,strap,boot);
        if(outfit==Outfit.Hide)
            Shape(parent,"Saç","head",wood*.70f,new Vector3(0,1.488f,-.031f),new Vector3(.261f,.144f,.237f));
        for(int i=-1;i<=1;i+=2)
        {
            Shape(parent,"Kulak","round",skin*.86f,new Vector3(i*.118f,1.385f,.02f),new Vector3(.041f,.075f,.054f));
            Part(parent,"Göz çevresi",skin*.79f,new Vector3(i*.053f,1.409f,.134f),new Vector3(.046f,.026f,.016f));
            Part(parent,"Göz",boot,new Vector3(i*.053f,1.407f,.144f),new Vector3(.022f,.016f,.013f));
            Part(parent,"Kaş",strap*.82f,new Vector3(i*.054f,1.432f,.14f),new Vector3(.035f,.009f,.017f));
            Part(parent,"Kemer çantası",strap*.84f,new Vector3(i*.139f,.808f,.152f),new Vector3(.108f,.122f,.088f));
        }
        Shape(parent,"Burun","round",skin*.89f,new Vector3(0,1.38f,.141f),new Vector3(.059f,.051f,.047f));
        if(variant==2) Part(parent,"Sakal",wood*.69f,new Vector3(0,1.314f,.098f),new Vector3(.148f,.059f,.086f));
        if(outfit>=Outfit.Gunpowder && outfit!=Outfit.Street)
        {
            Shape(parent,"Sırt teçhizatı","torso",strap,new Vector3(0,.962f,-.209f),new Vector3(.35f,.34f,.18f));
            Part(parent,"Sırt çantası bağı",strap*.78f,new Vector3(0,.972f,-.309f),new Vector3(.033f,.253f,.032f));
        }
        if(kit=="Crossbow")
        {
            var bow=Joint(arms[1],"Arbalet",new Vector3(-.018f,-.42f,.23f));
            Part(bow,"Arbalet gövdesi",wood,new Vector3(0,0,.145f),new Vector3(.061f,.078f,.48f));
            for(int side=-1;side<=1;side+=2)
                Part(bow,"Arbalet yayı",iron,new Vector3(side*.14f,.017f,.283f),new Vector3(.30f,.033f,.043f))
                    .localRotation=Quaternion.Euler(0,side*23,0);
            Part(bow,"Kiriş",strap*.55f,new Vector3(0,.013f,.229f),new Vector3(.48f,.012f,.012f));
        }
        else if(kit=="Archer")
        {
            var bow=Joint(arms[1],"Yay",new Vector3(-.025f,-.40f,.20f));
            for(int i=-2;i<=2;i++)
                Part(bow,"Yay kolu",wood,new Vector3(0,i*.16f,.155f-i*i*.034f),new Vector3(.041f,.189f,.045f))
                    .localRotation=Quaternion.Euler(-i*22,0,0);
            Part(bow,"Kiriş",strap*.49f,new Vector3(0,0,.009f),new Vector3(.009f,.73f,.009f));
            Shape(parent,"Sadak","round",wood,new Vector3(.15f,1.075f,-.29f),new Vector3(.132f,.46f,.13f))
                .localRotation=Quaternion.Euler(0,0,-20);
            for(int i=0;i<3;i++) Part(parent,"Ok tüyü",iron,new Vector3(.215f+i*.018f,1.32f-i*.022f,-.29f),new Vector3(.013f,.18f,.027f));
        }
        else if(kit=="Pike" || (lowTech && (kit=="Blade"||kit=="Guard"||kit=="Scout")))
        {
            bool pike=kit=="Pike";
            if(lowTech)
            {
                Shape(arms[0],"Kalkan","shield",uniform*.86f,new Vector3(-.014f,-.25f,.24f),
                    new Vector3(.33f,.45f,.092f));
                Shape(arms[0],"Kalkan göbeği","round",iron,new Vector3(-.014f,-.22f,.284f),new Vector3(.103f,.103f,.07f));
            }
            Part(arms[1],pike ? "Mızrak sapı" : "Kılıç kabzası",wood,new Vector3(0,-.29f,.19f),new Vector3(.038f,pike ? 1.26f : .24f,.038f));
            if(!pike) Part(arms[1],"Balçak",wood,new Vector3(0,-.173f,.19f),new Vector3(.16f,.038f,.045f));
            Shape(arms[1],"Çelik uç","cone",iron,new Vector3(0,pike ? .402f : .119f,.19f),
                new Vector3(pike ? .082f : .10f,pike ? .23f : .60f,.032f));
        }
        else if(kit=="Crew"||kit=="Support")
        {
            Shape(arms[1],"El aleti","taper",wood,new Vector3(0,-.21f,.19f),new Vector3(.054f,.58f,.055f));
            Part(arms[1],"Alet başı",iron,new Vector3(0,.077f,.19f),new Vector3(.158f,.125f,.048f));
        }
        else if(kit=="Riot")
        {
            Part(arms[0],"Kalkan",iron*.76f,new Vector3(-.018f,-.24f,.247f),new Vector3(.30f,.59f,.053f));
            Part(arms[0],"Görüş penceresi",cloth*.68f,new Vector3(-.018f,-.07f,.276f),new Vector3(.20f,.075f,.021f));
            Part(arms[1],"Cop",boot,new Vector3(0,-.25f,.186f),new Vector3(.055f,.52f,.061f));
        }
        else
        {
            bool musket=outfit==Outfit.Gunpowder;
            var gun=Joint(arms[1],"Tüfek",new Vector3(-.028f,-.339f,.235f));
            gun.localRotation=Quaternion.Euler(-11,0,0);
            Shape(gun,"Dipçik","torso",musket ? wood : boot,new Vector3(0,-.018f,.02f),new Vector3(.11f,.105f,.35f));
            Part(gun,"Mekanizma",iron*.65f,new Vector3(0,.04f,.22f),new Vector3(.085f,.083f,.26f));
            Shape(gun,"Namlu","taper",iron*.62f,new Vector3(0,.049f,.43f),new Vector3(.043f,.047f,.32f))
                .localRotation=Quaternion.Euler(90,0,0);
            if(!musket) Part(gun,"Şarjör",boot,new Vector3(0,-.04f,.23f),new Vector3(.061f,.13f,.075f));
            Part(gun,"Nişangâh",boot,new Vector3(0,.095f,.31f),new Vector3(.057f,.055f,.085f));
            if(kit=="Plasma")
            {
                Part(gun,"Enerji hücresi",new Color(.32f,.82f,.85f),new Vector3(0,.04f,.30f),new Vector3(.104f,.039f,.226f));
            }
            if(kit=="Flame")
            {
                for(int i=-1;i<=1;i+=2)
                    Shape(parent,"Yakıt tankı","round",iron*.69f,new Vector3(i*.104f,1.007f,-.31f),new Vector3(.163f,.424f,.165f));
            }
        }
    }

    void MakeMachine(Model model,string id,Color tint,int era)
    {
        var hull=Color.Lerp(tint,new Color(.32f,.35f,.29f),.49f);
        var metal=new Color(.26f,.29f,.27f);
        var rubber=new Color(.13f,.14f,.13f);
        var wood=new Color(.39f,.27f,.14f);
        var body=model.Root;
        if(model.Kind==Kind.Artillery)
        {
            bool engine=id.Contains("catapult")||id.Contains("trebuchet")||id.Contains("ballista");
            Part(body,"Top arabası",wood,new Vector3(0,.34f,-.11f),new Vector3(.64f,.20f,1.26f));
            for(int side=-1;side<=1;side+=2)
            {
                var wheel=Shape(body,"Teker","round",wood*.69f,new Vector3(.40f*side,.312f,-.1f),new Vector3(.62f,.13f,.62f));
                wheel.localRotation=Quaternion.Euler(0,0,90);
                Part(body,"Teker göbeği",metal,new Vector3(side*.467f,.312f,-.10f),new Vector3(.065f,.136f,.137f));
                Part(body,"Taşıyıcı",wood*.77f,new Vector3(side*.235f,.45f,-.39f),new Vector3(.105f,.11f,.83f));
            }
            if(engine)
            {
                Part(body,"Ahşap sehpa",wood,new Vector3(0,.69f,-.11f),new Vector3(.51f,.58f,.33f));
                Part(body,"Fırlatma kolu",wood*.78f,new Vector3(0,.96f,.07f),new Vector3(.12f,.145f,1.16f))
                    .localRotation=Quaternion.Euler(-42,0,0);
                Shape(body,"Taş","round",metal,new Vector3(0,.61f,-.45f),Vector3.one*.28f);
            }
            else
            {
                var gun=Joint(body,"Namlu düzeneği",new Vector3(0,.59f,.16f));
                gun.localRotation=Quaternion.Euler(-13,0,0);
                Shape(gun,"Top namlusu","taper",metal,new Vector3(0,0,.14f),new Vector3(.24f,1.16f,.24f))
                    .localRotation=Quaternion.Euler(90,0,0);
                Shape(gun,"Namlu ağzı","round",metal*.74f,new Vector3(0,0,.73f),new Vector3(.25f,.125f,.25f))
                    .localRotation=Quaternion.Euler(90,0,0);
                if(era>=5) Part(body,"Topçu siperi",hull,new Vector3(0,.65f,.03f),new Vector3(.65f,.43f,.071f));
            }
            return;
        }
        if(model.Kind==Kind.Aircraft)
        {
            bool rotor=id.Contains("heli")||id.Contains("helo")||id.Contains("rotor");
            if(id.Contains("dragon")||id.Contains("eagle"))
            {
                bool dragon=id.Contains("dragon");
                var hide=Color.Lerp(tint,dragon ? new Color(.36f,.22f,.16f) : new Color(.40f,.28f,.16f),.58f);
                Shape(body,"Uçan hayvan gövdesi","round",hide,new Vector3(0,.42f,0),new Vector3(.46f,.41f,.94f));
                Shape(body,"Baş","head",hide*1.19f,new Vector3(0,.57f,.43f),new Vector3(.27f,.30f,.32f));
                Shape(body,"Gaga","cone",new Color(.72f,.55f,.26f),new Vector3(0,.548f,.64f),new Vector3(.136f,.31f,.125f))
                    .localRotation=Quaternion.Euler(90,0,0);
                for(int side=-1;side<=1;side+=2)
                {
                    var wing=Part(body,dragon ? "Zar kanat" : "Kanat",hide*.88f,new Vector3(side*.62f,.45f,-.039f),new Vector3(1.10f,.077f,.58f));
                    wing.localRotation=Quaternion.Euler(0,side*-19,side*13);
                    for(int i=0;i<4;i++)
                        Shape(wing,"Kanat ucu","cone",hide*(.85f+i*.05f),new Vector3(side*(.09f+i*.113f),0,-.48f),
                            new Vector3(.14f,.46f,.73f)).localRotation=Quaternion.Euler(-90,0,0);
                }
                Shape(body,"Kuyruk","cone",hide*.83f,new Vector3(0,.37f,-.61f),
                    new Vector3(.17f,dragon ? .86f : .49f,.23f)).localRotation=Quaternion.Euler(-90,0,0);
                return;
            }
            if(id.Contains("zeppelin"))
            {
                Shape(body,"Balon","round",hull*1.20f,new Vector3(0,.93f,0),new Vector3(.79f,.81f,2.26f));
                Part(body,"Gondol",metal,new Vector3(0,.41f,.21f),new Vector3(.32f,.27f,.61f));
                Part(body,"Kuyruk dümeni",metal,new Vector3(0,1.14f,-.90f),new Vector3(.086f,.43f,.38f));
                return;
            }
            Shape(body,"Uçak gövdesi","round",hull,new Vector3(0,.44f,0),new Vector3(.48f,.39f,1.66f));
            Shape(body,"Kokpit camı","helmet",new Color(.33f,.53f,.61f),new Vector3(0,.64f,.23f),new Vector3(.32f,.25f,.47f));
            Part(body,rotor ? "Rotor" : "Kanat",hull,new Vector3(0,rotor ? .84f : .43f,.03f),new Vector3(2.08f,.071f,rotor ? .122f : .44f));
            Part(body,"Kuyruk kanadı",hull*.91f,new Vector3(0,.48f,-.66f),new Vector3(.77f,.077f,.33f));
            Part(body,"Dikey dengeleyici",hull*.93f,new Vector3(0,.65f,-.72f),new Vector3(.066f,.40f,.29f))
                .localRotation=Quaternion.Euler(-22,0,0);
            return;
        }
        bool wheeled=id.Contains("truck")||id.Contains("car");
        Part(body,"Alt şasi",hull*.73f,new Vector3(0,.29f,0),new Vector3(.80f,.24f,1.51f));
        Shape(body,"Zırhlı gövde","torso",hull,new Vector3(0,.47f,.012f),new Vector3(.96f,.30f,1.59f));
        Shape(body,wheeled ? "Sürücü kabini" : "Taret","round",hull*.86f,
            new Vector3(0,.67f,wheeled ? .34f : -.095f),new Vector3(.62f,.39f,.61f));
        if(wheeled)
            Part(body,"Ön cam",new Color(.35f,.49f,.53f),new Vector3(0,.718f,.602f),new Vector3(.45f,.147f,.056f));
        else
        {
            Shape(body,"Namlu","taper",metal,new Vector3(0,.718f,.455f),new Vector3(.111f,1.03f,.115f))
                .localRotation=Quaternion.Euler(90,0,0);
            Part(body,"Komutan kapağı",metal,new Vector3(.14f,.877f,-.21f),new Vector3(.19f,.064f,.22f));
        }
        for(int side=-1;side<=1;side+=2)
        {
            if(!wheeled) Part(body,"Palet",rubber,new Vector3(side*.47f,.205f,0),new Vector3(.23f,.29f,1.61f));
            int wheels=wheeled ? 3 : 5;
            for(int i=0;i<wheels;i++)
                Shape(body,"Teker","round",wheeled ? rubber : metal,new Vector3(side*.51f,.204f,-.60f+i*1.20f/(wheels-1)),
                    new Vector3(wheeled ? .37f : .26f,.19f,wheeled ? .37f : .26f))
                    .localRotation=Quaternion.Euler(0,0,90);
            Part(body,"Ön lamba",new Color(.86f,.74f,.48f),new Vector3(side*.31f,.443f,.739f),new Vector3(.119f,.07f,.047f));
        }
    }
}
