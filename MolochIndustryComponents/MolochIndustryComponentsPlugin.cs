namespace MolochIndustryComponents;

using HarmonyLib;
using Railloader;
using Serilog;

public class MolochIndustryComponents : SingletonPluginBase<MolochIndustryComponents>
{
    static ILogger logger = Log.ForContext<MolochIndustryComponents>();

    public MolochIndustryComponents()
    {
        new Harmony("Moloch.MolochIndustryComponents").PatchAll(GetType().Assembly);
    }

}

