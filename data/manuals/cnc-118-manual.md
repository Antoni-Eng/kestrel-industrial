# DMG Mori NLX Lathe Operations Manual (CNC-118)

Document ID: MAN-CNC-118
Equipment: CNC-118 (NLX2500)
Plant: PLANT-NORTH
Area: Bay A
Company: Kestrel Industrial

## Overview
The DMG Mori NLX Lathe (CNC-118) is a cnc lathe installed in Bay A at PLANT-NORTH.
This manual covers daily checks, scheduled service, and the spindle/drive bearing replacement procedure used by Kestrel maintenance technicians.

## Safety Precautions
1. Apply lockout/tagout (LOTO) per SOP-LOTO-01 before opening guards.
2. Wear safety glasses, cut-resistant gloves, and steel-toe boots.
3. Verify zero energy state with a calibrated meter before contacting moving assemblies.
4. Do not bypass light curtains or interlocks. Report faults via work order.

## Daily Checks
- Inspect guarding and E-stop function at start of shift.
- Check lubrication sight glass; top up with ISO VG 68 if below the mid mark.
- Listen for abnormal spindle or gearbox noise during warm-up cycle.
- Confirm coolant concentration is between 7% and 9% (CNC assets only).

## Preventive Maintenance Interval
Perform Level-B service every 500 hours of runtime or every 90 days, whichever comes first.
Level-B includes filter changes, backlash check, axis home verification, and grease purge of the primary bearing pack.

## Spindle / Drive Bearing Replacement (CNC-118)
Tools: bearing puller kit KT-BRG-2, torque wrench, NLX2500 alignment pins, lint-free cloths.

1. Create or open a work order and record spindle hours from the HMI.
2. Isolate electrical and pneumatic supplies. Apply LOTO per SOP-LOTO-01.
3. Remove the outer guard set and label fastener bags by zone.
4. Support the spindle/drive cartridge with the shop hoist rated ≥ 250 kg.
5. Unbolt the cartridge using a criss-cross pattern. Retain shims in order.
6. Press out the worn bearing. Clean the housing to bare metal; no scoring allowed.
7. Press in the new bearing (P/N KST-CNC-118-BRG). Seat fully against the shoulder.
8. Reinstall the cartridge. Torque housing bolts to **28 Nm** in three stages.
9. Regrease with Kestrel Grease KG-2 (two pumps only).
10. Remove LOTO, run the OEM warm-up program, and verify vibration < 2.5 mm/s RMS.
11. Update the maintenance log with part lot number and measured vibration.

## Troubleshooting
| Symptom | Likely Cause | Action |
| --- | --- | --- |
| High vibration after warm-up | Bearing preload or imbalance | Recheck torque 28 Nm; balance tool holder |
| Axis following error | Contaminated encoder / low lube | Clean encoder; verify lube pressure |
| Over-temperature alarm | Blocked coolant or fan | Clean heat exchanger; check coolant flow |

## Related Documents
- SOP-LOTO-01 Lockout/Tagout
- SOP-PPE-02 Shop PPE Matrix
- Runbook for CNC-118 fault recovery