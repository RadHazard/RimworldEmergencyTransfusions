using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace EmergencyTransfusion;

[UsedImplicitly]
public class JobDriver_EmergencyTransfusion : JobDriver
{
    public const int TransfusionDuration = 800;

    private const TargetIndex PatientIndex = TargetIndex.A;
    private const TargetIndex BloodIndex = TargetIndex.B;
    private const TargetIndex BloodHolderIndex = TargetIndex.C;
    
    private PathEndMode pathEndMode;
    
    protected Thing Bloodpack => job.targetB.Thing;

    protected Pawn Patient => job.targetA.Pawn;

    protected bool IsBloodInDoctorInventory => pawn.inventory.Contains(Bloodpack);

    protected Pawn_InventoryTracker? BloodHolderInventory =>
        Bloodpack.ParentHolder as Pawn_InventoryTracker;

    protected Pawn OtherPawnBloodHolder => job.targetC.Pawn;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref pathEndMode, "pathEndMode");
    }

    public override void Notify_Starting()
    {
        base.Notify_Starting();
        if (Patient == pawn)
            pathEndMode = PathEndMode.OnCell;
        else if (Patient.InBed())
            pathEndMode = PathEndMode.InteractionCell;
        else
            pathEndMode = PathEndMode.ClosestTouch;
    }

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        if (!pawn.Reserve(Patient, job, errorOnFailed: errorOnFailed))
            return false;

        var available = pawn.Map.reservationManager.CanReserveStack(pawn, (LocalTargetInfo)Bloodpack, 10);
        if (available <= 0 || !pawn.Reserve(Bloodpack, job, 10, 1, errorOnFailed: errorOnFailed))
            return false;

        return true;
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(PatientIndex);
        this.FailOnAggroMentalState(PatientIndex);

        // Collect the blood
        var gotoPatient = Toils_Goto.GotoThing(PatientIndex, pathEndMode);
        var gotoBloodHolder = Toils_Goto.GotoThing(BloodHolderIndex, PathEndMode.Touch)
            .FailOn(() => OtherPawnBloodHolder != BloodHolderInventory?.pawn || OtherPawnBloodHolder.IsForbidden(pawn));

        yield return Toils_General.Do(() => Log.Message("[ET] Start looking for blood"));
        
        yield return Toils_Jump.JumpIf(gotoPatient, () => IsBloodInDoctorInventory);
        yield return Toils_Haul.CheckItemCarriedByOtherPawn(Bloodpack, TargetIndex.C, gotoBloodHolder);
        // Blood is on the ground
        yield return Toils_Goto.GotoThing(BloodIndex, PathEndMode.ClosestTouch)
            .FailOnDespawnedNullOrForbidden(BloodIndex);
        yield return Toils_General.Do(() => Log.Message("[ET] At blood"));
        yield return Toils_Haul.StartCarryThing(BloodIndex, failIfStackCountLessThanJobCount: true);
        yield return Toils_General.Do(() => Log.Message("[ET] Carrying blood"));
        yield return Toils_Jump.Jump(gotoPatient);
        // Blood is being carried by pack animal
        yield return gotoBloodHolder;
        yield return Toils_General.Wait(25).WithProgressBarToilDelay(BloodHolderIndex);
        yield return Toils_Haul.TakeFromOtherInventory(Bloodpack, pawn.inventory.innerContainer,
            BloodHolderInventory?.innerContainer, 1, BloodHolderIndex);
        
        // Transfuse into patient
        yield return Toils_General.Do(() => Log.Message("[ET] Going to patient"));
        yield return gotoPatient;
        yield return Toils_General.Do(() => Log.Message("[ET] At patient"));
        yield return Toils_General.WaitWith(PatientIndex, TransfusionDuration,
            true, true, true, PatientIndex)
            .WithEffect(() => EffecterDefOf.Surgery, PatientIndex)
            .PlaySustainerOrSound(SoundDefOf.Recipe_Surgery);
        yield return PerformTransfusion();
    }
    
    private static Toil PerformTransfusion()
    {
        var toil = ToilMaker.MakeToil();
        toil.initAction = () =>
        {
            var doctor = toil.actor;
            var patient = doctor.CurJob.targetA.Pawn;
            var bloodpack = doctor.CurJob.targetB.Thing;
            if (doctor.skills != null)
            {
                //TODO handle XP
                // var num1 = patient.RaceProps.Animal ? 175f : 500f;
                // var num2 = thing == null ? 0.5f : thing.def.MedicineTendXpGainFactor;
                // actor.skills.Learn(SkillDefOf.Medicine, num1 * num2);
            }
            
            var packsUsed = doctor.CurJob.count;

            // Reduce bloodloss
            var bloodloss = patient.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (bloodloss != null)
                bloodloss.Severity -= packsUsed * Recipe_BloodTransfusion.BloodlossHealedPerPack;
            
            // Add hemogen if the pawn is hemogenic
            if (patient.genes?.GetFirstGeneOfType<Gene_Hemogen>() != null)
                GeneUtility.OffsetHemogen(patient, packsUsed * JobGiver_GetHemogen.HemogenPackHemogenGain);
            
            // Use up packs
            if (bloodpack.stackCount > packsUsed)
                bloodpack.stackCount -= packsUsed;
            else
                bloodpack.Destroy();

            doctor.jobs.EndCurrentJob(JobCondition.Succeeded);
        };
        toil.defaultCompleteMode = ToilCompleteMode.Instant;
        return toil;
    }
}