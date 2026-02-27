using RegionExtension.Commands.Parameters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TShockAPI;

namespace RegionExtension.Commands.SubCommands
{
    public class RestoreSubCommand : SubCommand
    {
        public override string[] Names => new[] { "restore", "res" };
        public override string Description => "RegionRestoreDesc";

        public override void InitializeParams()
        {
            _params = new ICommandParam[]
            {
                new StringParam("regionname", "name of region. Name must be exact same"),
            };
        }

        public override void Execute(CommandArgsExtension args)
        {
            var region = (string)Params[0].Value;
            RestoreRegion(args, region);
        }

        private void RestoreRegion(CommandArgsExtension args, string region)
        {
            var reg = args.Context.RegionManager.DeletedRegions.GetRegionByName(region);
            if(reg == null)
            {
                args.Player.SendErrorMessage("Failed found region '{0}'!".SFormat(region));
                return;
            }
            args.Context.RegionManager.DeletedRegions.RemoveRegionFromDeleted(reg.Region.ID);
            string newName;
            if(!Utils.TryAutoComplete(args.Context.Config, region, reg.Region.Area, out newName))
            {
                args.Player.SendErrorMessage("Region '{0}' already exist!".SFormat(region));
                return;
            }
            reg.Region.Name = newName;
            if (args.Context.RegionManager.DefineRegion(args, reg.Region))
                args.Player.SendSuccessMessage("Region restored '{0}'!".SFormat(region));
            else
                args.Player.SendErrorMessage("Failed restore region!");
        }
    }
}

