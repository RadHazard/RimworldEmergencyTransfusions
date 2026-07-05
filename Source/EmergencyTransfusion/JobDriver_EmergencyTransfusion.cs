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

    private const TargetIndex MedicineIndex = TargetIndex.B;
    private const TargetIndex MedicineHolderIndex = TargetIndex.C;
        
    private PathEndMode pathEndMode;
        
    private static List<Toil> tmpCollectToils = new List<Toil>();

    protected Thing Bloodpack => job.targetB.Thing;

    protected Pawn Patient => job.targetA.Pawn;

    protected bool IsBloodInDoctorInventory => Bloodpack != null && pawn.inventory.Contains(Bloodpack);

    protected Pawn_InventoryTracker BloodHolderInventory =>
        Bloodpack?.ParentHolder as Pawn_InventoryTracker;

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

        if (Bloodpack == null)
            return false;
            
        var available = pawn.Map.reservationManager.CanReserveStack(pawn, (LocalTargetInfo)Bloodpack, 10);
        if (available <= 0 || !pawn.Reserve(Bloodpack, job, 10, 1, errorOnFailed: errorOnFailed))
            return false;

        return true;
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        this.FailOnDespawnedNullOrForbidden(TargetIndex.B);
        this.FailOnAggroMentalState(TargetIndex.A);

        yield return Toils_Goto.GotoThing(TargetIndex.A, pathEndMode);
            
            
        // var f = this;
        // // ISSUE: reference to a compiler-generated method
        // f.FailOn<JobDriver_TendPatient>(new Func<bool>(f.\u003CMakeNewToils\u003Eb__19_0));
        // // ISSUE: reference to a compiler-generated method
        // // ISSUE: explicit non-virtual call
        // __nonvirtual(f.AddEndCondition(new Func<JobCondition>(f.\u003CMakeNewToils\u003Eb__19_1)));
        // f.FailOnAggroMentalState<JobDriver_TendPatient>(TargetIndex.A);
        // var reserveMedicine = (Toil)null;
        // var gotoToil = Toils_Goto.GotoThing(TargetIndex.A, f.pathEndMode);
        // if (f.usesMedicine)
        //   foreach (Toil collectMedicineToil in JobDriver_TendPatient.CollectMedicineToils(f.pawn, f.Patient,
        //              f.job, gotoToil, out reserveMedicine))
        //     yield return collectMedicineToil;
        // yield return gotoToil;
        // var ticks = (int)(1.0 / (double)f.pawn.GetStatValue(StatDefOf.MedicalTendSpeed) * 600.0);
        // Toil waitToil;
        // if (!f.job.draftedTend || f.pawn == f.TargetPawnA)
        // {
        //   waitToil = Toils_General.Wait(ticks);
        // }
        // else
        // {
        //   waitToil = Toils_General.WaitWith(TargetIndex.A, ticks, maintainPosture: true, face: TargetIndex.A,
        //     pathEndMode: f.pathEndMode);
        //   // ISSUE: reference to a compiler-generated method
        //   waitToil.AddFinishAction(new Action(f.\u003CMakeNewToils\u003Eb__19_2));
        // }
        //
        // waitToil.WithProgressBarToilDelay(TargetIndex.A).PlaySustainerOrSound(SoundDefOf.Interact_Tend);
        // waitToil.activeSkill = (Func<SkillDef>)(() => SkillDefOf.Medicine);
        // waitToil.handlingFacing = true;
        // // ISSUE: reference to a compiler-generated method
        // waitToil.tickIntervalAction = new Action<int>(f.\u003CMakeNewToils\u003Eb__19_4);
        // // ISSUE: reference to a compiler-generated method
        // waitToil.FailOn<Toil>(new Func<bool>(f.\u003CMakeNewToils\u003Eb__19_5));
        // // ISSUE: reference to a compiler-generated method
        // yield return Toils_Jump.JumpIf(waitToil, new Func<bool>(f.\u003CMakeNewToils\u003Eb__19_6));
        // yield return Toils_Tend.PickupMedicine(TargetIndex.B, f.Patient)
        //   .FailOnDestroyedOrNull<Toil>(TargetIndex.B);
        // yield return waitToil;
        // yield return Toils_Tend.FinalizeTend(f.Patient);
        // if (f.usesMedicine)
        //   yield return JobDriver_TendPatient.FindMoreMedicineToil(f.pawn, f.Patient, TargetIndex.B, f.job,
        //     reserveMedicine);
        // yield return Toils_Jump.Jump(gotoToil);
    }

    // public override void Notify_DamageTaken(DamageInfo dinfo)
    // {
    //     base.Notify_DamageTaken(dinfo);
    //     if (!dinfo.Def.ExternalViolenceFor(pawn) || pawn.Faction == Faction.OfPlayer || pawn != Patient)
    //         return;
    //     pawn.jobs.CheckForJobOverride();
    // }
        
    // public static List<Toil> CollectBloodToils(
    //     Pawn doctor,
    //     Pawn patient,
    //     Job job,
    //     Toil gotoToil,
    //     out Toil reserveMedicine)
    // {
    //     
    //     tmpCollectToils.Clear();
    //     reserveMedicine = Toils_Tend.ReserveMedicine(TargetIndex.B, patient)
    //         .FailOnDespawnedNullOrForbidden<Toil>(TargetIndex.B);
    //     tmpCollectToils.Add(Toils_Jump.JumpIf(gotoToil,
    //       (Func<bool>)(() => medicineUsed != null && doctor.inventory.Contains(medicineUsed))));
    //     var jumpIfCarriedByOther = Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.Touch)
    //       .FailOn<Toil>((Func<bool>)(() =>
    //         otherPawnMedicineHolder != medicineHolderInventory?.pawn ||
    //         otherPawnMedicineHolder.IsForbidden(doctor)));
    //     tmpCollectToils.Add(
    //       Toils_Haul.CheckItemCarriedByOtherPawn(medicineUsed, TargetIndex.C, jumpIfCarriedByOther));
    //     tmpCollectToils.Add(reserveMedicine);
    //     tmpCollectToils.Add(Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch)
    //       .FailOnDespawnedNullOrForbidden<Toil>(TargetIndex.B));
    //     tmpCollectToils.Add(Toils_Tend.PickupMedicine(TargetIndex.B, patient)
    //       .FailOnDestroyedOrNull<Toil>(TargetIndex.B));
    //     tmpCollectToils.Add(
    //       Toils_Haul.CheckForGetOpportunityDuplicate(reserveMedicine, TargetIndex.B, TargetIndex.None, true));
    //     tmpCollectToils.Add(Toils_Jump.Jump(gotoToil));
    //     tmpCollectToils.Add(jumpIfCarriedByOther);
    //     tmpCollectToils.Add(Toils_General.Wait(25).WithProgressBarToilDelay(TargetIndex.C));
    //     tmpCollectToils.Add(Toils_Haul.TakeFromOtherInventory(medicineUsed,
    //       (ThingOwner)doctor.inventory.innerContainer, (ThingOwner)medicineHolderInventory?.innerContainer,
    //       Medicine.GetMedicineCountToFullyHeal(patient), TargetIndex.B));
    //     return tmpCollectToils; 
    // }
}