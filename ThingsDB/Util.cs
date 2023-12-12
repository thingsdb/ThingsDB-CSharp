using MessagePack;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ThingsDB
{
    internal class Util
    {
        internal static async Task<TResult> TimeoutAfter<TResult>(Task<TResult> task, TimeSpan timeout)
        {
            using var timeoutCancellationTokenSource = new CancellationTokenSource();

            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));
            if (completedTask == task)
            {
                timeoutCancellationTokenSource.Cancel();
                return await task;
            }
            else
            {
                throw new TimeoutException("The operation has timed out.");
            }
        }
    }
    public class Unpack
    {
        public static T Deserialize<T>(byte[] bytes)
        {
            return MessagePackSerializer.Deserialize<T>(bytes);
        }
        public static bool IsNil(byte[] bytes)
        {
            return MessagePackSerializer.Deserialize<object>(bytes) == null;
        }
    }
}
