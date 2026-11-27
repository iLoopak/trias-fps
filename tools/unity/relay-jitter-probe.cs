using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Profiling;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Trias.Native
{
    [DefaultExecutionOrder(1000)]
    public sealed class RelayJitterProbe : MonoBehaviour
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        const int Samples = 240;
        TriasClient client;
        TriasWorld world;
        FieldInfo state;
        Vector3 eye, aim;
        bool fixedView;
        string output;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Activate()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TRIAS_RELAY_JITTER")))
                new GameObject("Relay jitter probe").AddComponent<RelayJitterProbe>();
        }

        static FieldInfo Field(string name) => typeof(TriasClient).GetField(name,Fields);
        WireValue Snapshot => state.GetValue(client) as WireValue;

        IEnumerator Start()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            output=Environment.GetEnvironmentVariable("TRIAS_RELAY_JITTER");
            Directory.CreateDirectory(output);
            Application.runInBackground=true;
            QualitySettings.SetQualityLevel(QualitySettings.names.Length-1,true);
            string mode=Environment.GetEnvironmentVariable("TRIAS_RELAY_JITTER_MODE");
            bool pacing=mode=="pacing";
            bool production=mode=="production" || pacing;
            if (!production) QualitySettings.vSyncCount=0;
            Application.targetFrameRate=-1;
            Screen.SetResolution(1280,720,FullScreenMode.Windowed);
            client=FindFirstObjectByType<TriasClient>();
            state=Field("state");
            yield return new WaitForSecondsRealtime(1);
            string address=Environment.GetEnvironmentVariable("TRIAS_ENV_SERVER") ?? "ws://127.0.0.1:3087";
            client.Join(address,"JITTER"+(DateTime.UtcNow.Ticks%100000000),"Relay profile","relay","tdm","vector",true,false);
            float deadline=Time.realtimeSinceStartup+20;
            while (Time.realtimeSinceStartup < deadline)
            {
                world=Field("world").GetValue(client) as TriasWorld;
                if (world?.Spec.id=="relay" && Snapshot?["players"].Count==12) break;
                yield return null;
            }
            if (world?.Spec.id!="relay" || Snapshot?["players"].Count!=12)
                throw new InvalidOperationException("Expected a Relay bot match with 12 players.");
            if (BlenderParts.ProfileRuns.Count == 0)
                throw new InvalidOperationException("Renderer-toggle diagnostic overlay is missing.");
            Field("deploymentUntil").SetValue(client,0f);
            var focus=typeof(TriasClient).GetMethod("Capture",Fields);
            focus.Invoke(client,new object[]{true});
            yield return new WaitForSecondsRealtime(2);
            WriteColliders("live-start");
            File.WriteAllText(Path.Combine(output,"setup.txt"),$"{Application.unityVersion} {SystemInfo.graphicsDeviceName} "+
                $"{Screen.width}x{Screen.height} {QualitySettings.names[QualitySettings.GetQualityLevel()]} "+
                $"players={Snapshot["players"].Count} rooms=1 samples={Samples} runs={BlenderParts.ProfileRuns.Count} vsync={QualitySettings.vSyncCount}\n");

            var roof=world.Spec.cover.First(box=>box.structure=="roof");
            Quaternion rotation=Quaternion.Euler(0,roof.angle*Mathf.Rad2Deg,0);
            Vector3 Hut(float x,float y,float z)=>roof.Position+rotation*new Vector3(x,y,z);
            var shaftEye=new Vector3(7.5f,4.88f,-14.75f);
            var shaftAim=new Vector3(-.2f,.5f,-1.95f);
            var hutEye=Hut(.8f,1.58f,-6.3f);
            var hutAim=Hut(-.15f,1.36f,.55f);
            if (pacing)
            {
                yield return Pacing("shaft",shaftEye,shaftAim,false);
                yield return Pacing("hut-entry",hutEye,hutAim,true);
            }
            else
            {
                yield return Live("shaft",shaftEye,shaftAim,false);
                yield return Live("hut-entry",hutEye,hutAim,true);
            }
            WriteColliders("live-end");
            if (production)
            {
                Finish();
                yield break;
            }

            // Keep the exact last actors and geometry in place while stopping ingestion and gameplay updates.
            client.enabled=false;
            foreach (var animation in world.Root.GetComponentsInChildren<Animation>()) animation.enabled=false;
            foreach (var animation in world.Root.GetComponentsInChildren<Animator>()) animation.enabled=false;
            foreach (var particle in world.Root.GetComponentsInChildren<ParticleSystem>()) particle.Pause();
            BlenderParts.ProfileSetBatching(true);
            foreach (string view in new[]{"shaft","hut-entry"})
            {
                eye=view=="shaft"?shaftEye:hutEye;
                aim=view=="shaft"?shaftAim:hutAim;
                for (int i=0;i<4;i++)
                    yield return Measure($"frozen-{view}-{i+1}",-1,false,false);
                for (int i=0;i<2;i++)
                    yield return Measure($"paced60-{view}-{i+1}",60,false,false);
            }
            WriteColliders("frozen-end");
            Finish();
        }

        void Finish()
        {
            client.Disconnect(null);
            Debug.Log("RELAY JITTER COMPLETE");
            Application.Quit(0);
        }

        IEnumerator Live(string name,Vector3 position,Vector3 target,bool reverse)
        {
            eye=position;
            aim=target;
            fixedView=true;
            for (int i=0;i<4;i++)
            {
                bool batched=(i==1 || i==2)^reverse;
                BlenderParts.ProfileSetBatching(batched);
                yield return Measure($"live-{name}-{i+1}-{(batched?"batched":"separate")}",-1,true,true);
            }
        }

        IEnumerator Pacing(string name,Vector3 position,Vector3 target,bool reverse)
        {
            eye=position;
            aim=target;
            fixedView=true;
            BlenderParts.ProfileSetBatching(true);
            for (int i=0;i<4;i++)
            {
                bool synchronised=(i==0 || i==3)^reverse;
                QualitySettings.vSyncCount=synchronised?1:0;
                yield return Measure($"live-{name}-{i+1}-{(synchronised?"vsync":"software60")}",
                    synchronised?-1:60,true,false);
            }
            QualitySettings.vSyncCount=1;
        }

        void LateUpdate()
        {
            if (fixedView) client.view.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(aim-eye));
        }

        void WriteColliders(string name)
        {
            Physics.SyncTransforms();
            File.WriteAllLines(Path.Combine(output,name+"-collision.txt"),world.Root.GetComponentsInChildren<Collider>()
                .Where(c=>c.enabled).Select(c=>$"{c.GetType().Name}|{c.transform.position:F5}|{c.transform.rotation:F5}|{c.bounds.center:F5}|{c.bounds.size:F5}")
                .OrderBy(line=>line,StringComparer.Ordinal));
        }

        IEnumerator Measure(string key,int rate,bool live,bool picture)
        {
            Application.targetFrameRate=rate;
            for (int i=0;i<55;i++) yield return null;
            using var draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count");
            using var passes=ProfilerRecorder.StartNew(ProfilerCategory.Render,"SetPass Calls Count");
            using var tris=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count");
            using var allocated=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame");
            var timing=new FrameTiming[1];
            var samples=new Frame[Samples];
            uint ticks=0;
            WireValue previous=null;
            for (int i=0;i<samples.Length;i++)
            {
                long start=Stopwatch.GetTimestamp();
                FrameTimingManager.CaptureFrameTimings();
                yield return null;
                FrameTimingManager.GetLatestTimings(1,timing);
                var snapshot=live?Snapshot:null;
                if (live && !ReferenceEquals(previous,snapshot)) ticks++;
                previous=snapshot;
                samples[i]=new Frame((Stopwatch.GetTimestamp()-start)*1000d/Stopwatch.Frequency,timing[0],
                    draws.LastValue,passes.LastValue,tris.LastValue,allocated.Valid?allocated.LastValue:0,
                    GC.CollectionCount(0),Application.isFocused,snapshot?["players"].Count ?? 0);
            }
            File.WriteAllLines(Path.Combine(output,key+".csv"),new[]{"wall_ms,cpu_ms,main_ms,render_ms,gpu_ms,present_wait_ms,"+
                "draws,setpass,triangles,gc_bytes,gc_collections,focused,players,sync_interval"}.Concat(samples.Select(f=>f.Csv())));
            if (picture)
            {
                yield return new WaitForEndOfFrame();
                var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);
                File.WriteAllBytes(Path.Combine(output,key+".png"),image.EncodeToPNG());
                Destroy(image);
            }
            Debug.Log($"RELAY JITTER {key} snapshot changes={ticks}");
        }

        readonly struct Frame
        {
            readonly double wall;
            readonly FrameTiming timing;
            readonly long draws,passes,tris,allocated;
            readonly int collections,players;
            readonly bool focused;

            internal Frame(double wall,FrameTiming timing,long draws,long passes,long tris,long allocated,
                int collections,bool focused,int players)
            {
                this.wall=wall; this.timing=timing; this.draws=draws; this.passes=passes; this.tris=tris; this.allocated=allocated;
                this.collections=collections; this.focused=focused; this.players=players;
            }

            internal string Csv() => $"{wall:F4},{timing.cpuFrameTime:F4},{timing.cpuMainThreadFrameTime:F4},"+
                $"{timing.cpuRenderThreadFrameTime:F4},{timing.gpuFrameTime:F4},{timing.cpuMainThreadPresentWaitTime:F4},"+
                $"{draws},{passes},{tris},{allocated},{collections},{(focused?1:0)},{players},{timing.syncInterval}";
        }
    }
}
