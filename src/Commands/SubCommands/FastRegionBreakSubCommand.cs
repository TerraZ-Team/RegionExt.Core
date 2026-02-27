using RegionExtension.Commands.Parameters;
using RegionExtension.Infrastructure;
using TShockAPI;
using TShockAPI.DB;

namespace RegionExtension.Commands.SubCommands
{
    internal class FastRegionBreakSubCommand : SubCommand
    {
        public override string[] Names => new[] { "fastregionbreak", "frb" };
        public override string Description => "BreakFRRequestDesc";

        public override void Execute(CommandArgsExtension args)
        {
            var id = FastRegionLookup.FindByUser(args.Player.Account, args.Context.FastRegions);
            if (id == -1)
                args.Player.SendInfoMessage("You dont have fast region request.");
            else
            {
                args.Context.FastRegions.RemoveAt(id);
                args.Player.SendInfoMessage("Fast region request removed.");
            }
        }
    }
}

