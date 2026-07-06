using System.Linq;
using JetBrains.Annotations;
using RimWorld;
using Verse;
using Verse.AI;

namespace EmergencyTransfusion;

[UsedImplicitly]
public class FloatMenuOptionProvider_EmergencyTransfusion : FloatMenuOptionProvider
{
    protected override bool Drafted => true;
    protected override bool Undrafted => true;
    protected override bool Multiselect => false;
    protected override bool MechanoidCanDo => true;
    protected override bool RequiresManipulation => true;

    protected override bool AppliesInt(FloatMenuContext context)
    {
        return !context.FirstSelectedPawn.IsMutant || context.FirstSelectedPawn.mutant.Def.canTend;
    }

    public override bool TargetPawnValid(Pawn pawn, FloatMenuContext context)
    {
        return base.TargetPawnValid(pawn, context) &&
               IsValidTransfusionTarget(context.FirstSelectedPawn, pawn) &&
               pawn.health.hediffSet.HasHediff(HediffDefOf.BloodLoss);
    }

    protected override FloatMenuOption? GetSingleOptionFor(
        Pawn clickedPawn,
        FloatMenuContext context)
    {
        var bloodLoss = clickedPawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
        if (bloodLoss == null) return null;
        
        if (bloodLoss.Severity < 0.15)
            return new FloatMenuOption(
                "ET_CannotTransfuse".Translate(clickedPawn) + ": " +
                "ET_TransfusionNotRequired".Translate(clickedPawn),
                null);
        if (context.FirstSelectedPawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
            return new FloatMenuOption(
                "ET_CannotTransfuse".Translate(clickedPawn) + ": " +
                "CannotPrioritizeWorkTypeDisabled".Translate(WorkTypeDefOf.Doctor.gerundLabel),
                null);
        if (!context.FirstSelectedPawn.CanReach(clickedPawn, PathEndMode.ClosestTouch, Danger.Deadly))
            return new FloatMenuOption(
                "ET_CannotTransfuse".Translate(clickedPawn) + ": " +
                "NoPath".Translate().CapitalizeFirst(),
                null);
        if (clickedPawn.InAggroMentalState && !clickedPawn.health.hediffSet.HasHediff(HediffDefOf.Scaria))
            return new FloatMenuOption(
                "ET_CannotTransfuse".Translate(clickedPawn) + ": " +
                "PawnIsInAggroMentalState".Translate(clickedPawn).CapitalizeFirst(),
                null);
        
        var bloodpack = FindBloodpack(context.FirstSelectedPawn, clickedPawn);
        if (bloodpack == null)
        {
            return new FloatMenuOption(
                "ET_CannotTransfuse".Translate(clickedPawn) + ": " +
                "ET_NoBlood".Translate().CapitalizeFirst(),
                null);
        }

        return FloatMenuUtility.DecoratePrioritizedTask(
            new FloatMenuOption("ET_Transfuse".Translate(clickedPawn), Transfuse),
            context.FirstSelectedPawn, clickedPawn);

        void Transfuse()
        {
            var job = JobMaker.MakeJob(ET_DefOf.ET_TransfuseBlood, clickedPawn, bloodpack);
            job.count = 1;
            job.draftedTend = true;
            context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job);
        }
    }

    /// <summary>
    /// Checks whether we can ever apply a transfusion to the target.  Does not check if they actually need it,
    /// only if we could potentially give one (i.e. they aren't an animal or a hostile).
    /// </summary>
    /// <param name="doctor">The pawn performing the transfusion</param>
    /// <param name="patient">The pawn receiving the transfusion</param>
    /// <returns>True if we can potentially perform the transfusion</returns>
    private static bool IsValidTransfusionTarget(Pawn doctor, Pawn patient)
    {
        return patient.RaceProps.Humanlike &&
               patient.health.CanBleed &&
               (patient.Downed ||
                    (!patient.HostileTo(doctor.Faction) &&
                    (patient.IsColonist ||
                     patient.IsQuestLodger() ||
                     patient.IsPrisonerOfColony ||
                     patient.IsSlaveOfColony))
               );
    }

    /// <summary>
    /// Locates the closest valid bloodpack
    /// </summary>
    /// <param name="doctor"></param>
    /// <param name="patient"></param>
    /// <returns></returns>
    private static Thing? FindBloodpack(Pawn doctor, Pawn patient)
    {
        // Try to grab from the doctor's or the patient's inventory first
        var bloodpack = GetBloodInInventory(doctor.inventory.innerContainer) ??
                        GetBloodInInventory(patient.inventory.innerContainer);
            
        // Search the map for blood packs
        if (bloodpack == null)
        {
            bloodpack = GenClosest.ClosestThing_Global_Reachable(patient.PositionHeld, patient.MapHeld,
                patient.MapHeld.listerThings.ThingsOfDef(ThingDefOf.HemogenPack),
                PathEndMode.ClosestTouch,
                TraverseParms.For(doctor),
                validator: CanReserve);
        }
            
        // Search caravan animals
        if (bloodpack == null && doctor.IsColonist && doctor.Map != null)
        {
            foreach (var spawnedColonyAnimal in doctor.Map.mapPawns.SpawnedColonyAnimals)
            {
                var caravanBlood = GetBloodInInventory(spawnedColonyAnimal.inventory.innerContainer);
                if (caravanBlood != null && bloodpack == null && !spawnedColonyAnimal.IsForbidden(doctor) &&
                    doctor.CanReach((LocalTargetInfo)spawnedColonyAnimal, PathEndMode.OnCell, Danger.Some))
                    bloodpack = caravanBlood;
            }
        }

        return bloodpack;
            
        bool CanReserve(Thing m) => !m.IsForbidden(doctor) && !m.IsForbidden(doctor.Faction) && doctor.CanReserve((LocalTargetInfo)m, 10, 1);

        Thing? GetBloodInInventory(ThingOwner inventory)
        {
            return inventory.FirstOrDefault(t => t.def == ThingDefOf.HemogenPack && CanReserve(t));
        }
    }
}