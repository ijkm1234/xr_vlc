using UnityEngine;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Linq;

namespace XRVLC
{
    /// <summary>
    /// 用于将后台线程的操作调度到 Unity 主线程执行的工具类。
    /// 必须在场景中有一个 GameObject 挂载此脚本。
    /// </summary>
    public class Loom : MonoBehaviour
    {
        public static int maxThreads = 8;
        private static int numThreads;
        
        private static Loom _current;
        private int _count;
        public static Loom Current
        {
            get
            {
                Initialize();
                return _current;
            }
        }

        private void Awake()
        {
            _current = this;
            initialized = true;
        }

        static bool initialized;

        public static void Initialize()
        {
            if (!initialized)
            {
                if (!Application.isPlaying) return;
                
                initialized = true;
                var go = new GameObject("Loom");
                _current = go.AddComponent<Loom>();
                DontDestroyOnLoad(go);
            }
        }

        private struct NoDelayedQueueItem
        {
            public Action action;
        }

        private List<NoDelayedQueueItem> _actions = new List<NoDelayedQueueItem>();
        private List<NoDelayedQueueItem> _currentActions = new List<NoDelayedQueueItem>();

        public static void QueueOnMainThread(Action action)
        {
            QueueOnMainThread(action, 0f);
        }
        
        public static void QueueOnMainThread(Action action, float time)
        {
            if(time != 0)
            {
                lock(Current._delayed)
                {
                    Current._delayed.Add(new DelayedQueueItem { time = Time.time + time, action = action});
                }
            }
            else
            {
                lock (Current._actions)
                {
                    Current._actions.Add(new NoDelayedQueueItem { action = action });
                }
            }
        }

        public static Thread RunAsync(Action a)
        {
            Initialize();
            while(numThreads >= maxThreads)
            {
                Thread.Sleep(1);
            }
            Interlocked.Increment(ref numThreads);
            ThreadPool.QueueUserWorkItem(RunAction, a);
            return null;
        }

        private static void RunAction(object action)
        {
            try
            {
                ((Action)action)();
            }
            catch(Exception e)
            {
                Debug.LogError("Error in RunAsync: " + e.Message);
            }
            finally
            {
                Interlocked.Decrement(ref numThreads);
            }
        }

        private struct DelayedQueueItem
        {
            public float time;
            public Action action;
        }
        private List<DelayedQueueItem> _delayed = new List<DelayedQueueItem>();
        private List<DelayedQueueItem> _currentDelayed = new List<DelayedQueueItem>();

        void Update()
        {
            lock (_actions)
            {
                _currentActions.Clear();
                _currentActions.AddRange(_actions);
                _actions.Clear();
            }
            foreach(var a in _currentActions)
            {
                a.action();
            }

            lock(_delayed)
            {
                _currentDelayed.Clear();
                _currentDelayed.AddRange(_delayed.Where(d=>d.time <= Time.time));
                foreach(var item in _currentDelayed)
                {
                    _delayed.Remove(item);
                }
            }
            foreach(var delayed in _currentDelayed)
            {
                delayed.action();
            }
        }
    }
}