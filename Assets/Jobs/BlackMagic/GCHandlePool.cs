using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

/* Black Magic in use to pass references to IJob.
 * See https://qiita.com/tatsunoru/items/611d0378086dc5986249
 * 
 * This will disable Burst compiler; However we may still define regular IJob for e.g. simple algorithms for performance.
 * Consider use regular IJobs before using this black magic.
 * This may cause un-reported bugs (no errors), even crashing Unity; handle with care (like regular parallel programming).
 * Not sure if this will be fixed in further unity updates ...
 */

public static class GCHandlePool
{
    // 使い回すためのスタックコンテナ
    //protected static readonly Stack<GCHandle> stack = new Stack<GCHandle>();
    private static readonly Stack<GCHandle> stack = new Stack<GCHandle>();

    // GCHandleを生成する(プールされたオブジェクトがあるならそれを返す)
    public static GCHandle Create<T>(T value)
    {
        if (stack.Count == 0)
        {
            return GCHandle.Alloc(value);
        }
        else
        {
            var ret = stack.Pop();
            ret.Target = value; // Targetにセットする
            return ret;
        }
    }

    // GCHandleを開放する
    public static void Release(GCHandle value)
    {
        if (value.IsAllocated)
        {
            value.Target = null; // Targetを開放する
            stack.Push(value); // スタックコンテナに積んで次回に使い回す
        }
    }
}

public struct GCHandle<T> : System.IDisposable
{
    GCHandle handle;

    // T型にキャストして返す
    public T Target
    {
        get
        {
            return (T)handle.Target;
        }

        set
        {
            handle.Target = value;
        }
    }

    // Pool経由で作成する
    public void Create(T value)
    {
        handle = GCHandlePool.Create(value);
    }

    // プール経由で開放する
    public void Dispose()
    {
        GCHandlePool.Release(handle);
        handle = default;
    }
}
