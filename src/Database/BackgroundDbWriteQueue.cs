using System;
using System.Collections.Concurrent;
using System.Data;
using System.Threading;
using TShockAPI;

namespace RegionExtension.Database
{
    internal sealed class BackgroundDbWriteQueue : IDisposable
    {
        private readonly BlockingCollection<WorkItem> _queue = new();
        private readonly Thread _worker;
        private readonly Func<IDbConnection> _connectionFactory;
        private readonly string _scope;
        private bool _disposed;

        public BackgroundDbWriteQueue(Func<IDbConnection> connectionFactory, string scope)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _worker = new Thread(WorkLoop)
            {
                IsBackground = true,
                Name = $"{scope}.DbWriteQueue"
            };
            _worker.Start();
        }

        public bool TryEnqueue(Action<IDbConnection> writeAction)
        {
            return TryEnqueue(writeAction, null);
        }

        public void Flush()
        {
            if (_disposed)
                return;

            using var completion = new ManualResetEventSlim(false);
            if (!TryEnqueue(static _ => { }, completion))
                return;

            completion.Wait(TimeSpan.FromSeconds(5));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _queue.CompleteAdding();
            _worker.Join(TimeSpan.FromSeconds(5));
            _queue.Dispose();
        }

        private bool TryEnqueue(Action<IDbConnection> writeAction, ManualResetEventSlim completion)
        {
            if (_disposed || writeAction == null)
                return false;

            try
            {
                _queue.Add(new WorkItem(writeAction, completion));
                return true;
            }
            catch (InvalidOperationException)
            {
                completion?.Set();
                return false;
            }
        }

        private void WorkLoop()
        {
            try
            {
                using var connection = _connectionFactory();
                foreach (var item in _queue.GetConsumingEnumerable())
                {
                    try
                    {
                        item.WriteAction(connection);
                    }
                    catch (Exception ex)
                    {
                        TShock.Log.Error($"[{_scope}][DB] Background write failed: {ex.Message}");
                    }
                    finally
                    {
                        item.Completion?.Set();
                    }
                }
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"[{_scope}][DB] Write queue stopped: {ex.Message}");
            }
        }

        private readonly record struct WorkItem(Action<IDbConnection> WriteAction, ManualResetEventSlim Completion);
    }
}
