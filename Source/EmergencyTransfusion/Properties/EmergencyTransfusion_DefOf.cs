using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace EmergencyTransfusion.Properties
{
    [DefOf]
    public class EmergencyTransfusion_DefOf
    {
        static EmergencyTransfusion_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(EmergencyTransfusion_DefOf));
        }
        
        [UsedImplicitly]
        public static JobDef ET_TransfuseBlood = null!;
    }
}