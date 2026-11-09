using System.Collections;
using System.Collections.Generic;
using AudioMW;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class BadShiftTour : MonoBehaviour
{
    public static bool StartOnLoad;
    public AudioMWPlayground Game;
    public BadShiftDemo Demo;
    public bool Running { get; private set; }
    public Vector3 Direction { get; private set; }
    public string Heading { get; private set; } = "Your audio playground";
    public string Explanation { get; private set; } = "Explore freely, or watch the guided demo. All sounds use AudioMW. Audio clips are placeholders.";
    private Coroutine routine;
    private IEnumerator Start()
    {
        bool start = StartOnLoad; StartOnLoad = false;
        yield return new WaitForSeconds(.3f);
        if (start) Begin();
    }
    public void WatchFromStart() { StartOnLoad = true; Game.ResetScene(); }
    public void Begin()
    {
        Stop(); Running = true; routine = StartCoroutine(Show());
    }
    public void Stop()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null; Running = false; Direction = Vector3.zero;
        Heading = "Your audio playground";
        Explanation = "WASD move, Shift run, Space jump. X interacts with the nearest station; Q switches its variation. Watch demo starts a fresh guided tour.";
    }
    private IEnumerator Show()
    {
        Game.OrbitCamera.Home();
        Heading = "01 / Surface footsteps & pickups";
        Explanation = "The player walks across stone and grass. Local surface parameters choose footsteps; glowing bananas trigger pickup events automatically.";
        yield return WalkTo(new Vector3(-10,0,-7.5f)); if (!Running) yield break;
        yield return WalkTo(new Vector3(-6,0,-7.5f)); if (!Running) yield break;
        yield return new WaitForSeconds(2);
        Heading = "02 / Adaptive music";
        Explanation = "Crossing into the kitchen requests Combat. The music waits for a musical boundary; the employee dances only after the track changes.";
        yield return WalkTo(new Vector3(1,0,4.3f)); if (!Running) yield break;
        float until = Time.time + 8;
        while (AudioSystem.Music.IsTransitionPending && Time.time < until) yield return null;
        yield return new WaitForSeconds(3);
        Heading = "03 / Random & ShuffleBag";
        Explanation = "The microwave plays three ShuffleBag choices, then Random. The station readout shows the clip actually selected by AudioMW.";
        yield return WalkTo(new Vector3(6,0,5.75f)); if (!Running) yield break;
        for (int i=0;i<3;i++) { Demo.PlayMicrowave(); yield return new WaitForSeconds(1.2f); }
        Demo.ToggleSelection(); Demo.PlayMicrowave(); yield return new WaitForSeconds(2); Demo.ToggleSelection();
        Heading = "04 / Voice-over ducking";
        Explanation = "The manager speaks. Watch the quote and actual mixer duck value: music and ambience dip, then recover after the line.";
        yield return WalkTo(new Vector3(-5.8f,0,6.5f)); if (!Running) yield break;
        if (!AudioSystem.VoiceOver.IsSpeaking) Game.Speak();
        while (AudioSystem.VoiceOver.IsSpeaking) yield return null;
        yield return new WaitForSeconds(2);
        Heading = "05 / Dry, echo, reverb";
        Explanation = "One source and one clip: first dry, then echo, then reverb. Background music is reduced here so the processing is easier to compare.";
        if (Game.Door != null)
        {
            Game.Door.SetOpen(true);
            float doorDeadline = Time.time + 4;
            while (Game.Door.IsMoving && Time.time < doorDeadline) yield return null;
        }
        yield return WalkTo(new Vector3(-3,0,6.5f)); if (!Running) yield break;
        for (int mode=0;mode<3;mode++) { Demo.SetEffect(mode); Demo.PlayRoom(); yield return new WaitForSeconds(3); }
        Heading = "06 / Distance attenuation";
        Explanation = "Move the listener near and far from the fixed fridge. Music is muted here. E / lower door, G / upper door. Camera zoom does not change audio distance.";
        yield return WalkTo(new Vector3(9,0,5.25f)); if (!Running) yield break;
        yield return new WaitForSeconds(3);
        yield return WalkTo(new Vector3(7,0,5.25f)); if (!Running) yield break;
        if (Game.FridgeDoors != null)
        {
            Game.FridgeDoors.Lower.SetOpen(true); Game.FridgeDoors.Upper.SetOpen(true);
            yield return new WaitForSeconds(2);
            Game.FridgeDoors.Lower.SetOpen(false); Game.FridgeDoors.Upper.SetOpen(false);
            yield return new WaitForSeconds(.6f);
        }
        yield return WalkTo(new Vector3(9,0,-5)); if (!Running) yield break;
        yield return new WaitForSeconds(3);
        Heading = "Demo complete / Your turn";
        Explanation = "Take control. Shift-run into a dining table to spill cups, compare sounds, or replay the tour. Real music, voice and SFX are still placeholders.";
        Running = false; Direction = Vector3.zero; routine = null;
    }
    private IEnumerator WalkTo(Vector3 destination)
    {
        List<Vector3> path = Plan(destination);
        if (path == null) { Fail("No clear path to " + destination); yield break; }
        foreach (Vector3 point in path)
        {
            float deadline = Time.time + 4;
            while (Vector3.ProjectOnPlane(point-Game.transform.position,Vector3.up).magnitude > .25f)
            {
                if (Game.FreezeForVoice) { deadline += Time.deltaTime; Direction=Vector3.zero; yield return null; continue; }
                if (Time.time > deadline) { Fail("Movement blocked at " + Game.transform.position + " toward " + point); yield break; }
                Direction = Vector3.ProjectOnPlane(point-Game.transform.position,Vector3.up).normalized;
                yield return null;
            }
        }
        Direction = Vector3.zero;
    }
    private void Fail(string reason)
    {
        Running=false;Direction=Vector3.zero;Heading="Route obstructed";
        Explanation="A prop blocks the tour. Reset and replay, or take manual control to move it.";
        Debug.Log("BAD SHIFT tour: " + reason);
    }
    private List<Vector3> Plan(Vector3 destination)
    {
        const float step=.75f;
        Vector2Int start=new Vector2Int(Mathf.RoundToInt(Game.transform.position.x/step),Mathf.RoundToInt(Game.transform.position.z/step));
        Vector2Int goal=new Vector2Int(Mathf.RoundToInt(destination.x/step),Mathf.RoundToInt(destination.z/step));
        var parents=new Dictionary<Vector2Int,Vector2Int>();var queue=new Queue<Vector2Int>();queue.Enqueue(start);parents[start]=start;
        CharacterController c=Game.GetComponent<CharacterController>();
        Vector2Int[] directions={Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right};
        while(queue.Count>0)
        {
            Vector2Int current=queue.Dequeue();
            if(current==goal)
            {
                var path=new List<Vector3>();while(current!=start){path.Add(new Vector3(current.x*step,0,current.y*step));current=parents[current];}path.Reverse();return path;
            }
            foreach(Vector2Int direction in directions)
            {
                Vector2Int next=current+direction;if(parents.ContainsKey(next)||Mathf.Abs(next.x)>14||Mathf.Abs(next.y)>12)continue;
                Vector3 a=new Vector3(current.x*step,.08f,current.y*step),b=new Vector3(next.x*step,.08f,next.y*step);
                if(Physics.CheckCapsule(b+Vector3.up*c.radius,b+Vector3.up*(c.height-c.radius),c.radius,~(1<<2),QueryTriggerInteraction.Ignore))continue;
                if(Physics.CapsuleCast(a+Vector3.up*c.radius,a+Vector3.up*(c.height-c.radius),c.radius,(b-a).normalized,step,~(1<<2),QueryTriggerInteraction.Ignore))continue;
                parents[next]=current;queue.Enqueue(next);
            }
        }
        return null;
    }
    private void OnDisable() { Stop(); }
}
