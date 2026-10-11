using System.Security.Cryptography;
using System.Text;
using Google.Protobuf.Collections;
using Kestrel.Core.Abstractions;
using Kestrel.Core.Llm;
using Kestrel.Core.Models;
using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Kestrel.Core.Storage;

public sealed class QdrantVectorStore(
    QdrantClient client,
    ILlmProviderFactory llmFactory,
    ILogger<QdrantVectorStore> logger) : IVectorStore
{
    private string CollectionName => llmFactory.GetCollectionName();

    public async Task EnsureCollectionAsync(int dimensions, CancellationToken cancellationToken = default)
    {
        var name = CollectionName;
        if (await client.CollectionExistsAsync(name, cancellationToken))
        {
            return;
        }

        logger.LogInformation("Creating Qdrant collection {Collection} dims={Dims}", name, dimensions);
        await client.CreateCollectionAsync(
            name,
            new VectorParams
            {
                Size = (ulong)dimensions,
                Distance = Distance.Cosine
            },
            cancellationToken: cancellationToken);
    }

    public Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
        => client.CollectionExistsAsync(CollectionName, cancellationToken);

    public async Task UpsertAsync(
        IReadOnlyList<DocumentChunk> chunks,
        IReadOnlyList<ReadOnlyMemory<float>> vectors,
        CancellationToken cancellationToken = default)
    {
        if (chunks.Count != vectors.Count)
        {
            throw new ArgumentException("Chunk and vector counts must match.");
        }

        var points = new List<PointStruct>(chunks.Count);
        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var payload = new MapField<string, Value>
            {
                ["documentId"] = chunk.DocumentId,
                ["title"] = chunk.Title,
                ["docType"] = chunk.DocType,
                ["sourcePath"] = chunk.SourcePath,
                ["section"] = chunk.Section,
                ["content"] = chunk.Content,
                ["chunkIndex"] = chunk.ChunkIndex
            };

            // Leave the key out when unset so the search filter can use IsEmpty.
            if (!string.IsNullOrWhiteSpace(chunk.EquipmentId))
            {
                payload["equipmentId"] = chunk.EquipmentId;
            }

            if (!string.IsNullOrWhiteSpace(chunk.PlantId))
            {
                payload["plantId"] = chunk.PlantId;
            }

            points.Add(new PointStruct
            {
                Id = new PointId { Uuid = ToUuid(chunk.Id) },
                Vectors = vectors[i].ToArray(),
                Payload = { payload }
            });
        }

        await client.UpsertAsync(CollectionName, points, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        ReadOnlyMemory<float> vector,
        int topK,
        string? equipmentId,
        CancellationToken cancellationToken = default)
    {
        if (!await CollectionExistsAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                $"Qdrant collection '{CollectionName}' does not exist. Run ingestion for the active embedding profile.");
        }

        // Equipment-specific docs for this asset, plus general docs (SOPs, policies) with no equipment.
        Filter? filter = null;
        if (!string.IsNullOrWhiteSpace(equipmentId))
        {
            filter = new Filter
            {
                Should =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "equipmentId",
                            Match = new Match { Keyword = equipmentId }
                        }
                    },
                    new Condition
                    {
                        IsEmpty = new IsEmptyCondition { Key = "equipmentId" }
                    }
                }
            };
        }

        var results = await client.SearchAsync(
            CollectionName,
            vector.ToArray(),
            filter: filter,
            limit: (ulong)topK,
            cancellationToken: cancellationToken);

        // If equipment filter is too strict, fall back to unfiltered search.
        if (results.Count == 0 && filter is not null)
        {
            results = await client.SearchAsync(
                CollectionName,
                vector.ToArray(),
                limit: (ulong)topK,
                cancellationToken: cancellationToken);
        }

        var ranked = new List<RetrievedChunk>(results.Count);
        var rank = 1;
        foreach (var hit in results)
        {
            var chunk = FromPayload(hit.Id.Uuid, hit.Payload);
            ranked.Add(new RetrievedChunk(chunk, hit.Score, rank++));
        }

        return ranked;
    }

    private static DocumentChunk FromPayload(string id, IReadOnlyDictionary<string, Value> payload)
    {
        string Get(string key) => payload.TryGetValue(key, out var v) ? v.StringValue : string.Empty;
        int GetInt(string key) => payload.TryGetValue(key, out var v) ? (int)v.IntegerValue : 0;

        var equipmentId = Get("equipmentId");
        var plantId = Get("plantId");

        return new DocumentChunk(
            id,
            Get("documentId"),
            Get("title"),
            Get("docType"),
            Get("sourcePath"),
            string.IsNullOrWhiteSpace(equipmentId) ? null : equipmentId,
            string.IsNullOrWhiteSpace(plantId) ? null : plantId,
            Get("section"),
            Get("content"),
            GetInt("chunkIndex"));
    }

    private static string ToUuid(string id)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(id));
        return new Guid(hash).ToString();
    }
}
