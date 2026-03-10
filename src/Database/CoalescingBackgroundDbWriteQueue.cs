using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using TShockAPI;

namespace RegionExtension.Database
{
    internal sealed class CoalescingBackgroundDbWriteQueue<TKey> : IDisposable where TKey : notnull
    {
        private readonly BlockingCollection<TKey> _queue = new();
        private readonly Dictionary<TKey, Action<IDbConnection>> _pendingWrites = new();
        private readonly object _sync = new();
        private readonly Thread _worker;
        private readonly Func<IDbConnection> _connectionFactory;
        private readonly string _scope;
        private bool _disposed;

        public CoalescingBackgroundDbWriteQueue(Func<IDbConnection> connectionFactory, string scope)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _worker = new Thread(WorkLoop)
            {
                IsBackground = true,
                Name = $"{scope}.CoalescingDbWriteQueue"
            };
            _worker.Start();
        }

        public bool TryEnqueueOrReplace(TKey key, Action<IDbConnection> writeAction)
        {
            if (writeAction == null)
                return false;

            lock (_sync)
            {
                if (_disposed)
                    return false;

                var shouldEnqueue = !_pendingWrites.ContainsKey(key);
                _pendingWrites[key] = writeAction;

                if (!shouldEnqueue)
                    return true;

                try
                {
                    _queue.Add(key);
                    return true;
                }
                catch (InvalidOperationException)
                {
                    _pendingWrites.Remove(key);
                    return false;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            lock (_sync)
            {
                if (_disposed)
                    return;

                _disposed = true;
            }

            _queue.CompleteAdding();
            _worker.Join(TimeSpan.FromSeconds(5));
            _queue.Dispose();
        }

        private void WorkLoop()
        {
            try
            {
                using var connection = _connectionFactory();
                foreach (var key in _queue.GetConsumingEnumerable())
                {
                    Action<IDbConnection> writeAction;
                    lock (_sync)
                    {
                        if (!_pendingWrites.Remove(key, out writeAction))
                            continue;
                    }

                    try
                    {
                        writeAction(connection);
                    }
                    catch (Exception ex)
                    {
                        TShock.Log.Error($"[{_scope}][DB] Background write failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"[{_scope}][DB] Write queue stopped: {ex.Message}");
            }
        }
    }
}
