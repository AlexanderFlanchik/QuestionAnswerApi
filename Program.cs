using QuestionAnswerApi.Services;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<MongoClient>((_) =>
{
    var connectionString = builder.Configuration.GetConnectionString("mongoDb");
    var client = new MongoClient(connectionString);
    return client;
});
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IScriptService, ScriptService>();

var app = builder.Build();

app.MapGet("/", () => "Database Manager running...");

app.MapGet("/questions", async (IScriptService service) =>
{
    var result = await service.ExecuteScriptAsync("getQuestions.js", null);
    var jsonData = result.Data ?? "{}";
    return Results.Content(jsonData, "application/json");
});

app.MapGet("/answer-count/{questionId}", async (IScriptService service, string questionId) =>
{
    var paramters = new Dictionary<string, string>()
    {
        { "questionId", questionId }
    };
    
    var result = await service.ExecuteScriptAsync("answerCount.js", paramters);
    var jsonData = result.Data ?? "{}";
    return Results.Content(jsonData, "application/json");
});

app.MapPost("/ask-question", async (CreateQuestionRequest request, IScriptService service) =>
{
    var parameters = new Dictionary<string, string>()
    {
        { "question", request.Question },
    };

    var result = await service.ExecuteScriptAsync("addQuestion.js", parameters);
    return Results.Ok(result.Data);
});

app.MapPost("/answer-question", async (CreateAnswerRequest request, IScriptService service) =>
{
    var parameters = new Dictionary<string, string>()
    {
        { "answer", request.Answer },
        { "questionId", request.QuestionId }
    };
    
    var result = await service.ExecuteScriptAsync("answerQuestion.js", parameters);
    return string.IsNullOrEmpty(result.Error) ? Results.Ok() : Results.BadRequest(result.Error);
});

app.MapDelete("/questions/{questionId}", async (string questionId, IScriptService service) =>
{
    var parameters = new Dictionary<string, string>()
    {
        { "questionId", questionId }
    };
    
    await service.ExecuteScriptAsync("removeQuestion.js", parameters);
    return Results.Ok();
});

app.MapDelete("/answers/{answerId}", async (string answerId, IScriptService service) =>
{
    var parameters = new Dictionary<string, string>()
    {
        { "answerId", answerId }
    };
    
    await service.ExecuteScriptAsync("removeAnswer.js", parameters);
    return Results.Ok();
});

app.Run();

record CreateQuestionRequest(string Question);
record CreateAnswerRequest(string QuestionId, string Answer);