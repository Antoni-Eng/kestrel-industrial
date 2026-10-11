using System.Text;
using Kestrel.Core.Models;

namespace Kestrel.DataGen;

public static class DocumentTemplates
{
    public static string EquipmentManual(Equipment eq)
    {
        var torque = eq.Type.Contains("Robot") ? "45 Nm" : eq.Type.Contains("Press") ? "120 Nm" : "28 Nm";
        var interval = eq.Type.Contains("Conveyor") ? "250 hours" : "500 hours";

        return
            $"""
            # {eq.Name} Operations Manual ({eq.Id})

            Document ID: MAN-{eq.Id}
            Equipment: {eq.Id} ({eq.Model})
            Plant: {eq.PlantId}
            Area: {eq.Area}
            Company: Kestrel Industrial

            ## Overview
            The {eq.Name} ({eq.Id}) is a {eq.Type.ToLowerInvariant()} installed in {eq.Area} at {eq.PlantId}.
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
            Perform Level-B service every {interval} of runtime or every 90 days, whichever comes first.
            Level-B includes filter changes, backlash check, axis home verification, and grease purge of the primary bearing pack.

            ## Spindle / Drive Bearing Replacement ({eq.Id})
            Tools: bearing puller kit KT-BRG-2, torque wrench, {eq.Model} alignment pins, lint-free cloths.

            1. Create or open a work order and record spindle hours from the HMI.
            2. Isolate electrical and pneumatic supplies. Apply LOTO per SOP-LOTO-01.
            3. Remove the outer guard set and label fastener bags by zone.
            4. Support the spindle/drive cartridge with the shop hoist rated ≥ 250 kg.
            5. Unbolt the cartridge using a criss-cross pattern. Retain shims in order.
            6. Press out the worn bearing. Clean the housing to bare metal; no scoring allowed.
            7. Press in the new bearing (P/N KST-{eq.Id}-BRG). Seat fully against the shoulder.
            8. Reinstall the cartridge. Torque housing bolts to **{torque}** in three stages.
            9. Regrease with Kestrel Grease KG-2 (two pumps only).
            10. Remove LOTO, run the OEM warm-up program, and verify vibration < 2.5 mm/s RMS.
            11. Update the maintenance log with part lot number and measured vibration.

            ## Troubleshooting
            | Symptom | Likely Cause | Action |
            | --- | --- | --- |
            | High vibration after warm-up | Bearing preload or imbalance | Recheck torque {torque}; balance tool holder |
            | Axis following error | Contaminated encoder / low lube | Clean encoder; verify lube pressure |
            | Over-temperature alarm | Blocked coolant or fan | Clean heat exchanger; check coolant flow |

            ## Related Documents
            - SOP-LOTO-01 Lockout/Tagout
            - SOP-PPE-02 Shop PPE Matrix
            - Runbook for {eq.Id} fault recovery
            """;
    }

    public static IEnumerable<(string Id, string Title, string Content)> SafetyProcedures()
    {
        yield return ("SOP-LOTO-01", "Lockout/Tagout Procedure",
            """
            # Lockout/Tagout Procedure (SOP-LOTO-01)

            Plant applicability: PLANT-NORTH, PLANT-SOUTH
            Document type: safety

            ## Purpose
            Prevent unexpected energization of equipment during service. This procedure is mandatory for any work that places a technician inside a guarded envelope.

            ## Authorized Employees
            Only technicians with current Kestrel LOTO certification may apply locks. Escorts must remain outside the envelope.

            ## Energy Isolation Sequence
            1. Notify affected operators and place the machine in STOP.
            2. Shut down using the normal stopping procedure on the HMI.
            3. Isolate the main electrical disconnect. Attempt a start to verify isolation.
            4. Isolate pneumatic supply and bleed residual pressure to 0 bar.
            5. Isolate hydraulic power (press assets). Verify gauge reads 0 bar.
            6. Apply personal lock and completed tag on each energy source.
            7. Verify zero energy with a meter before contacting components.
            8. Perform the maintenance task.
            9. Remove tools, restore guards, remove locks in reverse order, and notify operations.

            ## Group Lockout
            For multi-person jobs use the group lock box. Each worker applies a personal lock to the box. The box may not be opened until all personal locks are removed.

            ## Prohibited Actions
            - Never loan your lock or use another person's lock.
            - Never remove a lock that is not yours.
            - Never rely on a software interlock as the sole isolation method.
            """);

        yield return ("SOP-PPE-02", "Shop PPE Matrix",
            """
            # Shop PPE Matrix (SOP-PPE-02)

            Document type: safety

            ## Minimum PPE (all production areas)
            - Safety glasses with side shields
            - Steel-toe boots
            - Hearing protection in marked zones (>85 dBA)

            ## Task-Specific PPE
            | Task | Extra PPE |
            | --- | --- |
            | Bearing / spindle service | Cut-resistant gloves, face shield when pressing |
            | Welding | Auto-darkening helmet, FR clothing, welding gloves |
            | Chemical handling (coolant concentrate) | Chemical gloves, apron, splash goggles |
            | Robot cell entry | High-visibility vest, teach-pendant tether |

            ## Notes
            Damaged PPE must be replaced immediately from the crib. Prescription safety glasses are available via EHS.
            """);

        yield return ("SOP-CHEM-03", "Coolant and Chemical Handling",
            """
            # Coolant and Chemical Handling (SOP-CHEM-03)

            Document type: safety

            ## Scope
            Covers metalworking fluid concentrate, degreasers, and grease used on CNC and washer assets.

            ## Storage
            Keep concentrates in the flammables cabinet. Do not store above eye level. Secondary containment required for drums > 20 L.

            ## Mixing Coolant
            Target concentration 7–9%. Use refractometer daily. Never add water to concentrate without measuring; always add concentrate to water.

            ## Spill Response
            1. Stop the source if safe.
            2. Don chemical gloves and goggles.
            3. Absorb with spill kit SK-2.
            4. Place waste in labeled drum CHEM-WASTE-A.
            5. Report via EHS incident form within 2 hours.
            """);

        yield return ("SOP-ROBOT-04", "Robot Cell Entry",
            """
            # Robot Cell Entry (SOP-ROBOT-04)

            Document type: safety
            Equipment focus: RBT-07, RBT-08

            ## Entry Conditions
            Enter only in Teach mode with enabling device held. Place the cell in SAFE STOP before crossing the light curtain for maintenance that does not require powered motion.

            ## Rules
            - One teach pendant operator inside the cell at a time unless a written two-person plan exists.
            - Keep clear of pinch points at EOAT and fixture clamps.
            - After exit, verify light curtains and door interlocks before returning to Auto.
            """);

        yield return ("SOP-PRESS-05", "Press Safety Die Change",
            """
            # Press Safety Die Change (SOP-PRESS-05)

            Document type: safety
            Equipment focus: PRS-012, PRS-045

            ## Die Change Controls
            1. Apply LOTO per SOP-LOTO-01 including hydraulic dump.
            2. Insert safety blocks rated for the press tonnage.
            3. Use die cart DC-3; never manually lift dies > 25 kg.
            4. Verify counterbalance pressure before removing blocks.
            5. Inch the press through one stroke with hands clear before production.
            """);

        yield return ("SOP-HOTWORK-06", "Hot Work Permit",
            """
            # Hot Work Permit (SOP-HOTWORK-06)

            Document type: safety

            Welding, grinding, or cutting outside the designated Weld Bay requires a hot work permit from the shift supervisor. A fire watch must remain 30 minutes after completion. Keep extinguisher ABC within 3 m.
            """);

        yield return ("SOP-CONF-07", "Confined Space Awareness",
            """
            # Confined Space Awareness (SOP-CONF-07)

            Document type: safety

            The coolant pit under CLN-09 and the compressor receiver pit are permit-required confined spaces. Entry requires atmospheric testing, attendant, and retrieval harness. Never enter to retrieve dropped tools without a permit.
            """);

        yield return ("SOP-ELEC-08", "Electrical Panel Access",
            """
            # Electrical Panel Access (SOP-ELEC-08)

            Document type: safety

            Only qualified electricians open 480V panels. Arc-rated clothing category 2 minimum. Use insulated tools. Torque breaker lugs to manufacturer values and torque-stripe after service.
            """);

        yield return ("SOP-ERG-09", "Manual Handling Limits",
            """
            # Manual Handling Limits (SOP-ERG-09)

            Document type: safety

            Solo lift limit is 25 kg. Use hoist or team lift above that. Spindle cartridges and die shoes always use the rated hoist.
            """);

        yield return ("SOP-INC-10", "Incident Reporting",
            """
            # Incident Reporting (SOP-INC-10)

            Document type: safety

            Report injuries, near misses, and property damage before shift end. Preserve the scene for serious incidents. Notify the plant lead and EHS. Reference equipment IDs (for example CNC-204) in the report narrative.
            """);
    }

    public static string IncidentReport(int number, Equipment eq, DateTime when)
    {
        return
            $"""
            # Incident Report IR-{number:D3}

            Document type: incident
            Equipment: {eq.Id}
            Plant: {eq.PlantId}
            Date: {when:yyyy-MM-dd}

            ## Summary
            During shift handover, an operator reported abnormal noise on {eq.Id}. Production paused. Maintenance found early bearing wear. No injury occurred.

            ## Immediate Actions
            - Machine locked out per SOP-LOTO-01
            - Temporary production moved to alternate asset
            - Bearing replacement scheduled under WO linked to {eq.Id}

            ## Corrective Actions
            - Replace bearing pack per manual for {eq.Id}
            - Increase vibration walkaround frequency for 30 days
            - Review lubrication route compliance
            """;
    }

    public static string Runbook(Equipment eq)
    {
        return
            $"""
            # Fault Recovery Runbook — {eq.Id}

            Document type: runbook
            Equipment: {eq.Id}
            Plant: {eq.PlantId}

            ## Alarm: OVERTEMP / VIBE_HIGH
            1. Stop cycle; do not reset repeatedly.
            2. Check coolant/air flow and fan operation.
            3. If vibration exceeds 4.0 mm/s RMS, create urgent work order and apply LOTO.
            4. Consult bearing replacement section in the {eq.Id} manual.

            ## Alarm: ESTOP_CHAIN
            1. Inspect doors, light curtains, and E-stop mushrooms.
            2. Clear the fault on the safety PLC only after physical inspection.
            3. If faults persist, escalate to controls engineering.
            """;
    }

    public static string ToPdfFriendlyMarkdown(string markdown) => markdown;
}
