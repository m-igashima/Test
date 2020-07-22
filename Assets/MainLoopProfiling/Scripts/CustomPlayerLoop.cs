using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.Profiling;



// プレイヤーループのカスタマイズ
// 参考URL https://www.patreon.com/posts/unity-2018-1-16336053
namespace UTJ
{
    public class CustomPlayerLoop
    {

        public class ProfilingUpdate
        {
            private float startTime;
            private float executeTime;
            private float endTime;
            public void Start()
            {
                this.startTime = Time.realtimeSinceStartup;
            }
            public void End()
            {
                // for physics.Update
                endTime = Time.realtimeSinceStartup;
                executeTime += this.endTime - this.startTime;
            }
            public void Reset()
            {
                executeTime = 0.0f;
            }
            public float GetExecuteTime()
            {
                return executeTime;
            }
            public float GetStartTime()
            {
                return startTime;
            }
            public float GetEndTime()
            {
                return endTime;
            }
        }

        private static Dictionary<System.Type, ProfilingUpdate> profilingSubSystem;
        private static float loopStartTime;
        //
        private static Dictionary<System.Type, float> prevSubSystemExecuteTime;
        private static float prevLoopExecuteTime;
        private static float gfxWaitForPresentExecOnFinishRendering;

        // culling Start point
        private static float firstPreCullingPoint = 0.0f;


        public static void Init()
        {
            System.Type[] profilePoints = {
                // script
                typeof( UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate),
                typeof( UnityEngine.PlayerLoop.PreLateUpdate.ScriptRunBehaviourLateUpdate),
                typeof( UnityEngine.PlayerLoop.FixedUpdate.ScriptRunBehaviourFixedUpdate),
                // script (Coroutine)
                typeof( UnityEngine.PlayerLoop.Update.ScriptRunDelayedDynamicFrameRate),
                // Animator
                typeof(UnityEngine.PlayerLoop.PreLateUpdate.DirectorUpdateAnimationBegin),
                typeof(UnityEngine.PlayerLoop.PreLateUpdate.DirectorUpdateAnimationEnd),
                // Renderer
                typeof(UnityEngine.PlayerLoop.PostLateUpdate.UpdateAllRenderers),
                typeof(UnityEngine.PlayerLoop.PostLateUpdate.UpdateAllSkinnedMeshes),
                // Rendering(require)
                typeof(UnityEngine.PlayerLoop.PostLateUpdate.FinishFrameRendering),
                // Physics
                typeof( UnityEngine.PlayerLoop.FixedUpdate.PhysicsFixedUpdate),
            };

#if UNITY_2019_3_OR_NEWER
            var playerLoop = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
#else
            var playerLoop = PlayerLoop.GetDefaultPlayerLoop();
#endif

            AppendProfilingLoopSystem(ref playerLoop, profilePoints);
            UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(playerLoop);
        }

        public static float GetLastExecuteTime()
        {
            return prevLoopExecuteTime;
        }
        public static float GetGfxWaitForPresent()
        {
            return gfxWaitForPresentExecOnFinishRendering;
        }

        // For Android MultiThreadedRendering
        public static void OnPreCulling()
        {
            if (firstPreCullingPoint == 0.0f)
            {
                firstPreCullingPoint = Time.realtimeSinceStartup;
            }
        }

        public static float GetProfilingTime<T>()
        {
            return GetProfilingTime(typeof(T));
        }
        public static float GetProfilingTime(System.Type t)
        {
            float time = 0.0f;
            if (prevSubSystemExecuteTime.TryGetValue(t, out time))
            {
                return time;
            }
            return 0.0f;
        }

        private static void AppendProfilingLoopSystem(ref UnityEngine.LowLevel.PlayerLoopSystem playerLoop, System.Type[] profilePoints)
        {
            profilingSubSystem = new Dictionary<System.Type, ProfilingUpdate>();
            prevSubSystemExecuteTime = new Dictionary<System.Type, float>();
            // Add target subsytems
            for (int i = 0; i < profilePoints.Length; ++i)
            {
                profilingSubSystem.Add(profilePoints[i], new ProfilingUpdate());
                prevSubSystemExecuteTime.Add(profilePoints[i], 0.0f);
            }

            // FinishRendering is required point.
            System.Type finishRenderingType = typeof(UnityEngine.PlayerLoop.PostLateUpdate.FinishFrameRendering);
            if (!profilingSubSystem.ContainsKey(finishRenderingType))
            {
                profilingSubSystem.Add(finishRenderingType, new ProfilingUpdate());
                prevSubSystemExecuteTime.Add(finishRenderingType, 0.0f);
            }


            // Note: this also resets the loop to its defalt state first.        
            var newSystems = new List<UnityEngine.LowLevel.PlayerLoopSystem>();
            for (int i = 0; i < playerLoop.subSystemList.Length; ++i)
            {
                var subSystem = playerLoop.subSystemList[i];

                newSystems.Clear();
                if (i == 0)
                {
                    newSystems.Add(new UnityEngine.LowLevel.PlayerLoopSystem
                    {
                        updateDelegate = Loop1stPoint
                    });
                }
                for (int j = 0; j < subSystem.subSystemList.Length; ++j)
                {
                    var subsub = subSystem.subSystemList[j];
                    ProfilingUpdate updateObj;
                    if (profilingSubSystem.TryGetValue(subsub.type, out updateObj))
                    {
                        // SamplingStart - Exec - SamplingEnd
                        newSystems.Add(new UnityEngine.LowLevel.PlayerLoopSystem
                        {
                            type = typeof(ProfilingUpdate),
                            updateDelegate = updateObj.Start
                        });
                        newSystems.Add(subsub);
                        newSystems.Add(new UnityEngine.LowLevel.PlayerLoopSystem
                        {
                            type = typeof(ProfilingUpdate),
                            updateDelegate = updateObj.End
                        });
                    }
                    else
                    {
                        newSystems.Add(subsub);
                    }
                }

                if (i == playerLoop.subSystemList.Length - 1)
                {
                    newSystems.Add(new UnityEngine.LowLevel.PlayerLoopSystem
                    {
                        updateDelegate = LoopLastPoint
                    });
                }
                subSystem.subSystemList = newSystems.ToArray();
                playerLoop.subSystemList[i] = subSystem;
            }
        }
        private static void Loop1stPoint()
        {
            loopStartTime = Time.realtimeSinceStartup;
        }

        private static void LoopLastPoint()
        {
            var finishRenderProfiling = profilingSubSystem[typeof(UnityEngine.PlayerLoop.PostLateUpdate.FinishFrameRendering)];
            // get finish render profiling time to exclude profiler execute time .
            float endTime = finishRenderProfiling.GetEndTime();
            foreach (var kv in profilingSubSystem)
            {
                prevSubSystemExecuteTime[kv.Key] = kv.Value.GetExecuteTime();
                kv.Value.Reset();
            }
            prevLoopExecuteTime = endTime - loopStartTime;

            if (firstPreCullingPoint != 0.0f)
            {
                float finishRenderingStart = finishRenderProfiling.GetStartTime();
                gfxWaitForPresentExecOnFinishRendering = firstPreCullingPoint - finishRenderingStart;
            }
            else
            {
                gfxWaitForPresentExecOnFinishRendering = 0.0f;
            }
            firstPreCullingPoint = 0.0f;
//            SystemInfo.graphicsMultiThreaded
        }
    }
}