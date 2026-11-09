using System;
using System.IO;
using System.Linq;
using AudioMW;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildBadShiftInteractions
{
    private const string Root = "Assets/BadShift/Interactions";
    private static PhysicsMaterial friction;
    private static SoundEvent impact, breaking;
    private static Material ceramic;
    private static Mesh shard;
    private static void Body(GameObject item, float mass, bool fragile = false)
    {
        Rigidbody body = item.AddComponent<Rigidbody>(); body.mass = mass;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearDamping = .08f; body.angularDamping = .15f;
        body.solverIterations = 12; body.solverVelocityIterations = 4;
        foreach (Collider collider in item.GetComponentsInChildren<Collider>()) collider.sharedMaterial = friction;
        BadShiftImpactProp prop = item.AddComponent<BadShiftImpactProp>(); prop.ImpactSound = impact; prop.BreakSound = breaking;
        prop.Breakable = fragile; prop.ShardMesh = shard; prop.ShardMaterial = ceramic; prop.ContactMaterial = friction;
    }
    private static Mesh CupMesh()
    {
        Vector3[] vertices = new Vector3[50]; int[] triangles = new int[12*4*6+72];
        for(int i=0;i<12;i++)
        {
            float a=i*Mathf.PI/6;
            for(int j=0;j<4;j++)
            {
                float r=j<2?.16f:.125f, y=j==0?0:j==3?.04f:.34f;
                vertices[i*4+j]=new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);
                int next=(i+1)%12, k=(j+1)%4, t=(i*4+j)*6;
                triangles[t]=i*4+j;triangles[t+1]=next*4+j;triangles[t+2]=next*4+k;
                triangles[t+3]=i*4+j;triangles[t+4]=next*4+k;triangles[t+5]=i*4+k;
            }
        }
        for(int t=0;t<12*4*6;t+=3){int temp=triangles[t+1];triangles[t+1]=triangles[t+2];triangles[t+2]=temp;}
        vertices[48]=new Vector3(0,.04f,0);vertices[49]=Vector3.zero;
        for(int i=0;i<12;i++)
        {
            int next=(i+1)%12,t=12*4*6+i*6;
            triangles[t]=48;triangles[t+1]=next*4+3;triangles[t+2]=i*4+3;
            triangles[t+3]=49;triangles[t+4]=i*4;triangles[t+5]=next*4;
        }
        Mesh mesh=new Mesh{name="Ceramic cup"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,Root+"/Cup.asset");return mesh;
    }
    private static ParticleSystem Particles(string name, Material material, Mesh mesh, bool burst)
    {
        GameObject item=new GameObject(name);ParticleSystem particles=item.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=particles.main;main.duration=1;main.loop=!burst;main.playOnAwake=true;main.startLifetime=burst?.6f:1;main.startSpeed=burst?1.6f:.3f;main.startSize=burst?.1f:.07f;main.maxParticles=32;main.simulationSpace=ParticleSystemSimulationSpace.World;
        var emission=particles.emission;emission.rateOverTime=burst?0:7;if(burst)emission.SetBursts(new[]{new ParticleSystem.Burst(0,18)});
        var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.4f;
        var size=particles.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,1,1,0));
        ParticleSystemRenderer renderer=particles.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=mesh;renderer.sharedMaterial=material;
        return particles;
    }
    public static void StabilizeCups()
    {
        if(!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),".bad-shift-demo")))throw new Exception("Isolated BAD SHIFT required");
        EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
        foreach(BadShiftImpactProp prop in UnityEngine.Object.FindObjectsByType<BadShiftImpactProp>(FindObjectsSortMode.None).Where(p=>p.Breakable))
        {
            prop.transform.position=new Vector3(prop.transform.position.x,1.02f,prop.transform.position.z<0?-2.6f:1.4f);
            CapsuleCollider old=prop.GetComponent<CapsuleCollider>();if(old!=null)UnityEngine.Object.DestroyImmediate(old);
            MeshCollider shape=prop.GetComponent<MeshCollider>();if(shape==null)shape=prop.gameObject.AddComponent<MeshCollider>();
            shape.sharedMesh=prop.GetComponent<MeshFilter>().sharedMesh;shape.convex=true;shape.sharedMaterial=prop.ContactMaterial;
        }
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);EditorApplication.Exit(0);
    }
    public static void Run()
    {
        try
        {
            if(!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),".bad-shift-demo")))throw new Exception("Isolated BAD SHIFT required");
            EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
            if(AssetDatabase.IsValidFolder(Root))throw new Exception("Preserve existing interaction authoring");
            AssetDatabase.CreateFolder("Assets/BadShift","Interactions");
            BadShiftDemo demo=UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();AudioMWPlayground game=demo.Game;
            game.OrbitCamera=game.gameObject.AddComponent<BadShiftOrbitCamera>();game.OrbitCamera.Game=game;game.JumpHeight=.55f;
            friction=new PhysicsMaterial("Cafe contact"){dynamicFriction=.55f,staticFriction=.65f,bounciness=.03f,frictionCombine=PhysicsMaterialCombine.Average,bounceCombine=PhysicsMaterialCombine.Minimum};AssetDatabase.CreateAsset(friction,Root+"/Contact.physicMaterial");
            ceramic=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=new Color(.96f,.9f,.78f)};ceramic.SetFloat("_Smoothness",.2f);AssetDatabase.CreateAsset(ceramic,Root+"/Ceramic.mat");
            shard=new Mesh{name="Ceramic shard"};shard.vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(0,0,.5f),new Vector3(0,.8f,0)};shard.triangles=new[]{0,2,1,0,1,3,1,2,3,2,0,3};shard.RecalculateNormals();shard.RecalculateBounds();AssetDatabase.CreateAsset(shard,Root+"/Shard.asset");
            impact=UnityEngine.Object.Instantiate(game.Impact);impact.name="Cafe_PropImpact";impact.Volume=.18f;impact.SpatialBlend=1;impact.MinDistance=1;impact.MaxDistance=18;AssetDatabase.CreateAsset(impact,Root+"/PropImpact.asset");
            breaking=UnityEngine.Object.Instantiate(impact);breaking.name="Cafe_CeramicBreak";breaking.Volume=.35f;AssetDatabase.CreateAsset(breaking,Root+"/CeramicBreak.asset");
            Transform[] all=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach(Transform table in all.Where(t=>t.name=="Square dining table"))
            {
                Vector3 center=new Vector3(table.position.x,0,table.position.z);GameObject assembly=new GameObject("Pushable dining table");assembly.transform.position=center;table.SetParent(assembly.transform,true);
                foreach(Transform leg in all.Where(t=>t.name=="Dining table leg" && Vector3.Distance(t.position,center)<1.2f)){leg.SetParent(assembly.transform,true);if(leg.GetComponent<Collider>()==null)leg.gameObject.AddComponent<BoxCollider>();}
                Body(assembly,18);
            }
            foreach(Transform chair in all.Where(t=>t.name=="Coral cafe chair"))
            {
                foreach(Transform leg in chair.GetComponentsInChildren<Transform>().Where(t=>t.name=="Chair leg"))if(leg.GetComponent<Collider>()==null)leg.gameObject.AddComponent<BoxCollider>();
                Body(chair.gameObject,4);
            }
            foreach(Transform carton in all.Where(t=>t.name=="Milk carton"))
            {
                Transform label=all.First(t=>t.name=="Milk blue label"&&Vector3.Distance(t.position,carton.position)<.4f);label.SetParent(carton,true);
                carton.gameObject.AddComponent<BoxCollider>();Body(carton.gameObject,.2f);
            }
            Mesh cupMesh=CupMesh();
            foreach(float z in new[]{-2f,2f})for(int i=0;i<2;i++)
            {
                GameObject cup=new GameObject("Breakable ceramic cup");cup.transform.position=new Vector3(-9.5f+i*.8f,1.02f,z-.6f);
                cup.AddComponent<MeshFilter>().sharedMesh=cupMesh;cup.AddComponent<MeshRenderer>().sharedMaterial=ceramic;
                MeshCollider collider=cup.AddComponent<MeshCollider>();collider.sharedMesh=cupMesh;collider.convex=true;Body(cup,.35f,true);
            }
            Material glow=new Material(Shader.Find("Universal Render Pipeline/Unlit")){color=new Color(1,.75f,.12f)};AssetDatabase.CreateAsset(glow,Root+"/BananaGlow.mat");
            GameObject sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);Mesh particleMesh=sphere.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(sphere);
            ParticleSystem burst=Particles("Banana collect burst",glow,particleMesh,true);GameObject prefab=PrefabUtility.SaveAsPrefabAsset(burst.gameObject,Root+"/CollectBurst.prefab");UnityEngine.Object.DestroyImmediate(burst.gameObject);
            PlaygroundPickup first=game.Pickups[0];PlaygroundPickup[] bananas={first,UnityEngine.Object.Instantiate(first),UnityEngine.Object.Instantiate(first)};
            for(int i=0;i<bananas.Length;i++)
            {
                PlaygroundPickup banana=bananas[i];banana.name="Glowing banana "+(i+1);banana.transform.position=new Vector3(-9+i*1.8f,.9f,-6.5f);banana.AutoCollect=true;banana.CollectBurst=prefab.GetComponent<ParticleSystem>();
                foreach(Collider old in banana.GetComponents<Collider>())UnityEngine.Object.DestroyImmediate(old);
                SphereCollider trigger=banana.gameObject.AddComponent<SphereCollider>();trigger.isTrigger=true;trigger.radius=1.1f;
                Rigidbody body=banana.gameObject.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
                ParticleSystem sparkle=Particles("Banana sparkles",glow,particleMesh,false);sparkle.transform.SetParent(banana.transform,false);
                GameObject halo=new GameObject("Banana halo");halo.transform.SetParent(banana.transform,false);halo.transform.localPosition=Vector3.down*1.2f;
                LineRenderer line=halo.AddComponent<LineRenderer>();line.useWorldSpace=false;line.loop=true;line.widthMultiplier=.055f;line.positionCount=40;line.sharedMaterial=glow;
                for(int j=0;j<40;j++){float a=j*Mathf.PI/20;line.SetPosition(j,new Vector3(Mathf.Cos(a)*.75f,0,Mathf.Sin(a)*.75f));}
            }
            game.Pickups=game.Pickups.Concat(bananas.Skip(1)).ToArray();
            string[] quotes={"Team meeting: please stop eating the inventory.","I am broccoli. Of course I expect growth.","The microwave has requested a pay rise."};
            game.Lines=quotes.Select((quote,index)=>{VoiceLine line=UnityEngine.Object.Instantiate(game.Lines[index%game.Lines.Length]);line.name="Manager_"+(index+1);line.Speaker="The Manager";line.Subtitle=quote;AssetDatabase.CreateAsset(line,Root+"/Manager"+(index+1)+".asset");return line;}).ToArray();
            EditorUtility.SetDirty(game);EditorUtility.SetDirty(demo);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);Debug.Log("BAD SHIFT interactive physics and camera authored");EditorApplication.Exit(0);
        }
        catch(Exception exception){Debug.LogException(exception);EditorApplication.Exit(1);}
    }
}
