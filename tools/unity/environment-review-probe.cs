using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace Trias.Native
{
    [DefaultExecutionOrder(1000)]
    public sealed class EnvironmentReviewProbe : MonoBehaviour
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        TriasClient client;
        TriasWorld world;
        Vector3 eye, aim;
        bool holdingView;
        string folder;
        string address;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Activate()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TRIAS_ENV_REVIEW")))
                new GameObject("TRIAS environment review").AddComponent<EnvironmentReviewProbe>();
        }

        FieldInfo Field(string name) => typeof(TriasClient).GetField(name, Fields);

        IEnumerator Start()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            folder = Environment.GetEnvironmentVariable("TRIAS_ENV_REVIEW");
            address = Environment.GetEnvironmentVariable("TRIAS_ENV_SERVER") ?? "ws://127.0.0.1:3086";
            Application.runInBackground = true;
            QualitySettings.SetQualityLevel(QualitySettings.names.Length-1, true);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            client = FindFirstObjectByType<TriasClient>();
            yield return new WaitForSecondsRealtime(1.2f);

            yield return Open("relay","tdm");
            var hut = world.Spec.cover.First(box => box.structure == "roof");
            Vector3 Hut(float x,float y,float z) => hut.Position +
                Quaternion.Euler(0,hut.angle*Mathf.Rad2Deg,0)*new Vector3(x,y,z);
            yield return View("relay-hut-entry",Hut(.8f,1.58f,-6.3f),Hut(-.15f,1.36f,.55f));
            yield return View("relay-hut-interior",Hut(-2.1f,1.58f,-1.8f),Hut(.15f,1.39f,2.8f));
            yield return View("relay-transfer",new Vector3(1.7f,8.18f,-15.8f),new Vector3(.3f,7.3f,-5.7f));

            yield return Open("island","ctf");
            Vector3 On(float x,float z,float lift) => new Vector3(x,world.Height(x,z)+lift,z);
            yield return View("kordilera-crossing",On(27.5f,11.3f,1.58f),On(23.4f,6.6f,.68f));
            yield return View("kordilera-river",On(19.2f,9.5f,1.58f),On(25.5f,5.1f,1.05f));
            yield return View("kordilera-ridge",On(11f,-21.7f,1.58f),On(8.112f,-23.556f,1.2f));

            yield return Open("foundry","tdm");
            yield return View("vanta-approach",new Vector3(-10,1.58f,-24.5f),new Vector3(-6,1.5f,-16));
            yield return Open("basin","tdm");
            yield return View("meridian-approach",new Vector3(-8.8f,1.58f,-37.4f),new Vector3(-5.3f,1.4f,-31));
            if (Environment.GetEnvironmentVariable("TRIAS_MERIDIAN_REVIEW") == "1")
            {
                yield return View("meridian-shelf",new Vector3(-5,1.58f,0),new Vector3(20,15,73));
                yield return View("meridian-unbound",new Vector3(-12,1.58f,-21),new Vector3(20,24,73), clean:true);
                yield return View("meridian-cut",new Vector3(-8.8f,1.58f,-37.4f),new Vector3(-5.3f,1.4f,-31), clean:true);
            }
            if (Environment.GetEnvironmentVariable("TRIAS_ENV_FOOTAGE") == "1")
            {
                yield return Open("relay","tdm");
                yield return Footage("relay",new Vector3(7.9f,4.88f,-19.4f),new Vector3(9.2f,4.88f,-18.7f),
                    new Vector3(11.25f,4.3f,-15f));
                yield return Open("island","ctf");
                yield return Footage("kordilera",On(26.8f,10.4f,1.58f),On(25.35f,9.1f,1.58f),
                    On(23.45f,6.7f,.7f));
            }
            client.Disconnect(null);
            Debug.Log("ENVIRONMENT REVIEW COMPLETE");
            Application.Quit(0);
        }

        IEnumerator Open(string arena,string mode)
        {
            holdingView = false;
            client.Join(address,"DNA"+(DateTime.UtcNow.Ticks%100000000),"TRIAS review",arena,mode,"vector",true,false);
            float until = Time.realtimeSinceStartup+16;
            int players = 0;
            while (Time.realtimeSinceStartup < until)
            {
                world = Field("world").GetValue(client) as TriasWorld;
                var state = Field("state").GetValue(client) as WireValue;
                players = state?["players"].Count ?? 0;
                if (world?.Spec.id == arena && players > 1) break;
                yield return null;
            }
            if (world?.Spec.id != arena || players < 2) throw new InvalidOperationException("No live bot match for "+arena);
            Field("captured").SetValue(client,true);
            Field("deploymentUntil").SetValue(client,0f);
            yield return new WaitForSecondsRealtime(1.6f);
            var colliders = world.Root.GetComponentsInChildren<Collider>().Where(c => c.enabled);
            File.WriteAllLines(Path.Combine(folder,arena+"-collision.txt"),colliders
                .Select(c => $"{c.GetType().Name}|{c.transform.position:F5}|{c.transform.rotation:F5}|{c.bounds.center:F5}|{c.bounds.size:F5}")
                .OrderBy(line=>line));
            Debug.Log($"ENVIRONMENT LOADED {arena} players={players}");
        }

        void LateUpdate()
        {
            if (holdingView) client.view.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(aim-eye));
        }

        IEnumerator Footage(string key,Vector3 start,Vector3 end,Vector3 target)
        {
            string path=Path.Combine(folder,key+"-frames");
            Directory.CreateDirectory(path);
            aim=target;
            eye=start;
            holdingView=true;
            for(int frame=0;frame<72;frame++)
            {
                eye=Vector3.Lerp(start,end,frame/71f);
                yield return new WaitForSecondsRealtime(1f/18f);
                yield return new WaitForEndOfFrame();
                var shot=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
                shot.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);
                File.WriteAllBytes(Path.Combine(path,$"{frame:D3}.jpg"),shot.EncodeToJPG(91));
                Destroy(shot);
            }
        }

        IEnumerator View(string key,Vector3 position,Vector3 target,bool clean = false)
        {
            eye=position;
            aim=target;
            holdingView=true;
            for(int i=0;i<55;i++) yield return null;
            yield return new WaitForEndOfFrame();
            var shot=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
            if (clean)
            {
                var camera = client.view;
                var previous = camera.targetTexture;
                var active = RenderTexture.active;
                var targetTexture = RenderTexture.GetTemporary(shot.width,shot.height,24);
                try
                {
                    camera.targetTexture = targetTexture;
                    camera.Render();
                    RenderTexture.active = targetTexture;
                    shot.ReadPixels(new Rect(0,0,shot.width,shot.height),0,0);
                }
                finally
                {
                    camera.targetTexture = previous;
                    RenderTexture.active = active;
                    RenderTexture.ReleaseTemporary(targetTexture);
                }
            }
            else shot.ReadPixels(new Rect(0,0,shot.width,shot.height),0,0);
            File.WriteAllBytes(Path.Combine(folder,key+".jpg"),shot.EncodeToJPG(94));
            string resolution=$"{shot.width}x{shot.height}";
            Destroy(shot);

            using var draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count");
            using var triangles=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count");
            using var main=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread");
            var timing=new FrameTiming[1];
            var stats=new string[181];
            stats[0]="frame_ms,cpu_ms,gpu_ms,draws,triangles,main_ms,allocated_bytes";
            for(int i=1;i<stats.Length;i++)
            {
                FrameTimingManager.CaptureFrameTimings();
                yield return null;
                uint count=FrameTimingManager.GetLatestTimings(1,timing);
                stats[i]=$"{Time.unscaledDeltaTime*1000:F4},{(count>0?timing[0].cpuFrameTime:0):F4},"+
                    $"{(count>0?timing[0].gpuFrameTime:0):F4},{(draws.Valid?draws.LastValue:0)},"+
                    $"{(triangles.Valid?triangles.LastValue:0)},{(main.Valid?main.LastValue/1000000d:0):F4},"+
                    $"{Profiler.GetTotalAllocatedMemoryLong()}";
            }
            File.WriteAllLines(Path.Combine(folder,key+".csv"),stats);
            Debug.Log($"ENVIRONMENT SAMPLE {key} {resolution}");
        }
    }
}
