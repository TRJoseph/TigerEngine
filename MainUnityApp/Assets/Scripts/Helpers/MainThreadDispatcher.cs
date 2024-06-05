
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

public class MainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> _executionQueue = new Queue<Action>();

    public static void Enqueue(Action action)
    {
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }

    void Update()
    {
        while (_executionQueue.Count > 0)
        {
            Action action = null;
            lock (_executionQueue)
            {
                if (_executionQueue.Count > 0)
                {
                    action = _executionQueue.Dequeue();
                }
            }

            action?.Invoke();
        }
    }
}
