using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Trias.Native
{
    public sealed class RelayControlledProbe : MonoBehaviour
    {
        const int ActorCount = 12, Warmup = 135, Samples = 300;
        readonly Transform[] pilots = new Transform[ActorCount];
        readonly Animation[] rigs = new Animation[ActorCount];
        Camera view;
        TriasWorld world;
        string output;

        IEnumerator Start()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            output = Environment.GetEnvironmentVariable("TRIAS_RELAY_PROFILE");
            if (string.IsNullOrWhiteSpace(output)) { Application.Quit(2); yield break; }
            Directory.CreateDirectory(output);
            Application.runInBackground = true;
            QualitySettings.SetQualityLevel(QualitySettings.names.Length-1, true);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Screen.SetResolution(1280,720,FullScreenMode.Windowed);
            yield return null;

            view = new GameObject("Fixed Relay eye").AddComponent<Camera>();
            view.tag = "MainCamera";
            view.renderingPath = RenderingPath.Forward;
            view.fieldOfView = 67;
            view.nearClipPlane = .037f;
            view.farClipPlane = 235;
            var data = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TRIAS/World").text);
            world = new TriasWorld(data.maps.Single(map => map.id == "relay"),data,"tdm",view);
            for (int i=0;i<pilots.Length;i++)
            {
                int team = i%3;
                pilots[i] = TriasWorld.Group(world.Actors,$"Scripted pilot {i+1}");
                string kit = data.pilots[i%data.pilots.Length].id;
                if (world.Kit.Pilot(pilots[i],kit,world.TeamLight[team],team,i%2==0,out rigs[i]) == null || rigs[i]["run"] == null)
                    throw new InvalidOperationException("Missing production pilot animation for "+kit);
                var gun = TriasWorld.Group(pilots[i],"Pulse",new Vector3(.18f,1.14f,.34f));
                gun.localScale = Vector3.one*.62f;
                TriasModels.Gun(world,gun,"pulse",world.TeamLight[team]);
            }
            yield return null;
            Physics.SyncTransforms();
            File.WriteAllLines(Path.Combine(output,"collision.txt"),world.Root.GetComponentsInChildren<Collider>()
                .Where(c=>c.enabled).Select(c=>$"{c.GetType().Name}|{c.transform.position:F5}|{c.transform.rotation:F5}|{c.bounds.center:F5}|{c.bounds.size:F5}")
                .OrderBy(line=>line,StringComparer.Ordinal));
            File.WriteAllText(Path.Combine(output,"run.txt"),
                $"{SystemInfo.graphicsDeviceName}, {Application.unityVersion}, {Screen.width}x{Screen.height}, "+
                $"{QualitySettings.names[QualitySettings.GetQualityLevel()]}, {pilots.Length} production pilots, {Samples} frames per view\n");

            var hut = world.Spec.cover.First(box=>box.structure=="roof");
            var hutRotation = Quaternion.Euler(0,hut.angle*Mathf.Rad2Deg,0);
            Vector3 Hut(float x,float y,float z) => hut.Position+hutRotation*new Vector3(x,y,z);
            yield return Measure("shaft",new Vector3(7.5f,4.88f,-14.75f),new Vector3(-.2f,.5f,-1.95f),
                new Vector3(-10,3.3f,-17),new Vector3(4,0,0),new Vector3(0,0,1.35f));
            yield return Measure("bridge",new Vector3(1.7f,8.18f,-15.8f),new Vector3(.3f,7.3f,-5.7f),
                new Vector3(-1.45f,6.6f,-13.1f),new Vector3(1.38f,0,0),new Vector3(0,0,2.13f));
            yield return Measure("hut-entry",Hut(.8f,1.58f,-6.3f),Hut(-.15f,1.36f,.55f),
                Hut(-2.55f,0,-1.8f),hutRotation*new Vector3(2.35f,0,0),hutRotation*new Vector3(0,0,1.20f));
            yield return Measure("hut-interior",Hut(-2.1f,1.58f,-1.8f),Hut(.15f,1.39f,2.8f),
                Hut(-1.12f,0,.20f),hutRotation*new Vector3(1.47f,0,0),hutRotation*new Vector3(0,0,.64f));
            Debug.Log("CONTROLLED RELAY COMPLETE");
            Application.Quit(0);
        }

        IEnumerator Measure(string key,Vector3 eye,Vector3 aim,Vector3 origin,Vector3 across,Vector3 along)
        {
            view.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(aim-eye));
            Vector3 heading=along.normalized;
            void Replay(int frame)
            {
                float time=(frame%120)/60f;
                for (int i=0;i<pilots.Length;i++)
                {
                    float phase=time*Mathf.PI+(i*.75f);
                    pilots[i].SetPositionAndRotation(origin+across*(i%3)+along*(i/3)+heading*(.31f*Mathf.Sin(phase)),
                        Quaternion.LookRotation(heading*(Mathf.Cos(phase)>=0 ? 1 : -1)));
                    var run=rigs[i]["run"];
                    run.enabled=true;
                    run.weight=1;
                    run.time=(time+i*.07f)%run.length;
                    rigs[i].Sample();
                    run.enabled=false;
                }
                world.Animate(50+time,.8f,0,eye,1,.6f,50+time,719);
            }

            for (int frame=0;frame<Warmup;frame++) { Replay(frame); yield return null; }
            using var draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count");
            using var passes=ProfilerRecorder.StartNew(ProfilerCategory.Render,"SetPass Calls Count");
            using var tris=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count");
            var timing=new FrameTiming[1];
            var rows=new Sample[Samples];
            for (int i=0;i<rows.Length;i++)
            {
                long start=Stopwatch.GetTimestamp();
                Replay(i);
                FrameTimingManager.CaptureFrameTimings();
                yield return null;
                FrameTimingManager.GetLatestTimings(1,timing);
                var t=timing[0];
                rows[i]=new Sample((Stopwatch.GetTimestamp()-start)*1000d/Stopwatch.Frequency,
                    t.cpuFrameTime,t.cpuMainThreadFrameTime,t.cpuRenderThreadFrameTime,t.gpuFrameTime,
                    draws.LastValue,passes.LastValue,tris.LastValue);
            }
            File.WriteAllLines(Path.Combine(output,key+".csv"),new[]{"wall_ms,cpu_ms,main_ms,render_ms,gpu_ms,draws,setpass,triangles"}
                .Concat(rows.Select(row => row.Csv())));
            Replay(0);
            yield return new WaitForEndOfFrame();
            var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);
            File.WriteAllBytes(Path.Combine(output,key+".png"),image.EncodeToPNG());
            Destroy(image);
            Debug.Log($"CONTROLLED RELAY {key} draws={rows[Samples-1].Draws}");
        }

        readonly struct Sample
        {
            readonly double wall,cpu,main,render,gpu;
            internal readonly long Draws;
            readonly long passes,triangles;

            internal Sample(double wall,double cpu,double main,double render,double gpu,long draws,long passes,long triangles)
            {
                this.wall=wall; this.cpu=cpu; this.main=main; this.render=render; this.gpu=gpu;
                Draws=draws; this.passes=passes; this.triangles=triangles;
            }

            internal string Csv() => $"{wall:F4},{cpu:F4},{main:F4},{render:F4},{gpu:F4},{Draws},{passes},{triangles}";
        }
    }
}
