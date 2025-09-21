using Bribery.Api;
using Bribery.Api.Contracts;
using Bribery.Domain;
using Bribery.Domain.Models;
using Microsoft.AspNetCore.Diagnostics;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddSingleton<ITimeProvider, SystemTimeProvider>();
builder.Services.AddSingleton(provider => new PromptLibrary(DefaultPrompts()));
builder.Services.AddSingleton(provider => new RandomBribeLibrary(DefaultBribeSubjects(), DefaultBribeActivities()));
builder.Services.AddSingleton<GameService>();
builder.Services.AddHostedService<GameTickHostedService>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

app.UseCors();
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerPathFeature>();
        var exception = feature?.Error;
        context.Response.ContentType = "application/json";
        switch (exception)
        {
            case GameRuleException ruleException:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { error = ruleException.Message });
                break;
            case null:
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new { error = "An unknown error occurred." });
                break;
            default:
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new { error = exception.Message });
                break;
        }
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var games = app.MapGroup("/api/games");

games.MapPost("/", (CreateGameRequest request, GameService service) =>
{
    var settings = request.Settings ?? GameSettings.Default;
    var state = service.CreateGame(request.HostName, settings);
    return Results.Ok(state);
});

games.MapGet("/{gameId:guid}", (Guid gameId, GameService service) =>
{
    var state = service.GetGame(gameId);
    return Results.Ok(state);
});

games.MapGet("/prompts", (PromptLibrary library) => Results.Ok(library.All));

games.MapPost("/{code}/join", (string code, JoinGameRequest request, GameService service) =>
{
    var player = service.JoinGame(code, request.Name, request.PlayerId);
    var state = service.GetGameByCode(code);
    return Results.Ok(new JoinGameResponse(player, state));
});

app.MapGet("/api/library/prompts", (PromptLibrary library) => Results.Ok(library.All));

// Additional endpoints rely on game id; helper to avoid duplicate retrieval

games.MapPost("/{gameId:guid}/start", (Guid gameId, StartGameRequest request, GameService service) =>
{
    var state = service.StartGame(gameId, request.PlayerId);
    return Results.Ok(state);
});

games.MapPost("/{gameId:guid}/settings", (Guid gameId, UpdateSettingsRequest request, GameService service) =>
{
    var state = service.UpdateSettings(gameId, request.PlayerId, request.Settings);
    return Results.Ok(state);
});

games.MapPost("/{gameId:guid}/prompts", (Guid gameId, PromptSelectionRequest request, GameService service) =>
{
    var selection = new PromptSelection(request.Prompt, request.Source);
    service.ConfirmPrompt(gameId, request.PlayerId, selection);
    var state = service.GetGame(gameId);
    return Results.Ok(state);
});

games.MapPost("/{gameId:guid}/submissions", (Guid gameId, SubmitBribeRequest request, GameService service) =>
{
    var submission = request.Type.ToLowerInvariant() switch
    {
        "text" => BribeSubmission.FromText(request.Content),
        "image" => BribeSubmission.FromImage(request.Content),
        _ => throw new GameRuleException("Unsupported bribe type. Use 'text' or 'image'.")
    };
    service.SubmitBribe(gameId, request.PlayerId, request.TargetId, submission);
    var state = service.GetGame(gameId);
    return Results.Ok(state);
});

games.MapPost("/{gameId:guid}/votes", (Guid gameId, VoteRequest request, GameService service) =>
{
    service.CastVote(gameId, request.PlayerId, request.ChosenBriberId);
    var state = service.GetGame(gameId);
    return Results.Ok(state);
});

games.MapPost("/{gameId:guid}/advance", (Guid gameId, AdvanceRequest request, GameService service) =>
{
    service.AdvanceFromScoreboard(gameId, request.PlayerId);
    var state = service.GetGame(gameId);
    return Results.Ok(state);
});

games.MapPost("/{gameId:guid}/connection", (Guid gameId, ConnectionUpdateRequest request, GameService service) =>
{
    var player = service.UpdateConnection(gameId, request.PlayerId, request.IsConnected);
    return Results.Ok(player);
});

games.MapPost("/{gameId:guid}/kick", (Guid gameId, KickPlayerRequest request, GameService service) =>
{
    var state = service.RemovePlayer(gameId, request.HostId, request.PlayerId);
    return Results.Ok(state);
});

app.Run();

static IEnumerable<string> DefaultPrompts() => new[]
{
    "Convince them to give you their dessert",
    "Offer to babysit their dragon",
    "Promise to do their chores for a year",
    "Trade your best secret",
    "Sing them a lullaby",
    "Write their campaign speech"
};

static IEnumerable<string> DefaultBribeSubjects() => new[]
{
    "a dancing penguin",
    "a golden llama",
    "a teleporting cactus",
    "a whispering taco"
};

static IEnumerable<string> DefaultBribeActivities() => new[]
{
    "juggling fireworks",
    "painting invisible murals",
    "hosting a midnight tea party",
    "composing interpretive dance"
};
