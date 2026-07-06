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
    private const TargetIndex PatientIndex = TargetIndex.A;
    private const TargetIndex BloodIndex = TargetIndex.B;
    private const TargetIndex BloodHolderIndex = TargetIndex.C;
    
    private PathEndMode pathEndMode;
    
    protected Pawn Patient => job.targetA.Pawn;

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

        for (var i = 0; i < job.targetQueueB.Count; i++)
        {
            if (!pawn.Reserve(job.targetQueueB[i], job, 10, job.countQueue[i]))
                return false;
        }

        return true;
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(PatientIndex);
        this.FailOnAggroMentalState(PatientIndex);

        // Collect the blood
        foreach (var toil in CollectBloodToils()) yield return toil;

        // Transfuse into patient
        yield return Toils_Goto.GotoThing(PatientIndex, pathEndMode);
        //TODO - this checks the operation speed when the pawn starts the job, not when they actually reach the patient
        //we need to make a custom wait toil to fix this
        var duration = (int)(ET_DefOf.BloodTransfusion.workAmount / pawn.GetStatValue(ET_DefOf.MedicalOperationSpeed));
        yield return Toils_General.WaitWith(PatientIndex, duration,
            true, true, true, PatientIndex)
            .WithEffect(() => EffecterDefOf.Surgery, PatientIndex)
            .PlaySustainerOrSound(SoundDefOf.Recipe_Surgery);
        yield return PerformTransfusion(duration);
    }

    /// <summary>
    /// Makes a loop of toils for collecting all the blood packs
    /// </summary>
    /// <returns>The collection toils</returns>
    protected static IEnumerable<Toil> CollectBloodToils()
    {
        var getNextPack = Toils_JobTransforms.ExtractNextTargetFromQueue(BloodIndex);
        yield return getNextPack;
        
        var gotoBloodHolder = Toils_Goto.GotoThing(BloodIndex, PathEndMode.Touch, true);
        
        var pickUpBlood = Toils_Haul.StartCarryThing(BloodIndex, failIfStackCountLessThanJobCount: true,
            reserve: false,
            canTakeFromInventory: true);
        
        yield return Toils_ET.JumpIfHoldingTarget(BloodIndex, pickUpBlood);
        yield return Toils_ET.JumpIfSomeoneElseHoldingTarget(BloodIndex, gotoBloodHolder, BloodHolderIndex);
        
        // Blood is on the ground
        yield return Toils_Goto.GotoThing(BloodIndex, PathEndMode.ClosestTouch)
            .FailOnDespawnedNullOrForbidden(BloodIndex)
            .FailOnBurningImmobile(BloodIndex);
        yield return Toils_Jump.Jump(pickUpBlood);
        
        // Blood is being carried by someone else
        yield return gotoBloodHolder;
        yield return Toils_General.Wait(25).WithProgressBarToilDelay(BloodHolderIndex);
        
        // We are at the blood (or it's in our inventory)
        yield return pickUpBlood;
        yield return Toils_Jump.JumpIfHaveTargetInQueue(BloodIndex, getNextPack);
    }
    
    private static Toil PerformTransfusion(float duration)
    {
        var toil = ToilMaker.MakeToil();
        toil.initAction = () =>
        {
            var doctor = toil.actor;
            var patient = doctor.CurJob.targetA.Pawn;
            var bloodpack = doctor.CurJob.targetB.Thing;
            if (doctor.skills != null)
            {
                var xp = duration * 0.1f * ET_DefOf.BloodTransfusion.workSkillLearnFactor;
                doctor.skills.Learn(SkillDefOf.Medicine, xp);
            }

            // Reduce blood loss
            var bloodLoss = patient.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (bloodLoss != null)
                bloodLoss.Severity -= bloodpack.stackCount * Recipe_BloodTransfusion.BloodlossHealedPerPack;
            
            // Add hemogen if the pawn is hemogenic
            if (patient.genes?.GetFirstGeneOfType<Gene_Hemogen>() != null)
                GeneUtility.OffsetHemogen(patient, bloodpack.stackCount * JobGiver_GetHemogen.HemogenPackHemogenGain);
            
            bloodpack.Destroy();
            doctor.jobs.EndCurrentJob(JobCondition.Succeeded);
        };
        toil.defaultCompleteMode = ToilCompleteMode.Instant;
        return toil;
    }
}