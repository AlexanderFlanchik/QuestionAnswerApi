using System.Dynamic;
using Jint;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Bson;
using MongoDB.Driver;

namespace QuestionAnswerApi.Services;

public interface IScriptService
{
    Task<ScriptResult> ExecuteScriptAsync(string script, IDictionary<string, string>? parameters);
}
  
public class ScriptService(
    MongoClient client,
    IConfiguration configuration,
    IMemoryCache cache,
    ILogger<ScriptService> logger) : IScriptService
{
    private readonly IMongoDatabase _database = client.GetDatabase(configuration.GetSection("Parameters:dbName").Value);
    
    public async Task<ScriptResult> ExecuteScriptAsync(string script, IDictionary<string, string>? parameters)
    {
        var result = new ScriptResult();
        var tsc = new TaskCompletionSource<ScriptResult>();
        
        using var engine = CreateScriptEngine(result, tsc, parameters);
        var scriptTimoutValue = configuration.GetSection("Parameters:scriptTimeout").Value ?? string.Empty;
        if (!TimeSpan.TryParse(scriptTimoutValue, out var scriptTimeout))
        {
            scriptTimeout = TimeSpan.FromSeconds(10);
        }
        
        var timeoutTask = Task.Delay(scriptTimeout);
        
        try
        {
            var scriptContent =  await cache.GetOrCreateAsync(
                script, 
                async (_)  => await File.ReadAllTextAsync(Path.Combine("scripts", script)), 
                new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) }
                );
            
            engine.Execute(GetMainScriptTemplate(scriptContent!));
        
            await Task.WhenAny(tsc.Task, timeoutTask);
            if (tsc.Task.IsCompletedSuccessfully)
            {
                return result;
            }
            
            tsc.TrySetCanceled();
            throw new TimeoutException("Script execution timed out");
        }
        catch (Exception e)
        {
            logger.LogError(e, "[Script Error]: {message}", e.Message);
            throw;
        }
    }

    private Engine CreateScriptEngine(ScriptResult result, TaskCompletionSource<ScriptResult> tsc, IDictionary<string, string>? parameters)
    {
        var engine = new Engine();
        
        // CRUD
        engine.SetValue("query", async (string collectionName, ExpandoObject p) =>
        {
            logger.LogInformation("query method invoked");
            return await QueryAsync(collectionName, p.ToBsonDocument());
        });

        engine.SetValue("insert", async (string collectionName, object document) =>
        {
            logger.LogInformation("insert method invoked");
            await InsertAsync(collectionName, document);
        });

        engine.SetValue("update", async (string collectionName, ExpandoObject filterObj, ExpandoObject updateObj) =>
        {
            logger.LogInformation("update method invoked");
            await UpdateAsync(collectionName, filterObj.ToBsonDocument(), updateObj.ToBsonDocument());
        });

        engine.SetValue("remove", async (string collectionName, ExpandoObject filterObj) =>
        {
            logger.LogInformation("remove method invoked");
            await RemoveAsync(collectionName, filterObj.ToBsonDocument());
        });

        // Aggregation
        engine.SetValue("aggregate", async (string collectionName, ExpandoObject pipelineObj) =>
        {
            logger.LogInformation("aggregate method invoked");
            var arr = pipelineObj.ToBsonDocument();
            var pipelineStages = new List<BsonDocument>();
            foreach (var a in arr)
            {
                if (a.Value is not BsonDocument stage)
                {
                    continue;
                }
                
                var bd = new BsonDocument();
                bd.Set(a.Name, stage);
                pipelineStages.Add(bd);
            }
            
            return await AggregateAsync(collectionName, pipelineStages.ToArray());
        });
        
        // Parameters and callback
        engine.SetValue("parameters", parameters);
        engine.SetValue("callback", (string? data, string? error) =>
        {
            result.Data = data;
            result.Error = error;
            tsc.SetResult(result);
        });
        
        // Helpers
        engine.SetValue("log", (string entry) => logger.LogInformation("[Script Log]: {entry}", entry));
        engine.SetValue("newId", () => Guid.NewGuid().ToString());
        
        return engine;
    }
    
    private async Task<string> QueryAsync(string collectionName, BsonDocument? parameters)
    {
        var dbCollection = _database.GetCollection<BsonDocument>(collectionName);
        var filter = Builders<BsonDocument>.Filter.Empty;
        
        if (parameters is not null)
        {
            filter &= GetFiltered(parameters);
        }
        
        var docs = await dbCollection.Find(filter).ToListAsync();
        var jsonData = docs?.ToJson() ?? "{}";
        
        return jsonData;
    }
    
    private async Task InsertAsync(string collectionName, object document)
    {
        var dbCollection = _database.GetCollection<BsonDocument>(collectionName);
        var jsonData = document.ToJson();
        await dbCollection.InsertOneAsync(BsonDocument.Parse(jsonData));
    }

    private async Task UpdateAsync(string collectionName, BsonDocument filters, BsonDocument updates)
    {
        var dbCollection = _database.GetCollection<BsonDocument>(collectionName);
        
        var updateDefinition = new BsonDocument("$set", GetFiltered(updates));
        await dbCollection.UpdateManyAsync(GetFiltered(filters), updateDefinition);
        
        logger.LogInformation("Update successful");
    }

    private async Task RemoveAsync(string collectionName, BsonDocument filters)
    {
        var dbCollection = _database.GetCollection<BsonDocument>(collectionName);
        await dbCollection.DeleteManyAsync(GetFiltered(filters));
        
        logger.LogInformation("Remove successful");
    }

    private async Task<string> AggregateAsync(string collectionName, BsonDocument[] pipeline)
    {
        // Example of aggregation
        // db.answers.aggregate([
        //  { $match: { questionId: "9485e294-cc09-4eec-b0a3-40885e0ec114"} },
        //  { $group: {_id: "$questionId", totalCount:{ $count:{} } }
        // }])
        var dbCollection = _database.GetCollection<BsonDocument>(collectionName);
        var pipelineDefinition = new BsonDocumentStagePipelineDefinition<BsonDocument, BsonDocument>(pipeline);
        var aggregationResult = await dbCollection.AggregateAsync(pipelineDefinition);
        var results = await aggregationResult.ToListAsync();
        
        return results.ToJson();
    }

    private BsonDocument GetFiltered(BsonDocument obj)
    {
        var objFiltered = new BsonDocument();
        FilterParameters(obj, objFiltered);

        return objFiltered;
    }
    
    private static void FilterParameters(BsonDocument obj, BsonDocument filtered)
    {
        foreach (BsonElement be in obj)
        {
            if (!be.Value.IsBsonDocument)
            {
                var newBe = new BsonElement(be.Name, be.Value);
                filtered.Add(newBe);
                continue;
            }
                    
            var childDoc = be.Value.AsBsonDocument;
            if (childDoc.ElementCount == 2 && childDoc.Names.Contains("_t") && childDoc.Names.Contains("_v"))
            {
                var valueNode = childDoc["_v"];
                if (valueNode.IsBsonDocument)
                {
                    var newValueNode = new BsonDocument();
                    FilterParameters(valueNode.AsBsonDocument, newValueNode);
                    filtered.Add(new BsonElement(be.Name, newValueNode));
                }
                else
                {
                    var newBe = new BsonElement(be.Name, valueNode);
                    filtered.Add(newBe);
                }
            }
            else
            {
                var newValueNode = new BsonDocument();
                FilterParameters(childDoc, newValueNode);
                filtered.Add(new BsonElement(be.Name, newValueNode));
            }
        }
    }

    private string GetMainScriptTemplate(string script)
    {
        return $$"""
                 try {
                    {{script}}
                 } catch {
                    callback(null, "Script error");
                 }
                 """;
    }
}