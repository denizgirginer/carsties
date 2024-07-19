using MongoDB.Driver;
using MongoDB.Entities;
using SearchService;
using SearchService.Services;
using Polly;
using Polly.Retry;
using Sundry.Extensions.Http.Polly.DependencyInjection;
using MassTransit;
using SearchService.Mappers;
using SearchService.Consumers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient<AuctionSvcHttpClient>()
    .AddResiliencePipelineHandler(PollyResilienceStrategy.Retry());

//AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfiles));

//MassTransit
builder.Services.AddMassTransit(x =>
{
    x.AddConsumersFromNamespaceContaining<AuctionCreatedConsumer>();

    x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter("search", false));

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.UseRetry(r =>
        {
            r.Handle<RabbitMqConnectionException>();
            r.Interval(5, TimeSpan.FromSeconds(10));
        });

        cfg.Host(builder.Configuration["RabbitMq:Host"], "/", host =>
        {
            host.Username(builder.Configuration.GetValue("RabbitMq:Username", "guest"));
            host.Password(builder.Configuration.GetValue("RabbitMq:Password", "guest"));
        });

        cfg.ReceiveEndpoint("search-auction-created", e =>
        {
            e.UseMessageRetry(r => r.Interval(5, 5));

            e.ConfigureConsumer<AuctionCreatedConsumer>(context);
        });

        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Lifetime.ApplicationStarted.Register(async () =>
{
    await Policy.Handle<TimeoutException>()
        .WaitAndRetryAsync(5, retryAttempt => TimeSpan.FromSeconds(10))
        .ExecuteAndCaptureAsync(async () => await DbInitializer.InitDb(app));

});

app.Run();

public static class PollyResilienceStrategy
{
    public static ResiliencePipeline<HttpResponseMessage> Retry()
    {
        return new ResiliencePipelineBuilder<HttpResponseMessage>()
               .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
               {
                   ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                       .Handle<HttpRequestException>()
                       .HandleResult(result => !result.IsSuccessStatusCode),
                   Delay = TimeSpan.FromSeconds(1),
                   MaxRetryAttempts = 5,
                   UseJitter = true,
                   BackoffType = DelayBackoffType.Exponential,
                   OnRetry = args =>
                   {
                       Console.WriteLine($"Retry Attempt Number : {args.AttemptNumber} after {args.RetryDelay.TotalSeconds} seconds.");
                       return default;
                   }
               })
               .Build();
    }
}