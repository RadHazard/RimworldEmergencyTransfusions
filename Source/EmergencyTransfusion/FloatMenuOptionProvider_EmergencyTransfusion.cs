using System;
using System.Collections.Generic;
using System.Linq;
using EmergencyTransfusion.Properties;
using JetBrains.Annotations;
using RimWorld;
using Verse;
using Verse.AI;

namespace EmergencyTransfusion
{
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

        public override IEnumerable<FloatMenuOption> GetOptionsFor(
            Pawn clickedPawn,
            FloatMenuContext context)
        {
            if (IsValidTransfusionTarget(context.FirstSelectedPawn, clickedPawn))
            {
                var bloodLoss = clickedPawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);

                if (bloodLoss == null || bloodLoss.Severity < 0.1)
                {
                    if (context.FirstSelectedPawn != clickedPawn)
                        yield return new FloatMenuOption(
                            "EmergencyTransfusion_CannotTransfuse".Translate(clickedPawn) + ": " +
                            "EmergencyTransfusion_TransfusionNotRequired".Translate(clickedPawn),
                            null);
                }
                else if (context.FirstSelectedPawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
                    yield return new FloatMenuOption(
                        "EmergencyTransfusion_CannotTransfuse".Translate(clickedPawn) + ": " +
                        "CannotPrioritizeWorkTypeDisabled".Translate(WorkTypeDefOf.Doctor.gerundLabel),
                        null);
                else if (!context.FirstSelectedPawn.CanReach(clickedPawn, PathEndMode.ClosestTouch, Danger.Deadly))
                    yield return new FloatMenuOption(
                        "EmergencyTransfusion_CannotTransfuse".Translate(clickedPawn) + ": " +
                        "NoPath".Translate().CapitalizeFirst(),
                        null);
                else if (clickedPawn.InAggroMentalState && !clickedPawn.health.hediffSet.HasHediff(HediffDefOf.Scaria))
                {
                    yield return new FloatMenuOption(
                        "EmergencyTransfusion_CannotTransfuse".Translate(clickedPawn) + ": " +
                        "PawnIsInAggroMentalState".Translate(clickedPawn).CapitalizeFirst(),
                        null);
                }
                else
                {
                    var bloodpack = FindBloodpack(context.FirstSelectedPawn, clickedPawn);
                    if (bloodpack == null)
                    {
                        yield return new FloatMenuOption(
                            "EmergencyTransfusion_CannotTransfuse".Translate(clickedPawn) + ": " +
                            "EmergencyTransfusion_NoBlood".Translate().CapitalizeFirst(),
                            null);
                    }
                    yield return FloatMenuUtility.DecoratePrioritizedTask(
                        new FloatMenuOption("EmergencyTransfusion_Transfuse".Translate(clickedPawn), Transfuse),
                        context.FirstSelectedPawn, clickedPawn);

                    void Transfuse()
                    {
                        var job = JobMaker.MakeJob(EmergencyTransfusion_DefOf.ET_TransfuseBlood, clickedPawn, bloodpack);
                        job.count = 1;
                        job.draftedTend = true;
                        context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job);
                    }
                }
            }
        }

        /// <summary>
        /// Checks whether we can ever apply a transfusion to the target.  Does not check if they actually need it,
        /// only if we could potentially give one (i.e. they aren't a hostile). 
        /// Derived from a similar method in FloatMenuOptionProvider_DraftedTend
        /// </summary>
        /// <param name="doctor">The pawn performing the transfusion</param>
        /// <param name="patient">The pawn receiving the transfusion</param>
        /// <returns>True if we can potentially perform the transfusion</returns>
        private static bool IsValidTransfusionTarget(Pawn doctor, Pawn patient)
        {
            return patient.Downed || !patient.HostileTo(doctor.Faction) &&
                (patient.IsColonist ||
                 patient.IsQuestLodger() ||
                 patient.IsPrisonerOfColony ||
                 patient.IsSlaveOfColony ||
                 patient.IsAnimal && patient.Faction == Faction.OfPlayer ||
                 patient.IsColonySubhuman && patient.mutant.Def.entitledToMedicalCare);
        }

        /// <summary>
        /// Locates the closest valid bloodpack
        /// </summary>
        /// <param name="doctor"></param>
        /// <param name="patient"></param>
        /// <returns></returns>
        private static Thing FindBloodpack(Pawn doctor, Pawn patient)
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
            
            bool CanReserve(Thing m) => !m.IsForbidden(doctor) && doctor.CanReserve((LocalTargetInfo)m, 10, 1);

            Thing GetBloodInInventory(ThingOwner inventory)
            {
                return inventory.FirstOrDefault(t => t.def == ThingDefOf.HemogenPack && CanReserve(t));
            }
        }

        //TODO
        protected override FloatMenuOption GetSingleOptionFor(
            Thing clickedThing,
            FloatMenuContext context)
        {
            Building_HoldingPlatform holdingPlatform = clickedThing as Building_HoldingPlatform;
            if (holdingPlatform == null)
                return (FloatMenuOption)null;
            Pawn heldPawn = holdingPlatform.HeldPawn;
            if (heldPawn == null)
                return (FloatMenuOption)null;
            if (!HealthAIUtility.ShouldBeTendedNowByPlayer(heldPawn))
                return (FloatMenuOption)null;
            if (context.FirstSelectedPawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
                return new FloatMenuOption(
                    (string)("CannotTransfuse".Translate((NamedArgument)(Thing)heldPawn) + ": " +
                             "CannotPrioritizeWorkTypeDisabled".Translate(
                                 (NamedArgument)WorkTypeDefOf.Doctor.gerundLabel)), (Action)null);
            if (!context.FirstSelectedPawn.CanReach((LocalTargetInfo)(Thing)heldPawn, PathEndMode.ClosestTouch,
                    Danger.Deadly))
                return new FloatMenuOption(
                    (string)("CannotTransfuse".Translate((NamedArgument)(Thing)heldPawn) + ": " +
                             "NoPath".Translate().CapitalizeFirst()), (Action)null);
            Thing medicine = HealthAIUtility.FindBestMedicine(context.FirstSelectedPawn, heldPawn);
            return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(
                (string)"Tend".Translate((NamedArgument)heldPawn.LabelShort), (Action)(() =>
                {
                    JobDef tendEntity = JobDefOf.TendEntity;
                    LocalTargetInfo targetA = (LocalTargetInfo)(Thing)holdingPlatform;
                    Thing thing = medicine;
                    LocalTargetInfo targetB = thing != null ? (LocalTargetInfo)thing : LocalTargetInfo.Invalid;
                    Job job = JobMaker.MakeJob(tendEntity, targetA, targetB);
                    job.count = 1;
                    job.draftedTend = true;
                    context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job);
                })), context.FirstSelectedPawn, (LocalTargetInfo)(Thing)holdingPlatform);
        }
    }
}