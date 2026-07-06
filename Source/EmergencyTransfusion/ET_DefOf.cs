using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace EmergencyTransfusion;

[DefOf]
public class ET_DefOf
{
    static ET_DefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ET_DefOf));
    }
        
    [UsedImplicitly]
    public static JobDef ET_TransfuseBlood = null!;
    
    [UsedImplicitly]
    public static RecipeDef BloodTransfusion = null!;
}