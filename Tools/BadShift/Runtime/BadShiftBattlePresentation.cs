using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(190)]
public sealed class BadShiftBattlePresentation : MonoBehaviour
{
    public BadShiftBattle Battle;
    public Volume Grade;
    public ParticleSystem[] Smoke;
    public Light RedLight,BlueLight,PlayerHitLight,EnemyHitLight;
    public LineRenderer Ring;
    public float Weight { get; private set; }
    public float OvertimeWeight { get; private set; }
    public Texture BossPortrait=>portraitTarget;
    private Camera portraitCamera;
    private RenderTexture portraitTarget;
    private Light[] keys;
    private float[] keyIntensities;
    private Color[] keyColors;
    private Color background,ambient,redSource,blueSource;
    private Renderer[][] actors;
    private Color[][] colors;
    private MaterialPropertyBlock[][] original;
    private MaterialPropertyBlock block;
    private LineRenderer guard,tell;
    private LineRenderer[][] sparks;
    private LineRenderer[] swishes,finale;
    private readonly Vector3[] lastTips=new Vector3[2];
    private readonly bool[] tracking=new bool[2];
    private void Start()
    {
        var demo=Battle.Life.Demo;redSource=RedLight.color;blueSource=BlueLight.color;
        keys=FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.type==LightType.Directional).ToArray();
        keyIntensities=keys.Select(l=>l.intensity).ToArray();keyColors=keys.Select(l=>l.color).ToArray();background=demo.Game.View.backgroundColor;ambient=RenderSettings.ambientLight;
        actors=new[]{demo.PlayerAnimator.GetComponentsInChildren<Renderer>(),demo.DanceAnimator.GetComponentsInChildren<Renderer>()};
        colors=new Color[2][];original=new MaterialPropertyBlock[2][];block=new MaterialPropertyBlock();
        for(int a=0;a<2;a++)
        {
            colors[a]=new Color[actors[a].Length];original[a]=new MaterialPropertyBlock[actors[a].Length];
            for(int i=0;i<actors[a].Length;i++){colors[a][i]=actors[a][i].sharedMaterial.color;original[a][i]=new MaterialPropertyBlock();actors[a][i].GetPropertyBlock(original[a][i]);}
        }
    }
    private LineRenderer Stroke(string label,int points,float width)
    {
        var line=new GameObject(label).AddComponent<LineRenderer>();line.transform.SetParent(transform,false);
        line.sharedMaterial=Ring.sharedMaterial;line.positionCount=points;line.widthMultiplier=width;line.useWorldSpace=true;
        line.numCapVertices=3;line.enabled=false;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
        return line;
    }
    private void CreateAccents()
    {
        guard=Stroke("Guard accent",25,.08f);tell=Stroke("Enemy attack direction",19,.055f);sparks=new LineRenderer[2][];swishes=new LineRenderer[2];
        for(int a=0;a<2;a++)
        {
            sparks[a]=new LineRenderer[6];for(int i=0;i<6;i++)sparks[a][i]=Stroke("Bonk accent "+a+" "+i,2,.075f);
            swishes[a]=Stroke("Breadstick swish "+a,3,.055f);
        }
    }
    private void Accents()
    {
        if(guard==null)CreateAccents();
        EnemyTelegraph();
        Transform player=Battle.transform;
        bool blocked=Battle.BlockFeedback>0;
        guard.enabled=(Battle.IsBlocking||blocked)&&Battle.Phase==BadShiftBattlePhase.Fighting;
        Color cyan=new Color(.35f,.85f,1,blocked?.95f:.65f);guard.startColor=guard.endColor=cyan;
        Vector3 center=player.position+Vector3.up*1.55f+player.forward*.55f;
        for(int i=0;i<25;i++){float angle=Mathf.Lerp(-2.1f,2.1f,i/24f);guard.SetPosition(i,center+(Battle.Life.Demo.Game.View.transform.right*Mathf.Sin(angle)+Battle.Life.Demo.Game.View.transform.up*Mathf.Cos(angle))*(.57f+(blocked?(1-Battle.BlockFeedback)*.22f:0)));}
        for(int a=0;a<2;a++)
        {
            Transform actor=a==0?player:Battle.EnemyBody.transform;
            float amount=Mathf.Max(Mathf.Clamp01(((a==0?Battle.PlayerFlashUntil:Battle.EnemyFlashUntil)-Time.time)/.22f),a==0&&blocked?Battle.BlockFeedback:0);
            Vector3 impact=actor.position+Vector3.up*1.5f;
            Vector3 right=Battle.Life.Demo.Game.View.transform.right,up=Battle.Life.Demo.Game.View.transform.up;
            Color color=a==0&&blocked?cyan:new Color(1,.79f,.34f,amount);
            for(int i=0;i<6;i++)
            {
                var line=sparks[a][i];line.enabled=Battle.Active&&amount>0;
                float angle=(i+.15f)*Mathf.PI/3;Vector3 direction=right*Mathf.Cos(angle)+up*Mathf.Sin(angle);
                float radius=.24f+(1-amount)*.65f;
                line.SetPosition(0,impact+direction*radius);line.SetPosition(1,impact+direction*(radius+.25f*amount));
                line.startColor=line.endColor=color;
            }
            Transform stick=(a==0?Battle.Life.PlayerStick:Battle.Life.EnemyStick).transform;
            Vector3 tip=stick.TransformPoint(Vector3.up*.45f);
            bool swinging=Battle.Phase==BadShiftBattlePhase.Fighting&&Time.time<(a==0?Battle.PlayerSwingUntil:Battle.EnemySwingUntil);
            var swish=swishes[a];swish.enabled=swinging&&tracking[a]&&Vector3.Distance(tip,lastTips[a])>.015f;
            swish.SetPosition(0,lastTips[a]);swish.SetPosition(1,Vector3.Lerp(lastTips[a],tip,.5f)+Vector3.up*.035f);swish.SetPosition(2,tip);
            swish.startColor=new Color(1,.87f,.63f,0);swish.endColor=new Color(1,.87f,.63f,.8f);
            lastTips[a]=tip;tracking[a]=swinging;
        }
    }
    private void EnemyTelegraph()
    {
        tell.enabled=Battle.Phase==BadShiftBattlePhase.Fighting&&Battle.EnemyWindingUp;
        if(!tell.enabled)return;
        Vector3 origin=Battle.EnemyBody.transform.position+Vector3.up*.05f,forward=Battle.EnemyAim,right=Vector3.Cross(Vector3.up,forward);
        float progress=Battle.EnemyWindupProgress;
        bool wide=Battle.EnemyAttack==BadShiftEnemyAttack.Sweep||Battle.EnemyAttack==BadShiftEnemyAttack.Backhand;
        Color color=Battle.EnemyAttack==BadShiftEnemyAttack.Overhead?new Color(1,.53f,.35f):Battle.EnemyAttack==BadShiftEnemyAttack.Thrust?new Color(.85f,.65f,1):new Color(1,.83f,.3f);
        color.a=.35f+progress*.5f;tell.startColor=tell.endColor=color;tell.widthMultiplier=.045f+progress*.025f;
        tell.positionCount=wide?19:5;
        if(wide)
        {
            tell.SetPosition(0,origin);tell.SetPosition(18,origin);
            for(int i=1;i<18;i++)tell.SetPosition(i,origin+Quaternion.AngleAxis(Mathf.Lerp(-65,65,(i-1)/16f),Vector3.up)*forward*Battle.EnemyTelegraphReach);
        }
        else
        {
            float width=Battle.EnemyAttack==BadShiftEnemyAttack.Thrust?.3f:.5f,length=Battle.EnemyTelegraphReach;
            tell.SetPosition(0,origin-right*width);tell.SetPosition(1,origin+forward*length-right*width);tell.SetPosition(2,origin+forward*length+right*width);tell.SetPosition(3,origin+right*width);tell.SetPosition(4,origin-right*width);
        }
    }
    private void LateUpdate()
    {
        Accents();
        Weight=Mathf.MoveTowards(Weight,Battle.Active?1:0,Time.deltaTime*1.5f);Grade.weight=Weight*(Battle.OvertimeFinish&&Battle.Finished?.55f:1);
        for(int i=0;i<keys.Length;i++){keys[i].intensity=Mathf.Lerp(keyIntensities[i],keyIntensities[i]*.4f,Weight);keys[i].color=Color.Lerp(keyColors[i],new Color(.57f,.65f,1),Weight);}
        RenderSettings.ambientLight=Color.Lerp(ambient,new Color(.12f,.15f,.25f),Weight);
        Battle.Life.Demo.Game.View.backgroundColor=Color.Lerp(background,new Color(.055f,.065f,.12f),Weight);
        OvertimeWeight=Mathf.MoveTowards(OvertimeWeight,Battle.Active&&Battle.SecondPhase?1:0,Time.deltaTime*2);
        RedLight.color=Color.Lerp(redSource,new Color(1,.42f,.08f),OvertimeWeight);BlueLight.color=Color.Lerp(blueSource,new Color(.65f,.25f,1),OvertimeWeight);
        RedLight.intensity=Weight*(4+OvertimeWeight);BlueLight.intensity=Weight*5;
        Ring.startColor=Ring.endColor=new Color(1,Mathf.Lerp(.14f,.5f,OvertimeWeight),Mathf.Lerp(.27f,.08f,OvertimeWeight),Weight*.7f);
        foreach(var smoke in Smoke){var emission=smoke.emission;emission.enabled=Battle.Active&&!Battle.Finished;}
        for(int a=0;a<2;a++)
        {
            bool blocked=Battle.BlockFeedback>0&&(a==0||Time.time>=Battle.EnemyFlashUntil);
            float amount=Mathf.Max(Mathf.Clamp01(((a==0?Battle.PlayerFlashUntil:Battle.EnemyFlashUntil)-Time.time)/.22f),blocked?Battle.BlockFeedback*(a==0?1:.45f):0);
            bool guarding=a==0&&Battle.IsBlocking;
            Color pulse=blocked||guarding?new Color(.2f,.7f,1):new Color(1,.34f,.24f);
            for(int i=0;i<actors[a].Length;i++)
            {
                if(amount<=0&&!guarding){actors[a][i].SetPropertyBlock(original[a][i]);continue;}
                actors[a][i].GetPropertyBlock(block);block.SetColor("_BaseColor",Color.Lerp(colors[a][i],pulse,Mathf.Max(amount*.8f,guarding?.24f:0)));actors[a][i].SetPropertyBlock(block);
            }
            Light flash=a==0?PlayerHitLight:EnemyHitLight;flash.intensity=amount*2.5f;flash.color=pulse;
        }
        Finale();
        RenderBossPortrait();
    }
    private void Finale()
    {
        if(finale==null){finale=new LineRenderer[20];for(int i=0;i<finale.Length;i++)finale[i]=Stroke("Overtime confetti "+i,2,.065f);}
        bool falling=Battle.OvertimeFinish&&Battle.Falling;
        float t=Battle.Finished?Battle.OutcomeTime:Battle.FinishElapsed;
        bool victory=Battle.OvertimeFinish&&Battle.Finished;
        Vector3 center=(falling?Battle.EnemyBody.transform.position:Battle.transform.position)+Vector3.up*(falling?2.5f:2.2f);
        for(int i=0;i<finale.Length;i++)
        {
            var line=finale[i];line.enabled=falling&&i<5||victory&&t<3.6f;
            if(!line.enabled)continue;
            float angle=i*Mathf.PI*2/(falling?5:20)+t*(falling?5:1.5f);
            float radius=falling?.45f:.4f+t*(.35f+i%4*.12f);
            Vector3 position=center+new Vector3(Mathf.Cos(angle)*radius,falling?Mathf.Sin(angle*2)*.08f:t*(1.9f+i%3*.25f)-.85f*t*t,Mathf.Sin(angle)*radius);
            Vector3 tilt=new Vector3(Mathf.Cos(t*10+i),Mathf.Sin(t*10+i),.2f)*.12f;
            line.SetPosition(0,position-tilt);line.SetPosition(1,position+tilt);
            Color color=i%3==0?new Color(1,.77f,.2f):i%3==1?new Color(.45f,1,.75f):new Color(1,.48f,.32f);
            color.a=falling?1:Mathf.Clamp01((3.6f-t)*2);line.startColor=line.endColor=color;
        }
        if(victory)
        {
            Ring.startColor=Ring.endColor=new Color(1,.8f,.3f,.65f);
            RedLight.color=new Color(1,.72f,.3f);BlueLight.color=new Color(.4f,1,.75f);
        }
    }
    private void RenderBossPortrait()
    {
        if(!Battle.Finished)return;
        if(portraitCamera==null)
        {
            portraitTarget=new RenderTexture(480,256,24){name="Broccoli result portrait"};
            portraitCamera=new GameObject("Broccoli portrait camera").AddComponent<Camera>();portraitCamera.transform.SetParent(transform,false);
            portraitCamera.CopyFrom(Battle.Life.Demo.Game.View);portraitCamera.enabled=false;portraitCamera.targetTexture=portraitTarget;
            portraitCamera.orthographic=true;portraitCamera.orthographicSize=2.8f;portraitCamera.aspect=1.875f;portraitCamera.clearFlags=CameraClearFlags.SolidColor;
            portraitCamera.backgroundColor=new Color(.13f,.18f,.20f);
        }
        Vector3 focus=Battle.Life.Demo.Boss.position+Vector3.up*3.1f;
        portraitCamera.transform.position=focus+new Vector3(0,.6f,-9);portraitCamera.transform.LookAt(focus);portraitCamera.Render();
    }
    private void OnDestroy()
    {
        if(portraitCamera!=null)Destroy(portraitCamera.gameObject);
        if(portraitTarget!=null){portraitTarget.Release();Destroy(portraitTarget);}
    }
    private void OnDisable()
    {
        if(finale!=null)foreach(var line in finale)line.enabled=false;
        if(guard!=null)guard.enabled=false;
        if(tell!=null)tell.enabled=false;
        if(sparks!=null)foreach(var group in sparks)foreach(var line in group)line.enabled=false;
        if(swishes!=null)foreach(var line in swishes)line.enabled=false;
        if(keys!=null){for(int i=0;i<keys.Length;i++)if(keys[i]!=null){keys[i].intensity=keyIntensities[i];keys[i].color=keyColors[i];}RenderSettings.ambientLight=ambient;}
        if(Battle!=null&&Battle.Life!=null&&Battle.Life.Demo!=null&&Battle.Life.Demo.Game.View!=null)Battle.Life.Demo.Game.View.backgroundColor=background;
        if(Grade!=null)Grade.weight=0;
        if(keys!=null){if(RedLight!=null)RedLight.color=redSource;if(BlueLight!=null)BlueLight.color=blueSource;}OvertimeWeight=0;
        if(actors!=null)for(int a=0;a<2;a++)for(int i=0;i<actors[a].Length;i++)if(actors[a][i]!=null)actors[a][i].SetPropertyBlock(original[a][i]);
    }
}
