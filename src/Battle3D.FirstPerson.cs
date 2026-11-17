using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class Battle3D
{
    Transform heldModel;
    Person heldPerson;
    bool commanding,steering,eyeReady;
    float firstPersonAim,lastAttack=-10;
    float previousAttackCooldown, hitKick, movementTilt;
    int previousHitTick;
    Func<bool> cameraShakeEnabled;
    Vector2 heldSway;
    Vector3 steadyEye;
    MethodInfo submitOrder,attackOrder,fogSeen;

    bool MayCommand(Person person)
    {
        if(person==null || !input || Flag("ReplayActive") || Flag("NetActive") ||
            !true.Equals(Read(person.Unit,"Alive")))return false;
        if(!true.Equals(Read(input,"ControlBothSides")) &&
            !Equals(Read(input,"ControlledFaction"),Read(person.Unit,"Faction")))return false;
        var seat=Read(input,"ControlledSeat");
        return seat is int && ((int)seat<0 || Equals(seat,Read(person.Unit,"OwnerSeat")));
    }

    bool PrepareOrders()
    {
        if(submitOrder!=null)return true;
        var game=battleType.Assembly;
        var order=game.GetType("Warlapse.Battle.MoveOrder");
        var sink=game.GetType("Warlapse.Battle.IOrderSink");
        var fog=game.GetType("Warlapse.Battle.FogOfWar");
        const BindingFlags api=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
        if(order==null || sink==null || fog==null)return false;
        attackOrder=order.GetMethod("Attack",api,null,new[]{unitType.Assembly.GetType("Warlapse.Battle.Unit")},null);
        fogSeen=fog.GetMethod("Seen",api,null,new[]{unitType.Assembly.GetType("Warlapse.Battle.Unit")},null);
        if(attackOrder==null || fogSeen==null)return false;
        submitOrder=sink.GetMethod("Submit");
        return submitOrder!=null;
    }

    bool SubmitFirstPerson(Person person,object order)
    {
        if(!MayCommand(person) || !PrepareOrders() || order==null)return false;
        var sink=Read(input,"_orders") ?? Read(battle,"Orders");
        if(sink==null)return false;
        submitOrder.Invoke(sink,new[]{person.Unit,order});
        return true;
    }

    void StopSteering()
    {
        steering=false;
    }

    void StartSoldierControl(Person soldier)
    {
        commanding=MayCommand(soldier);
        previousAttackCooldown=Convert.ToSingle(Read(soldier.Unit,"AttackCdLeft"));
        previousHitTick=Convert.ToInt32(Read(soldier.Unit,"LastHitTick"));
        movementTilt=hitKick=0;
        lastAttack=-10;
    }

    void UpdateFirstPersonOrders(Keyboard keyboard,Mouse mouse,Quaternion look)
    {
        if(keyboard==null){StopSteering();return;}
        if(keyboard.pKey.wasPressedThisFrame)
        {
            if(commanding)
            {
                StopSteering();
                commanding=false;
            }
            else
            {
                commanding=MayCommand(followed);
                if(!commanding)
                {
                    ShowNotice("Komuta için kendi tarafındaki bir askeri seç.",
                        "Select a soldier on your side to take control.", 2.2f);
                }
            }
        }
        if(!commanding)return;
        if(!MayCommand(followed))
        {
            steering=commanding=false;
            return;
        }
        if(mouse!=null && mouse.leftButton.wasPressedThisFrame && PrepareOrders())
        {
            var target=FirstPersonTarget(followed.Rig.Eye.position,look*Vector3.forward);
            if(target!=null)SubmitFirstPerson(followed,attackOrder.Invoke(null,new[]{target}));
        }
        float side=(keyboard.dKey.isPressed ? 1 : 0)-(keyboard.aKey.isPressed ? 1 : 0);
        float ahead=(keyboard.wKey.isPressed ? 1 : 0)-(keyboard.sKey.isPressed ? 1 : 0);
        float dt=Mathf.Min(Time.deltaTime,.075f);
        if((side==0 && ahead==0) || dt<=0)
        {
            StopSteering();
            return;
        }

        var step=Quaternion.Euler(0,yaw,0)*new Vector3(side,0,ahead).normalized;
        float speed=followed.Rig.Legs.Length==4 ? 3.6f : 2.0f;
        if(keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)speed*=1.65f;
        var current=followed.Position;
        var destination=current+new Vector2(step.x,step.z)*(speed*dt);
        destination.x=Mathf.Clamp(destination.x,-half.x+.12f,half.x-.12f);
        destination.y=Mathf.Clamp(destination.y,-half.y+.12f,half.y-.12f);
        steering=(destination-current).sqrMagnitude>0;
        if(!steering)return;

        followed.Detached=true;
        followed.DepartureCenter=followed.GroupCenter;
        followed.Position=destination;
        followed.Heading=yaw;
        followed.ManualTravel=(destination-current)/dt;
        followed.Height=GroundHeight(destination.x,destination.y);
        followed.Rig.transform.SetPositionAndRotation(
            DisplayPoint(new Vector3(destination.x,followed.Height+.028f,destination.y)),Quaternion.Euler(0,yaw,0));
        followed.Phase=Mathf.Repeat(followed.Phase+(destination-current).magnitude*
            (followed.Rig.Legs.Length==4 ? 3.9f : 6.2f)*followed.Stride,Mathf.PI*2);
        followed.Motion=Mathf.Lerp(followed.Motion,Mathf.Clamp01(speed*.5f),1-Mathf.Exp(-dt*13));
        PosePerson(followed);
    }

    object FirstPersonTarget(Vector3 origin,Vector3 direction)
    {
        var ownFaction=Read(followed.Unit,"Faction");
        float distance=70;
        object target=null;
        foreach(var entry in models)
        {
            var model=entry.Value;
            var unit=model.Unit;
            if(unit==null || Equals(ownFaction,Read(unit,"Faction")) ||
                !true.Equals(Read(unit,"Alive")) || !model.Root.gameObject.activeInHierarchy)continue;
            if(model.People!=null)
            {
                foreach(var person in model.People)
                    if(person.Ready && person.Rig.gameObject.activeSelf)
                        TestAimTarget(origin,direction,person.Rig.Eye.position-Vector3.up*.30f,
                            .40f,unit,ref distance,ref target);
            }
            else TestAimTarget(origin,direction,model.Root.position+Vector3.up*.42f,
                Mathf.Max(.46f,model.Root.localScale.y*.63f),unit,ref distance,ref target);
        }
        if(target==null)return null;
        var eye=MapPoint(origin);
        var ray=landRoot.InverseTransformVector(direction);
        for(float step=.45f;step<distance-.38f;step+=.60f)
        {
            var at=eye+ray*step;
            if(at.y<GroundHeight(at.x,at.z)+.03f)return null;
        }
        return target;
    }

    void TestAimTarget(Vector3 origin,Vector3 direction,Vector3 point,float radius,object unit,
        ref float best,ref object target)
    {
        Vector3 delta=point-origin;
        float depth=Vector3.Dot(delta,direction);
        if(depth<=.08f || depth>=best || (delta-direction*depth).sqrMagnitude>radius*radius)return;
        if(!true.Equals(fogSeen.Invoke(null,new[]{unit})))return;
        best=depth;
        target=unit;
    }

    Vector3 StabilizeEye(Person person,Quaternion look)
    {
        var raw=person.Rig.Eye.position;
        float dt=Mathf.Min(.075f,Time.unscaledDeltaTime);
        if(!eyeReady || (steadyEye-raw).sqrMagnitude>4)
        {
            steadyEye=raw;
            eyeReady=true;
        }
        else
        {
            steadyEye.x=Mathf.Lerp(steadyEye.x,raw.x,1-Mathf.Exp(-dt*25));
            steadyEye.z=Mathf.Lerp(steadyEye.z,raw.z,1-Mathf.Exp(-dt*25));
            steadyEye.y=Mathf.Lerp(steadyEye.y,raw.y,1-Mathf.Exp(-dt*12));
        }
        return steadyEye+look*new Vector3(0,0,.081f);
    }

    void ApplySoldierCamera(Person person,ref Vector3 position,ref Quaternion rotation)
    {
        float dt=Mathf.Min(.075f,Time.unscaledDeltaTime);
        float attackCooldown=Convert.ToSingle(Read(person.Unit,"AttackCdLeft"));
        int hitTick=Convert.ToInt32(Read(person.Unit,"LastHitTick"));
        if(attackCooldown>previousAttackCooldown+.03f)lastAttack=Time.unscaledTime;
        if(hitTick!=previousHitTick)
            hitKick=Mathf.Clamp(.25f+Convert.ToSingle(Read(person.Unit,"LastHitStrength"))*.7f,.25f,.85f);
        previousAttackCooldown=attackCooldown;
        previousHitTick=hitTick;
        hitKick*=Mathf.Exp(-dt*13);
        if(cameraShakeEnabled!=null && !cameraShakeEnabled())return;

        float pace=person.Motion*(1-firstPersonAim*.77f);
        float step=person.Phase, time=Time.unscaledTime;
        var velocity=Read(person.Unit,"Velocity");
        Vector2 travel=person.Detached ? (steering ? person.ManualTravel : Vector2.zero) :
            velocity is Vector2 ? (Vector2)velocity : Vector2.zero;
        Vector3 right=rotation*Vector3.right;
        float lateral=Mathf.Clamp(Vector2.Dot(travel,new Vector2(right.x,right.z)),-1,1);
        movementTilt=Mathf.Lerp(movementTilt,-lateral*.8f,1-Mathf.Exp(-dt*8));
        float recoil=Mathf.Exp(-(time-lastAttack)*16);
        float shake=Mathf.Sin(time*61)*hitKick;
        position+=rotation*new Vector3(Mathf.Sin(step)*.032f*pace+shake*.025f,
            (.5f-Mathf.Abs(Mathf.Cos(step)))*.077f*pace,-Mathf.Cos(step*2)*.028f*pace-recoil*.036f);
        rotation*=Quaternion.Euler(Mathf.Sin(step*2)*.43f*pace-recoil*(HeldFirearm(person) ? 1.35f : .70f),
            shake*.86f,Mathf.Sin(step)*.70f*pace+movementTilt+shake*.65f);
    }

    void SyncHeldModel(Vector2 lookDelta)
    {
        bool visible=followed!=null && followed.Rig && followed.Rig.gameObject.activeInHierarchy &&
            exitStart<0 && (followBlendStart<0 || Time.unscaledTime-followBlendStart>.49f);
        if(!visible)
        {
            if(heldModel)heldModel.gameObject.SetActive(false);
            return;
        }
        if(heldPerson!=followed || !heldModel)
        {
            if(heldModel)Destroy(heldModel.gameObject);
            heldPerson=followed;
            heldModel=Joint(view.transform,"Birinci şahıs kolları ve teçhizatı",Vector3.zero);
            BuildHeldModel(heldModel,followed);
        }
        heldModel.gameObject.SetActive(true);
        float dt=Mathf.Min(.075f,Time.unscaledDeltaTime);
        heldSway=Vector2.Lerp(heldSway,Vector2.ClampMagnitude(lookDelta,14)*.0015f,1-Mathf.Exp(-dt*13));
        float phase=followed.Phase, bob=followed.Motion*(1-firstPersonAim*.76f);
        float kick=Mathf.Exp(-(Time.unscaledTime-lastAttack)*13);
        bool firearm=HeldFirearm(followed);
        heldModel.localPosition=new Vector3(-firstPersonAim*(firearm ? .215f : .095f)-heldSway.x+Mathf.Sin(phase)*.023f*bob,
            firstPersonAim*(firearm ? .145f : .055f)+heldSway.y+Mathf.Cos(phase*2)*.017f*bob,-kick*.055f);
        heldModel.localRotation=Quaternion.Euler(-kick*4+heldSway.y*74,-heldSway.x*96,Mathf.Sin(phase)*1.8f*bob);
    }

    void BuildHeldModel(Transform model,Person person)
    {
        var cloth=Color.Lerp(new Color(.39f,.36f,.29f),person.Tint,.55f);
        var leather=new Color(.21f,.16f,.11f);
        var skin=person.Outfit>=Outfit.Modern ? leather : new Color(.65f,.44f,.30f);
        var steel=new Color(.49f,.50f,.48f);
        var wood=new Color(.36f,.23f,.14f);
        string kit=person.Kit;
        bool firearm=HeldFirearm(person);
        var leftHand=firearm ? new Vector3(.145f,-.282f,.896f) : new Vector3(-.255f,-.327f,.660f);
        var rightHand=new Vector3(.244f,-.318f,.669f);
        BuildHeldArm(model,"Sol",new Vector3(-.34f,-.63f,.31f),leftHand,cloth,skin);
        BuildHeldArm(model,"Sağ",new Vector3(.35f,-.62f,.32f),rightHand,cloth,skin);
        if(kit=="Archer" || kit=="Crossbow")
        {
            bool crossbow=kit=="Crossbow";
            float x=crossbow ? .14f : -.247f;
            var bow=Joint(model,crossbow ? "Arbalet" : "Yay",new Vector3(x,-.25f,.69f));
            if(crossbow)bow.localRotation=Quaternion.Euler(0,0,90);
            for(int i=-2;i<=2;i++)
                Part(bow,"Yay kolu",wood,new Vector3(0,i*.116f,.121f-i*i*.026f),new Vector3(.038f,.133f,.041f))
                    .localRotation=Quaternion.Euler(-i*24,0,0);
            Part(bow,"Yay kirişi",leather,new Vector3(0,0,.012f),new Vector3(.008f,.52f,.009f));
            if(crossbow)Part(model,"Arbalet gövdesi",wood,new Vector3(x,-.25f,.67f),new Vector3(.071f,.075f,.46f));
        }
        else if(person.Outfit<=Outfit.Medieval || kit=="Pike" || kit=="Riot")
        {
            var weapon=Joint(model,"Yakın dövüş silahı",new Vector3(.237f,-.28f,.655f));
            weapon.localRotation=Quaternion.Euler(-26,0,-15);
            if(kit=="Pike" || kit=="Riot")
            {
                Shape(weapon,"Sap","taper",wood,new Vector3(0,.12f,0),new Vector3(.046f,.69f,.046f));
                if(kit=="Pike")Shape(weapon,"Mızrak ucu","cone",steel,new Vector3(0,.54f,0),new Vector3(.075f,.19f,.035f));
            }
            else
            {
                Part(weapon,"Kabza",leather,Vector3.zero,new Vector3(.055f,.19f,.048f));
                Part(weapon,"Balçak",steel,new Vector3(0,.113f,0),new Vector3(.16f,.036f,.05f));
                Shape(weapon,"Kılıç","cone",steel,new Vector3(0,.314f,0),new Vector3(.065f,.39f,.022f));
            }
            Shape(model,"Kalkan kenarı","shield",Color.Lerp(cloth,wood,.58f),
                new Vector3(-.31f,-.32f,.69f),new Vector3(.31f,.39f,.073f)).localRotation=Quaternion.Euler(0,-16,-12);
        }
        else
        {
            bool musket=person.Outfit==Outfit.Gunpowder;
            bool oldWood=musket || person.Outfit<=Outfit.WorldWar;
            var stock=oldWood ? wood : leather;
            var metal=steel*.60f;
            var butt=Shape(model,"Dipçik","taper",stock,new Vector3(.231f,-.319f,.540f),new Vector3(.105f,.33f,.139f));
            butt.localRotation=Quaternion.Euler(90,0,0);
            Part(model,"El kundağı",stock,new Vector3(.231f,-.258f,.93f),new Vector3(.081f,.078f,.35f));
            Part(model,"Mekanizma",metal,new Vector3(.231f,-.225f,.756f),new Vector3(.073f,.067f,.245f));
            var barrel=Shape(model,"Namlu","taper",steel*.61f,new Vector3(.23f,-.221f,1.055f),
                new Vector3(.032f,musket ? .60f : .44f,.032f));
            barrel.localRotation=Quaternion.Euler(90,0,0);
            Part(model,"Arpacık",metal,new Vector3(.23f,-.183f,1.18f),new Vector3(.019f,.040f,.029f));
            for(int i=-1;i<=1;i+=2)
                Part(model,"Gez",metal,new Vector3(.23f+i*.019f,-.179f,.775f),new Vector3(.012f,.028f,.025f));
            Part(model,"Mekanizma kolu",metal,new Vector3(.277f,-.229f,.743f),new Vector3(.058f,.025f,.025f));
            Shape(model,"Mekanizma düğmesi","round",metal,new Vector3(.306f,-.231f,.743f),Vector3.one*.032f);
            Part(model,"Namlu bileziği",metal,new Vector3(.23f,-.243f,1.033f),new Vector3(.084f,.086f,.028f));
            if(!musket)
                Part(model,"Şarjör",metal,new Vector3(.23f,-.311f,.796f),new Vector3(.052f,oldWood ? .10f : .16f,.079f))
                    .localRotation=Quaternion.Euler(-11,0,0);
            if(person.Outfit==Outfit.Future)
                Part(model,"Enerji hücresi",new Color(.30f,.66f,.76f),new Vector3(.23f,-.216f,.886f),
                    new Vector3(.084f,.028f,.128f));
        }
        BakeStaticParts(new Model{Root=model},"held|"+person.Outfit+"|"+person.Kit+"|"+(Color32)person.Tint);
    }

    static bool HeldFirearm(Person person)
    {
        return person.Outfit>Outfit.Medieval && person.Kit!="Pike" && person.Kit!="Riot" &&
            person.Kit!="Archer" && person.Kit!="Crossbow";
    }

    void BuildHeldArm(Transform model,string side,Vector3 elbow,Vector3 hand,Color cloth,Color skin)
    {
        var arm=Shape(model,side+" ön kol","round",cloth,(elbow+hand)*.5f,
            new Vector3(.155f,Vector3.Distance(elbow,hand)+.075f,.158f));
        arm.localRotation=Quaternion.FromToRotation(Vector3.up,hand-elbow);
        Shape(model,side+" el","round",skin,hand,new Vector3(.109f,.094f,.124f));
    }
}
