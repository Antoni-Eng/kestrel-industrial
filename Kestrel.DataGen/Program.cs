using System.Text.Json;
using Kestrel.Core.Models;
using Kestrel.DataGen;
using Microsoft.Data.Sqlite;

var root = args.ElementAtOrDefault(0) ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data"));
var seed = args.Length > 1 && int.TryParse(args[1], out var s) ? s : 42;
var rng = new Random(seed);

Directory.CreateDirectory(root);
var manualsDir = Path.Combine(root, "manuals");
var safetyDir = Path.Combine(root, "safety");
var incidentsDir = Path.Combine(root, "incidents");
var runbooksDir = Path.Combine(root, "runbooks");
var evalDir = Path.Combine(root, "eval");
foreach (var dir in new[] { manualsDir, safetyDir, incidentsDir, runbooksDir, evalDir })
{
    Directory.CreateDirectory(dir);
}

Console.WriteLine($"Generating Kestrel Industrial dataset at {root} (seed={seed})");

foreach (var plant in CompanySeed.Plants)
{
    Console.WriteLine($"Plant {plant.Id}: {plant.Name}");
}

foreach (var eq in CompanySeed.Equipment)
{
    var path = Path.Combine(manualsDir, $"{eq.Id.ToLowerInvariant()}-manual.md");
    await File.WriteAllTextAsync(path, DocumentTemplates.EquipmentManual(eq));
    Console.WriteLine($"  manual {eq.Id}");
}

var safetyDocs = DocumentTemplates.SafetyProcedures().ToList();
foreach (var (id, title, content) in safetyDocs)
{
    var mdPath = Path.Combine(safetyDir, $"{id.ToLowerInvariant()}.md");
    await File.WriteAllTextAsync(mdPath, content);

    // Convert two procedures to PDF for multi-format ingestion demo.
    if (id is "SOP-LOTO-01" or "SOP-PPE-02")
    {
        var pdfPath = Path.Combine(safetyDir, $"{id.ToLowerInvariant()}.pdf");
        SimplePdfWriter.WriteTextPdf(pdfPath, title, content);
        Console.WriteLine($"  safety PDF {id}");
    }
    else
    {
        Console.WriteLine($"  safety {id}");
    }
}

for (var i = 1; i <= 5; i++)
{
    var eq = CompanySeed.Equipment[rng.Next(CompanySeed.Equipment.Length)];
    var when = DateTime.UtcNow.Date.AddDays(-rng.Next(30, 400));
    var path = Path.Combine(incidentsDir, $"ir-{i:D3}.md");
    await File.WriteAllTextAsync(path, DocumentTemplates.IncidentReport(i, eq, when));
}

foreach (var eq in CompanySeed.Equipment.Where(e => e.Type is "CNC Mill" or "Robot" or "Press"))
{
    var path = Path.Combine(runbooksDir, $"{eq.Id.ToLowerInvariant()}-runbook.md");
    await File.WriteAllTextAsync(path, DocumentTemplates.Runbook(eq));
}

// JSON sample document
var jsonDoc = new
{
    id = "policy-shift-handover",
    title = "Shift Handover Checklist",
    docType = DocTypes.Runbook,
    plantId = "PLANT-NORTH",
    content = """
    # Shift Handover Checklist
    1. Review open work orders for the bay.
    2. Note any equipment in LOTO.
    3. Confirm coolant checks for CNC-204 and CNC-118.
    4. Escalate unresolved E-stops to the plant lead.
    """
};
await File.WriteAllTextAsync(
    Path.Combine(runbooksDir, "shift-handover.json"),
    JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true }));

var dbPath = Path.Combine(root, "kestrel.db");
if (File.Exists(dbPath))
{
    File.Delete(dbPath);
}

await using (var connection = new SqliteConnection($"Data Source={dbPath}"))
{
    await connection.OpenAsync();
    await ExecAsync(connection,
        """
        CREATE TABLE plants (id TEXT PRIMARY KEY, name TEXT NOT NULL, location TEXT NOT NULL);
        CREATE TABLE equipment (
            id TEXT PRIMARY KEY, name TEXT NOT NULL, model TEXT NOT NULL,
            type TEXT NOT NULL, plant_id TEXT NOT NULL, area TEXT NOT NULL);
        CREATE TABLE maintenance_logs (
            id INTEGER PRIMARY KEY, equipment_id TEXT NOT NULL, performed_at TEXT NOT NULL,
            technician TEXT NOT NULL, summary TEXT NOT NULL, work_type TEXT NOT NULL, hours INTEGER NOT NULL);
        CREATE TABLE work_orders (
            id INTEGER PRIMARY KEY, equipment_id TEXT NOT NULL, plant_id TEXT NOT NULL,
            title TEXT NOT NULL, status TEXT NOT NULL, priority TEXT NOT NULL,
            opened_at TEXT NOT NULL, due_at TEXT NULL);
        CREATE TABLE feedback (
            id INTEGER PRIMARY KEY AUTOINCREMENT, conversation_id TEXT NOT NULL, message_id TEXT NOT NULL,
            helpful INTEGER NOT NULL, comment TEXT NULL, trace_id TEXT NULL, created_at TEXT NOT NULL);
        """);

    foreach (var plant in CompanySeed.Plants)
    {
        await ExecAsync(connection,
            "INSERT INTO plants (id, name, location) VALUES ($id, $n, $l)",
            ("$id", plant.Id), ("$n", plant.Name), ("$l", plant.Location));
    }

    foreach (var eq in CompanySeed.Equipment)
    {
        await ExecAsync(connection,
            "INSERT INTO equipment (id, name, model, type, plant_id, area) VALUES ($id,$n,$m,$t,$p,$a)",
            ("$id", eq.Id), ("$n", eq.Name), ("$m", eq.Model), ("$t", eq.Type), ("$p", eq.PlantId), ("$a", eq.Area));
    }

    var technicians = new[] { "A. Khan", "J. O'Neil", "M. Rossi", "S. Patel", "L. Nguyen", "C. Brooks" };
    var workTypes = new[] { "Preventive", "Corrective", "Inspection", "Lubrication", "Calibration" };
    for (var i = 1; i <= 500; i++)
    {
        var eq = CompanySeed.Equipment[rng.Next(CompanySeed.Equipment.Length)];
        var when = DateTime.UtcNow.Date.AddDays(-rng.Next(1, 720)).AddHours(rng.Next(6, 18));
        var workType = workTypes[rng.Next(workTypes.Length)];
        var summary = workType switch
        {
            "Preventive" => $"Level-B service on {eq.Id}; filters and grease purged.",
            "Corrective" => $"Corrected fault on {eq.Id}; verified warm-up cycle.",
            "Inspection" => $"Vibration walkaround on {eq.Id}; readings within limits.",
            "Lubrication" => $"Lubrication route complete for {eq.Id}.",
            _ => $"Calibration checks completed on {eq.Id}."
        };

        // Seed a few bearing jobs on CNC-204 for demo questions.
        if (eq.Id == "CNC-204" && i % 40 == 0)
        {
            workType = "Corrective";
            summary = "Replaced spindle bearing pack P/N KST-CNC-204-BRG; vibration 1.8 mm/s RMS after warm-up.";
        }

        await ExecAsync(connection,
            """
            INSERT INTO maintenance_logs (id, equipment_id, performed_at, technician, summary, work_type, hours)
            VALUES ($id,$eq,$when,$tech,$sum,$wt,$hrs)
            """,
            ("$id", i),
            ("$eq", eq.Id),
            ("$when", when.ToString("O")),
            ("$tech", technicians[rng.Next(technicians.Length)]),
            ("$sum", summary),
            ("$wt", workType),
            ("$hrs", rng.Next(1, 6)));
    }

    var statuses = new[] { "Open", "In Progress", "Closed" };
    var priorities = new[] { "Low", "Medium", "High", "Urgent" };
    for (var i = 1; i <= 80; i++)
    {
        var eq = CompanySeed.Equipment[rng.Next(CompanySeed.Equipment.Length)];
        var opened = DateTime.UtcNow.Date.AddDays(-rng.Next(0, 45));
        var status = statuses[rng.Next(statuses.Length)];
        await ExecAsync(connection,
            """
            INSERT INTO work_orders (id, equipment_id, plant_id, title, status, priority, opened_at, due_at)
            VALUES ($id,$eq,$plant,$title,$status,$pri,$opened,$due)
            """,
            ("$id", i),
            ("$eq", eq.Id),
            ("$plant", eq.PlantId),
            ("$title", $"{priorities[rng.Next(priorities.Length)]} service — {eq.Id}"),
            ("$status", status),
            ("$pri", priorities[rng.Next(priorities.Length)]),
            ("$opened", opened.ToString("O")),
            ("$due", opened.AddDays(rng.Next(2, 14)).ToString("O")));
    }
}

var evalQuestions = BuildEvalSet();
await File.WriteAllTextAsync(
    Path.Combine(evalDir, "golden-questions.json"),
    JsonSerializer.Serialize(evalQuestions, new JsonSerializerOptions { WriteIndented = true }));

var companyJson = new
{
    company = "Kestrel Industrial",
    plants = CompanySeed.Plants,
    equipment = CompanySeed.Equipment
};
await File.WriteAllTextAsync(
    Path.Combine(root, "company.json"),
    JsonSerializer.Serialize(companyJson, new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine($"Done. SQLite: {dbPath}");
Console.WriteLine($"Eval questions: {evalQuestions.Count}");

static List<EvalQuestion> BuildEvalSet()
{
    return
    [
        new("q01", "How do I replace the spindle bearing on CNC-204?", ["cnc-204-manual"], ["torque", "LOTO", "KST-CNC-204-BRG"], "CNC-204"),
        new("q02", "What PPE is required for bearing service?", ["sop-ppe-02"], ["cut-resistant", "face shield"], null),
        new("q03", "What is the lockout sequence before opening guards?", ["sop-loto-01"], ["isolate", "personal lock", "zero energy"], null),
        new("q04", "What coolant concentration should CNC machines use?", ["sop-chem-03", "cnc-204-manual"], ["7%", "9%"], null),
        new("q05", "How do I enter the robot cell for RBT-07?", ["sop-robot-04"], ["Teach mode", "enabling device"], "RBT-07"),
        new("q06", "What are the die change controls for PRS-012?", ["sop-press-05", "prs-012-manual"], ["safety blocks", "LOTO"], "PRS-012"),
        new("q07", "When was CNC-204 spindle bearing last replaced?", ["cnc-204-manual"], ["maintenance", "bearing"], "CNC-204"),
        new("q08", "What grease is used after bearing install on CNC-204?", ["cnc-204-manual"], ["KG-2", "two pumps"], "CNC-204"),
        new("q09", "What vibration limit is acceptable after warm-up?", ["cnc-204-manual"], ["2.5 mm/s"], "CNC-204"),
        new("q10", "How should chemical spills be handled?", ["sop-chem-03"], ["spill kit", "CHEM-WASTE-A"], null),
        new("q11", "What is the preventive maintenance interval for CNV-03?", ["cnv-03-manual"], ["250 hours"], "CNV-03"),
        new("q12", "Who can open 480V electrical panels?", ["sop-elec-08"], ["qualified electricians", "arc-rated"], null),
        new("q13", "What is the solo lift limit?", ["sop-erg-09"], ["25 kg"], null),
        new("q14", "How do I recover from ESTOP_CHAIN on RBT-08?", ["rbt-08-runbook"], ["light curtains", "safety PLC"], "RBT-08"),
        new("q15", "What should be checked during shift handover?", ["policy-shift-handover"], ["work orders", "LOTO"], null),
        new("q16", "Hot work outside the weld bay requires what?", ["sop-hotwork-06"], ["permit", "fire watch"], null),
        new("q17", "Are the coolant pit and compressor pit confined spaces?", ["sop-conf-07"], ["permit-required"], null),
        new("q18", "What model is INS-15?", ["ins-15-manual"], ["CONTURA", "Zeiss"], "INS-15"),
        new("q19", "What should I do if vibration exceeds 4.0 mm/s RMS?", ["cnc-204-runbook", "cnc-118-runbook"], ["urgent work order", "LOTO"], null),
        new("q20", "List open work orders for PLANT-NORTH related to presses.", ["prs-012-manual"], ["work order"], null),
        new("q21", "What torque is used on CNC-118 bearing housing bolts?", ["cnc-118-manual"], ["Nm"], "CNC-118"),
        new("q22", "Describe group lockout rules.", ["sop-loto-01"], ["lock box", "personal lock"], null),
        new("q23", "What hearing protection rule applies in production?", ["sop-ppe-02"], ["85 dBA"], null),
        new("q24", "How do I report a near miss?", ["sop-inc-10"], ["before shift end", "EHS"], null),
        new("q25", "What fanuc robot is RBT-07?", ["rbt-07-manual"], ["M-20iD"], "RBT-07"),
        new("q26", "Where is CMP-01 located?", ["cmp-01-manual"], ["Utilities", "PLANT-NORTH"], "CMP-01"),
        new("q27", "What press safety blocks requirement exists?", ["sop-press-05"], ["tonnage"], null),
        new("q28", "Coolant checks mention which CNC assets in handover?", ["policy-shift-handover"], ["CNC-204", "CNC-118"], null),
        new("q29", "What part number is the CNC-204 bearing?", ["cnc-204-manual"], ["KST-CNC-204-BRG"], "CNC-204"),
        new("q30", "Can I bypass light curtains if production is urgent?", ["cnc-204-manual", "sop-loto-01"], ["Do not bypass"], null),
        new("q31", "What is the Level-B service content?", ["cnc-204-manual"], ["filter", "backlash", "grease"], null),
        new("q32", "How long must fire watch remain after hot work?", ["sop-hotwork-06"], ["30 minutes"], null),
        new("q33", "What welder is WLD-22?", ["wld-22-manual"], ["Power Wave", "S500"], "WLD-22"),
        new("q34", "What should incident IR narratives include?", ["sop-inc-10"], ["equipment IDs"], null),
        new("q35", "Is software interlock enough for isolation?", ["sop-loto-01"], ["Never rely"], null),
        new("q36", "What area is PRS-045 in?", ["prs-045-manual"], ["Press Line"], "PRS-045"),
        new("q37", "How many pumps of grease after bearing install?", ["cnc-204-manual"], ["two pumps"], "CNC-204"),
        new("q38", "What do I do before contacting moving assemblies?", ["sop-loto-01", "cnc-204-manual"], ["zero energy"], null),
        new("q39", "Which plant hosts the metrology CMM?", ["ins-15-manual"], ["PLANT-SOUTH"], "INS-15"),
        new("q40", "What extinguisher type is required for hot work?", ["sop-hotwork-06"], ["ABC"], null)
    ];
}

static async Task ExecAsync(SqliteConnection connection, string sql, params (string Name, object Value)[] parameters)
{
    await using var cmd = connection.CreateCommand();
    cmd.CommandText = sql;
    foreach (var (name, value) in parameters)
    {
        cmd.Parameters.AddWithValue(name, value);
    }

    await cmd.ExecuteNonQueryAsync();
}
