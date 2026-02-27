using System;
using System.Collections.Generic;
using TShockAPI;
using TShockAPI.DB;

namespace RegionExtension.Database.Modules
{
    public interface IRegionRequest
    {
        Region Region { get; }
        UserAccount User { get; }
        DateTime DateCreation { get; }
    }

    public interface IRegionRequestManager
    {
        IReadOnlyList<IRegionRequest> Requests { get; }
        bool AddRequest(Region region, UserAccount user);
        bool DeleteRequest(Region region);
    }

    public interface IRegionTriggerManager
    {
        void HandleRegionDeleted(Region region);
    }

    public interface IRegionPropertyManager
    {
        void HandleRegionDeleted(Region region);
    }
}
