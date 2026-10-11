using Kestrel.Core.Ingestion;
using Kestrel.Core.Models;
using Kestrel.Core.Rag;

namespace Kestrel.Tests;

public sealed class DocumentMetadataTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kestrel-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Manual_IsManual_WithOwnEquipment_DespiteSopReferences()
    {
        var doc = await LoadMarkdownAsync("manuals", "cnc-204-manual.md", """
            # Haas VF-2 Vertical Mill Operations Manual (CNC-204)

            Document ID: MAN-CNC-204
            Equipment: CNC-204 (VF-2)
            Plant: PLANT-NORTH

            ## Safety Precautions
            1. Apply lockout/tagout (LOTO) per SOP-LOTO-01 before opening guards.
            2. Follow the safety procedure for PPE.
            """);

        Assert.Equal(DocTypes.Manual, doc.DocType);
        Assert.Equal("CNC-204", doc.EquipmentId);
        Assert.Equal("PLANT-NORTH", doc.PlantId);
    }

    [Fact]
    public async Task Manual_WithSingleDigitEquipmentId_IsTagged()
    {
        var doc = await LoadMarkdownAsync("manuals", "hvac-4-manual.md", """
            # Trane Rooftop Unit Operations Manual (HVAC-4)

            Equipment: HVAC-4 (IntelliPak)

            Apply LOTO per SOP-LOTO-01.
            """);

        Assert.Equal("HVAC-4", doc.EquipmentId);
    }

    [Fact]
    public async Task SafetyProcedure_HasNoEquipment()
    {
        var doc = await LoadMarkdownAsync("safety", "sop-loto-01.md", """
            # Lockout/Tagout Procedure (SOP-LOTO-01)

            Plant applicability: PLANT-NORTH, PLANT-SOUTH
            Document type: safety
            """);

        Assert.Equal(DocTypes.Safety, doc.DocType);
        Assert.Null(doc.EquipmentId);
        Assert.Null(doc.PlantId);
    }

    [Fact]
    public async Task SafetyProcedure_WithEquipmentFocus_StaysGeneral()
    {
        var doc = await LoadMarkdownAsync("safety", "sop-robot-04.md", """
            # Robot Cell Entry (SOP-ROBOT-04)

            Document type: safety
            Equipment focus: RBT-07, RBT-08
            """);

        Assert.Null(doc.EquipmentId);
    }

    [Theory]
    [InlineData("runbooks", "cnc-204-runbook.md", "runbook")]
    [InlineData("incidents", "ir-001.md", "incident")]
    public async Task DeclaredDocType_WinsOverLotoMentions(string folder, string file, string docType)
    {
        var doc = await LoadMarkdownAsync(folder, file, $"""
            # Title

            Document type: {docType}
            Equipment: CNC-204

            - Machine locked out per SOP-LOTO-01; safety first.
            """);

        Assert.Equal(docType, doc.DocType);
        Assert.Equal("CNC-204", doc.EquipmentId);
    }

    [Fact]
    public async Task JsonDocument_WithoutDocType_DefaultsToRunbook()
    {
        var path = Write("runbooks", "policy.json", """{ "id": "policy-x", "title": "X", "content": "body" }""");

        var doc = await new JsonDocumentLoader().LoadAsync(path);

        Assert.Equal(DocTypes.Runbook, doc.DocType);
    }

    [Theory]
    [InlineData("How do I replace the spindle bearing on CNC-204?", "CNC-204")]
    [InlineData("hvac-4 filter change", "HVAC-4")]
    [InlineData("What does SOP-LOTO-01 require?", null)]
    [InlineData("Where do I order KST-CNC-204-BRG?", null)]
    public void Retriever_ExtractEquipmentId_IgnoresIdsEmbeddedInLongerCodes(string query, string? expected)
    {
        Assert.Equal(expected, Retriever.ExtractEquipmentId(query));
    }

    private Task<SourceDocument> LoadMarkdownAsync(string folder, string file, string content)
        => new MarkdownDocumentLoader().LoadAsync(Write(folder, file, content));

    private string Write(string folder, string file, string content)
    {
        var dir = Path.Combine(_root, folder);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, file);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
