using System;
using System.Collections.Generic;
using TShockAPI;
using TShockAPI.DB;

namespace RegionExtension.Database
{
    public enum RegionOperationKind
    {
        Define,
        Delete,
        Rename,
        Move,
        AllowUser,
        RemoveUser,
        AllowGroup,
        RemoveGroup,
        SetZ,
        Protect,
        Resize,
        ChangeOwner
    }

    public enum RegionOperationStage
    {
        Before,
        After
    }

    public sealed class RegionOperationEventArgs : EventArgs
    {
        private readonly Dictionary<string, object> _data;

        public RegionOperationKind Operation { get; }
        public RegionOperationStage Stage { get; }
        public TSPlayer Executor { get; }
        public Region Region { get; }
        public IReadOnlyDictionary<string, object> Data => _data;
        public bool Cancel { get; set; }
        public string CancelReason { get; set; }

        public RegionOperationEventArgs(
            RegionOperationKind operation,
            RegionOperationStage stage,
            TSPlayer executor,
            Region region,
            Dictionary<string, object> data = null)
        {
            Operation = operation;
            Stage = stage;
            Executor = executor;
            Region = region;
            _data = data ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        public T GetData<T>(string key, T fallback = default)
        {
            if (!_data.TryGetValue(key, out var value) || value is not T typed)
                return fallback;
            return typed;
        }
    }
}
