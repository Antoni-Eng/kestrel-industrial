# Fault Recovery Runbook — CNC-204

Document type: runbook
Equipment: CNC-204
Plant: PLANT-NORTH

## Alarm: OVERTEMP / VIBE_HIGH
1. Stop cycle; do not reset repeatedly.
2. Check coolant/air flow and fan operation.
3. If vibration exceeds 4.0 mm/s RMS, create urgent work order and apply LOTO.
4. Consult bearing replacement section in the CNC-204 manual.

## Alarm: ESTOP_CHAIN
1. Inspect doors, light curtains, and E-stop mushrooms.
2. Clear the fault on the safety PLC only after physical inspection.
3. If faults persist, escalate to controls engineering.