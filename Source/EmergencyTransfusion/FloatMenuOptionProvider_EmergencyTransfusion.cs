using System;
using System.Collections.Generic;
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

    public override IEnumerable<FloatMenuOption> GetOptionsFor(
        Pawn clickedPawn,
        FloatMenuContext context)
    {
        var bloodLoss = clickedPawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
        if (bloodLoss == null) yield break;
        
        if (bloodLoss.Severity < 0.15)
            yield return new FloatMenuOption(
                "ET_CannotTransfuse".Translate(clickedPawn) + ": " +
                "ET_TransfusionNotRequired".Translate(clickedPawn),
                null);
        else if (context.FirstSelectedPawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
            yield return new FloatMenuOption(
                "ET_CannotTransfuse".Translate(clickedPawn) + ": " +
                "CannotPrioritizeWorkTypeDisabled".Translate(WorkTypeDefOf.Doctor.gerundLabel),
                null);
        else if (!context.FirstSelectedPawn.CanReach(clickedPawn, PathEndMode.ClosestTouch, Danger.Deadly))
            yield return new FloatMenuOption(
                "ET_CannotTransfuse".Translate(clickedPawn) + ": " +
                "NoPath".Translate().CapitalizeFirst(),
                null);
        else if (clickedPawn.InAggroMentalState && !clickedPawn.health.hediffSet.HasHediff(HediffDefOf.Scaria))
            yield return new FloatMenuOption(
                "ET_CannotTransfuse".Translate(clickedPawn) + ": " +
                "PawnIsInAggroMentalState".Translate(clickedPawn).CapitalizeFirst(),
                null);
        else
        {
            var maxPacks = (int) Math.Ceiling(bloodLoss.Severity / Recipe_BloodTransfusion.BloodlossHealedPerPack);

            var bloodPacks = FindBloodPacks(context.FirstSelectedPawn, clickedPawn, maxPacks);
            if (bloodPacks.Empty())
            {
                yield return new FloatMenuOption(
                    "ET_CannotTransfuse".Translate(clickedPawn) + ": " +
                    "ET_NoBlood".Translate().CapitalizeFirst(),
                    null);
            }
            else
            {
                yield return FloatMenuUtility.DecoratePrioritizedTask(
                    new FloatMenuOption("ET_TransfuseOne".Translate(clickedPawn), () =>
                    {
                        var job = JobMaker.MakeJob(ET_DefOf.ET_TransfuseBlood, clickedPawn);
                        job.targetQueueB = new List<LocalTargetInfo>(1);
                        job.countQueue = new List<int>(1);

                        job.targetQueueB.Add(bloodPacks[0].Thing);
                        job.countQueue.Add(1);
                        
                        context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job);
                    }),
                    context.FirstSelectedPawn, clickedPawn);

                var packsAvailable = bloodPacks.Sum(p => p.Count);
                if (packsAvailable > 1)
                    yield return FloatMenuUtility.DecoratePrioritizedTask(
                        new FloatMenuOption("ET_TransfuseMultiple".Translate(clickedPawn, packsAvailable), () =>
                        {
                            var job = JobMaker.MakeJob(ET_DefOf.ET_TransfuseBlood, clickedPawn);
                            job.targetQueueB = new List<LocalTargetInfo>(bloodPacks.Count);
                            job.countQueue = new List<int>(bloodPacks.Count);
                            foreach (var p in bloodPacks)
                            {
                                job.targetQueueB.Add(p.Thing);
                                job.countQueue.Add(p.Count);
                            }
                            context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job);
                        }),
                        context.FirstSelectedPawn, clickedPawn);
                else if (maxPacks > 1)
                    yield return new FloatMenuOption(
                        "ET_NotEnoughBlood".Translate(clickedPawn),
                        null);
                    
            }
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
    /// Finds the closest blood packs to the doctor and returns them in an order that maximizes grabbing efficiency
    ///
    /// The packs are checked in the following order:
    ///  - The patient inventory first (so that patients can carry their own blood)
    ///  - The doctor's inventory second (for speed).
    ///  - On the ground or caravan animal nearby, ordered by straight-line distance from the doctor.
    ///    - This is not necessarily the most efficient, but it should be acceptable in most circumstances.
    ///
    /// The found packs are then returned in the following order (to facilitate efficient grabbing on the way to the
    /// patient):
    ///  - The packs in the doctor's inventory.
    ///  - The packs on the ground, ordered in an efficient traveling-salesman manner.
    ///  - The packs in the patient's inventory.
    /// </summary>
    /// <param name="doctor">The pawn performing the transfusion</param>
    /// <param name="patient">The pawn receiving the transfusion</param>
    /// <param name="numPacks">The number of blood packs we want</param>
    /// <returns>A list of blood pack stacks and the amount we want to pull from each.  There can potentially be less
    /// than numPacks if not enough blood packs are available</returns>
    private static List<ThingCount> FindBloodPacks(Pawn doctor, Pawn patient, int numPacks)
    {

        // Check the patient inventory first
        var patientPacks = GetBloodFromInventory(doctor, patient.inventory.innerContainer,
            numPacks, out var packsFound);

        if (packsFound >= numPacks)
            return patientPacks;

        // If we still need blood, check the doc's inventory
        numPacks -= packsFound;
        var docPacks = GetBloodFromInventory(doctor, doctor.inventory.innerContainer,
            numPacks, out packsFound);

        if (packsFound >= numPacks)
        {
            docPacks.AddRange(patientPacks);
            return docPacks;
        }

        // If we *still* need blood, check the ground and pack animals
        numPacks -= packsFound;

        var animalBlood = doctor.Map.mapPawns.SpawnedColonyAnimals
            .Where(animal => !animal.IsForbidden(doctor) &&
                             doctor.CanReach(animal, PathEndMode.OnCell, Danger.Some))
            .SelectMany(animal => animal.inventory.innerContainer
                             .Where(t => t.def == ThingDefOf.HemogenPack &&
                                  !t.IsForbidden(doctor) &&
                                  !t.IsForbidden(doctor.Faction)));
        var allBlood = doctor.Map.listerThings.ThingsOfDef(ThingDefOf.HemogenPack)
            .Where(t => doctor.CanReach(t, PathEndMode.Touch, Danger.Some) &&
                        !t.IsForbidden(doctor) &&
                        !t.IsForbidden(doctor.Faction))
            .Concat(animalBlood)
            .ToList();
        
        allBlood.SortBy(t=> t.PositionHeld.DistanceToSquared(doctor.Position));

        var groundPacks = new List<ThingCount>();
        foreach (var pack in allBlood)
        {
            var thingCount = CanReserveUpTo(doctor, pack, numPacks);
            if (thingCount.Count > 0)
            {
                groundPacks.Add(thingCount);
                numPacks -= thingCount.Count;
            }

            if (numPacks <= 0) break;
        }
        
        // TODO sort ground packs to minimize traveling salesman distance
        
        docPacks.AddRange(groundPacks);
        docPacks.AddRange(patientPacks);
        return docPacks;
    }

    /// <summary>
    /// Returns the accessible blood packs from a given inventory 
    /// </summary>
    /// <param name="doctor">The doctor doing the reserving</param>
    /// <param name="inventory">The inventory to check</param>
    /// <param name="count">The maximum number of blood packs we want</param>
    /// <param name="found">How many packs we found</param>
    /// <returns>A list of ThingCounts for the various stacks and how many we can take from each</returns>
    private static List<ThingCount> GetBloodFromInventory(Pawn doctor, ThingOwner<Thing> inventory, int count, out int found)
    {
        found = 0;
        var blood = new List <ThingCount>();
        
        foreach (var t in inventory)
        {
            // IsForbidden(pawn) always returns false if the pawn is drafted so the second call ensures that drafted
            // pawns still ignore forbidden items
            if (t.def != ThingDefOf.HemogenPack ||
                t.IsForbidden(doctor) ||
                t.IsForbidden(doctor.Faction))
                continue;

            var thingCount = CanReserveUpTo(doctor, t, count - found);
            if (thingCount.Count > 0)
            {
                blood.Add(thingCount);
                found += thingCount.Count;
            }
            
            if (found >= count) break;
        }

        return blood;
    }
    
    /// <summary>
    /// Figure out how much of a stack we can reserve
    /// </summary>
    /// <param name="pawn">The pawn doing the reserving</param>
    /// <param name="thing">The stack to check</param>
    /// <param name="count">The maximum we want to reserve</param>
    /// <returns>A ThingCount containing the stack and the count of items we can reserve (can be zero if the stack is
    /// already fully reserved)</returns>
    private static ThingCount CanReserveUpTo(Pawn pawn, Thing thing, int count)
    {
        // This is a wildly inefficient way to figure out how many items in a stack we can reserve, but for some
        // reason ReservationManager does not have a method that just tells us how many unreserved items exist in
        // a stack.  In practice, we should never need more than 3 blood packs so it shouldn't matter much. 
        var n = Math.Min(thing.stackCount, count);
        while (n > 0)
        {
            if (pawn.CanReserve(thing, 10, n))
                return new ThingCount(thing, n);
            n--;
        }

        return new ThingCount(thing, 0);
    }
}