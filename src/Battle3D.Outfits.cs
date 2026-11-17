using System;
using UnityEngine;

public sealed partial class Battle3D
{
    enum Outfit { Hide, Bronze, Medieval, Gunpowder, GreatWar, WorldWar, Modern, Future, Street }

    static Outfit OutfitFor(int era,string id)
    {
        if(era>=6 && id.EndsWith("_fu7",StringComparison.Ordinal)) return Outfit.Future;
        if(era>=6 && id.EndsWith("_ur7",StringComparison.Ordinal)) return Outfit.Street;
        return (Outfit)Mathf.Clamp(era,0,(int)Outfit.Modern);
    }

    void DressSoldier(Transform body,Outfit outfit,string kit,int variant,Color team,Color fabric,
        Color darkFabric,Color metal,Color leather,Color boot)
    {
        var trim=Color.Lerp(team,new Color(.88f,.77f,.58f),.37f);
        var brass=new Color(.61f,.44f,.23f);
        bool armored=kit=="Guard"||kit=="Pike";
        switch(outfit)
        {
            case Outfit.Hide:
                Shape(body,"Deri tunik eteği","skirt",leather,new Vector3(0,.692f,0),new Vector3(.52f,.31f,.33f));
                var hide=Shape(body,"Kürk omuzluğu","torso",leather*.87f,new Vector3(-.10f,1.12f,.024f),
                    new Vector3(.37f,.29f,.31f));
                hide.localRotation=Quaternion.Euler(0,0,-17);
                Part(body,"Kemik toka",trim,new Vector3(-.077f,1.14f,.181f),new Vector3(.09f,.034f,.025f));
                break;
            case Outfit.Bronze:
                Shape(body,"Antik tunik eteği","skirt",fabric,new Vector3(0,.675f,0),new Vector3(.50f,.32f,.35f));
                Shape(body,"Göğüslük","torso",armored ? brass : trim*.83f,new Vector3(0,1.04f,.095f),
                    new Vector3(.41f,.37f,.23f));
                if(armored)
                    for(int i=-2;i<=2;i++)
                        Part(body,"Zırh eteği şeridi",leather,new Vector3(i*.077f,.70f,.171f),new Vector3(.058f,.21f,.037f));
                Shape(body,"Bronz miğfer","helmet",armored ? brass : fabric*.75f,new Vector3(0,1.52f,.019f),
                    new Vector3(.32f,.18f,.29f));
                for(int side=-1;side<=1;side+=2)
                    Part(body,"Yanaklık",armored ? brass : leather,new Vector3(side*.126f,1.444f,.095f),new Vector3(.048f,.108f,.108f));
                if(armored)
                    Shape(body,"Miğfer ibiği","round",team,new Vector3(0,1.646f,.007f),new Vector3(.074f,.133f,.25f));
                break;
            case Outfit.Medieval:
                Shape(body,"Surkot eteği","skirt",fabric,new Vector3(0,.683f,0),new Vector3(.50f,.32f,.34f));
                Shape(body,"Zincir yaka","round",metal*.73f,new Vector3(0,1.225f,.012f),new Vector3(.32f,.15f,.28f));
                Shape(body,"Göğüs levhası","torso",armored ? metal : darkFabric,new Vector3(0,1.026f,.107f),
                    new Vector3(.44f,.39f,.22f));
                Part(body,"Arma",team*.96f,new Vector3(.065f,1.067f,.222f),new Vector3(.11f,.121f,.025f));
                if(kit=="Guard")
                {
                    Shape(body,"Kapalı şövalye miğferi","taper",metal,new Vector3(0,1.471f,.022f),
                        new Vector3(.285f,.287f,.285f));
                    Part(body,"Vizör yarığı",boot,new Vector3(0,1.469f,.154f),new Vector3(.196f,.022f,.04f));
                }
                else
                {
                    Shape(body,"Konik miğfer","cone",armored ? metal : darkFabric,new Vector3(0,1.546f,.012f),
                        new Vector3(.32f,.222f,.31f));
                    Part(body,"Miğfer kenarı",metal*.81f,new Vector3(0,1.473f,.023f),new Vector3(.32f,.031f,.281f));
                }
                break;
            case Outfit.Gunpowder:
                for(int side=-1;side<=1;side+=2)
                {
                    Part(body,"Kuyruklu ceket",fabric,new Vector3(side*.117f,.712f,-.132f),new Vector3(.214f,.282f,.112f));
                    Part(body,"Dik yaka",trim,new Vector3(side*.067f,1.222f,.097f),new Vector3(.106f,.091f,.143f));
                    var belt=Part(body,"Çapraz beyaz kayış",trim,new Vector3(0,1.006f,.179f),new Vector3(.041f,.44f,.038f));
                    belt.localRotation=Quaternion.Euler(0,0,side*28);
                    for(int row=0;row<4;row++)
                        Part(body,"Pirinç düğme",brass,new Vector3(side*.074f,.884f+row*.073f,.205f),new Vector3(.027f,.027f,.027f));
                }
                Shape(body,"Şako","taper",boot,new Vector3(0,1.587f,.008f),new Vector3(.29f,.247f,.28f));
                Part(body,"Şako siperi",boot,new Vector3(0,1.477f,.126f),new Vector3(.232f,.031f,.128f));
                Shape(body,"Kokart","round",trim,new Vector3(0,1.583f,.151f),new Vector3(.069f,.069f,.035f));
                if(variant==2 || kit=="Crew")
                    Shape(body,"Şako ponponu","round",team,new Vector3(0,1.729f,.11f),new Vector3(.073f,.074f,.073f));
                break;
            case Outfit.GreatWar:
                Shape(body,"Yün tunik eteği","skirt",fabric,new Vector3(0,.738f,0),new Vector3(.47f,.22f,.34f));
                AddFieldKit(body,leather,darkFabric,brass,.18f);
                Shape(body,"Geniş çelik miğfer kenarı","round",darkFabric,new Vector3(0,1.48f,.028f),
                    new Vector3(.394f,.055f,.355f));
                Shape(body,"Alçak çelik miğfer","helmet",metal*.70f,new Vector3(0,1.534f,.019f),new Vector3(.30f,.143f,.29f));
                break;
            case Outfit.WorldWar:
                AddFieldKit(body,leather,darkFabric,brass,.18f);
                Shape(body,"Derin çelik miğfer","helmet",darkFabric,new Vector3(0,1.526f,.020f),
                    new Vector3(.338f,.193f,.325f));
                Part(body,"Miğfer siperliği",darkFabric,new Vector3(0,1.453f,.112f),new Vector3(.274f,.045f,.17f));
                Shape(body,"Rulo örtü","round",darkFabric,new Vector3(0,1.20f,-.220f),new Vector3(.395f,.105f,.11f));
                break;
            case Outfit.Modern:
                Shape(body,"Balistik yelek","torso",darkFabric,new Vector3(0,1.036f,.105f),
                    new Vector3(.449f,.423f,.27f));
                for(int pocket=-1;pocket<=1;pocket++)
                    Part(body,"Yelek cebi",leather,new Vector3(pocket*.122f,.927f,.241f),new Vector3(.103f,.125f,.065f));
                for(int row=0;row<3;row++)
                    Part(body,"Yelek bağı",fabric*.83f,new Vector3(0,1.003f+row*.041f,.23f),new Vector3(.352f,.018f,.028f));
                Shape(body,"Modern kompozit miğfer","helmet",darkFabric,new Vector3(0,1.526f,.016f),
                    new Vector3(.331f,.190f,.31f));
                Part(body,"Miğfer donanımı",boot,new Vector3(0,1.523f,.165f),new Vector3(.141f,.065f,.043f));
                break;
            case Outfit.Future:
                var plate=Color.Lerp(metal,team,.38f);
                Shape(body,"Segmentli göğüs zırhı","torso",plate,new Vector3(0,1.044f,.112f),new Vector3(.464f,.415f,.28f));
                for(int row=0;row<3;row++)
                    Part(body,"Zırh ek yeri",boot,new Vector3(0,.940f+row*.070f,.250f),new Vector3(.344f,.016f,.021f));
                Shape(body,"Kapalı zırhlı miğfer","head",plate,new Vector3(0,1.494f,.024f),new Vector3(.332f,.312f,.323f));
                Part(body,"Koyu vizör",boot,new Vector3(0,1.471f,.176f),new Vector3(.251f,.085f,.046f));
                Part(body,"Vizör ışık şeridi",Color.Lerp(team,new Color(.32f,.71f,.82f),.58f),
                    new Vector3(0,1.481f,.203f),new Vector3(.217f,.022f,.019f));
                break;
            case Outfit.Street:
                Part(body,"Sokak ceketi fermuarı",trim*.67f,new Vector3(0,1.00f,.179f),new Vector3(.026f,.365f,.027f));
                Shape(body,kit=="Riot" ? "Çevik kuvvet miğferi" : "Kumaş kapüşon","helmet",darkFabric,
                    new Vector3(0,1.510f,.005f),new Vector3(.329f,.222f,.32f));
                if(kit=="Riot")
                    Part(body,"Miğfer vizörü",metal*.43f,new Vector3(0,1.469f,.166f),new Vector3(.228f,.071f,.039f));
                break;
        }
    }

    void AddFieldKit(Transform body,Color leather,Color fabric,Color brass,float depth)
    {
        for(int side=-1;side<=1;side+=2)
        {
            Part(body,"Tunik cebi",fabric,new Vector3(side*.107f,1.042f,depth),new Vector3(.113f,.119f,.037f));
            Part(body,"Cep kapağı",fabric*.86f,new Vector3(side*.107f,1.091f,depth+.018f),new Vector3(.118f,.033f,.029f));
            Part(body,"Omuz askısı",leather,new Vector3(side*.13f,1.038f,depth+.026f),new Vector3(.038f,.37f,.035f));
        }
        for(int i=0;i<4;i++)
            Part(body,"Tunik düğmesi",brass,new Vector3(0,.875f+i*.069f,depth+.022f),new Vector3(.026f,.026f,.026f));
    }
}
