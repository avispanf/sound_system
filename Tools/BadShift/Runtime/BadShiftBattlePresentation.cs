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
    private Light[] keys;
    private float[] keyIntensities;
    private Color[] keyColors;
    private Color background,ambient;
    private Renderer[][] actors;
    private Color[][] colors;
    private MaterialPropertyBlock[][] original;
    private MaterialPropertyBlock block;
    private void Start()
    {
        var demo=Battle.Life.Demo;
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
    private void LateUpdate()
    {
        Weight=Mathf.MoveTowards(Weight,Battle.Active?1:0,Time.deltaTime*1.5f);Grade.weight=Weight;
        for(int i=0;i<keys.Length;i++){keys[i].intensity=Mathf.Lerp(keyIntensities[i],keyIntensities[i]*.4f,Weight);keys[i].color=Color.Lerp(keyColors[i],new Color(.57f,.65f,1),Weight);}
        RenderSettings.ambientLight=Color.Lerp(ambient,new Color(.12f,.15f,.25f),Weight);
        Battle.Life.Demo.Game.View.backgroundColor=Color.Lerp(background,new Color(.055f,.065f,.12f),Weight);
        RedLight.intensity=Weight*4;BlueLight.intensity=Weight*5;
        Ring.startColor=Ring.endColor=new Color(1,.14f,.27f,Weight*.7f);
        foreach(var smoke in Smoke){var emission=smoke.emission;emission.enabled=Battle.Active;}
        for(int a=0;a<2;a++)
        {
            bool blocked=a==0&&Time.time<Battle.PlayerBlockUntil;
            float amount=Mathf.Clamp01(((blocked?Battle.PlayerBlockUntil:a==0?Battle.PlayerFlashUntil:Battle.EnemyFlashUntil)-Time.time)/.22f);
            Color pulse=blocked?new Color(.2f,.7f,1):new Color(1,.34f,.24f);
            for(int i=0;i<actors[a].Length;i++)
            {
                if(amount<=0){actors[a][i].SetPropertyBlock(original[a][i]);continue;}
                actors[a][i].GetPropertyBlock(block);block.SetColor("_BaseColor",Color.Lerp(colors[a][i],pulse,amount*.8f));actors[a][i].SetPropertyBlock(block);
            }
            Light flash=a==0?PlayerHitLight:EnemyHitLight;flash.intensity=amount*2.5f;flash.color=pulse;
        }
    }
    private void OnDisable()
    {
        if(keys!=null){for(int i=0;i<keys.Length;i++)if(keys[i]!=null){keys[i].intensity=keyIntensities[i];keys[i].color=keyColors[i];}RenderSettings.ambientLight=ambient;}
        if(Battle!=null&&Battle.Life!=null&&Battle.Life.Demo!=null&&Battle.Life.Demo.Game.View!=null)Battle.Life.Demo.Game.View.backgroundColor=background;
        if(Grade!=null)Grade.weight=0;
        if(actors!=null)for(int a=0;a<2;a++)for(int i=0;i<actors[a].Length;i++)if(actors[a][i]!=null)actors[a][i].SetPropertyBlock(original[a][i]);
    }
}
