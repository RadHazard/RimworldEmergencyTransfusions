# Rimworld Emergency Blood Transfusions
Perform blood transfusions in the field!

Similar to the drafted "tend now" command, you can now command your doctors to perform an emergency blood transfusion without needing to drag them to your hospital first.  Emergency transfusions work just like normal transfusions, taking the same amount of time (13.333 seconds, modified by medical operation speed) and restoring the same amount of blood (35% per hemogen pack).  However, doctors can do them in the field and can use hemogen packs from their inventory or the inventory of their patient, which can save precious seconds when your best pawns are bleeding out.

To perform an emergency transfusion, you need to have at least one hemogen pack available and the patient must have lost at least 15% of their blood.  The pawn performing the transfusion must also be capable of doctoring.  You can choose to transfuse either a single hemogen pack or as many packs as the patient needs (or as many as the doctor can find, whichever is lower).

Transfusions can be given to humanlike pawns you control, or to downed humanlike pawns from any faction. Animals are not permitted, as they cannot receive normal transfusions either. More or less, if you could schedule a transfusion bill on them, you should be able to perform an emergency transfusion on them.

### How It Works

When using searching for blood packs, the doctor will prefer to use them in the following order:
 - Packs in the patient's inventory
 - Packs in the doctor's inventory
 - Any packs on the ground or held by a caravan animal, from closest to furthest

This allows you to give your most vulnerable pawns their own supply of blood packs, while preserving the doctor's supply for pawns that aren't carrying any.

To take advantage of this feature, it is recommended to use a mod that allows pawns to automatically carry things in their inventory, such as:
- Compositable Loadouts
- Combat Extended

### Compatability Notes
The emergency transfusion job inherits as much as it can from the original transfusion job.  If another mod changes the original def (e.g. making it faster or slower or changing how much XP it gives), this mod should automatically pick up those changes.

The only exception is the amount of blood loss healed per pack, and the object that serves as a blood pack.  Both are currently hard-coded.

When scanning for blood packs, this mod looks in the inventories of the doctor and the patient.  Any mod that uses the vanilla inventory system to hold items *should* work just fine.  Tested with Pick Up and Haul, Hauler's Dream, Combat Extended, and Compositable Loadouts.  Other mods that place items in the inventory are untested but should work as well.  
