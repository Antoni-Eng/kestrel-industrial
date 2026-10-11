using Kestrel.Core.Configuration;
using Kestrel.Core.Ingestion;
using Kestrel.Core.Models;
using Microsoft.Extensions.Options;

namespace Kestrel.Tests;

public class HeadingChunkerTests
{
    [Fact]
    public void Chunk_SplitsOnHeadings_AndKeepsMetadata()
    {
        var options = Options.Create(new RagOptions
        {
            ChunkSizeTokens = 80,
            ChunkOverlapTokens = 10
        });
        var chunker = new HeadingChunker(options);
        var doc = new SourceDocument(
            "cnc-204-manual",
            "CNC-204 Manual",
            DocTypes.Manual,
            "manuals/cnc-204-manual.md",
            "CNC-204",
            "PLANT-NORTH",
            """
            # CNC-204 Manual

            ## Overview
            This is a short overview paragraph about the mill used in Bay A.

            ## Bearing Replacement
            Apply LOTO. Remove the guard. Press out the worn bearing. Torque housing bolts to 28 Nm in three stages.
            Regrease with KG-2 using two pumps only. Verify vibration under 2.5 mm/s RMS after warm-up.
            """);

        var chunks = chunker.Chunk(doc);

        Assert.NotEmpty(chunks);
        Assert.All(chunks, c =>
        {
            Assert.Equal("CNC-204", c.EquipmentId);
            Assert.Equal("cnc-204-manual", c.DocumentId);
            Assert.False(string.IsNullOrWhiteSpace(c.Section));
        });
        Assert.Contains(chunks, c => c.Content.Contains("28 Nm", StringComparison.Ordinal));
    }
}
