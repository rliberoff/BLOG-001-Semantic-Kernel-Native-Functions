using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hellang.Middleware.ProblemDetails;

using Microsoft.Extensions.Logging.ApplicationInsights;

using Microsoft.OpenApi.Models;

using Microsoft.SemanticKernel;

using SemanticKernelDemo.Web;
using SemanticKernelDemo.Web.Options;
using SemanticKernelDemo.Web.Plugins;

/*
 *  Load Configuration
 */

var programType = typeof(Program);

var applicationName = programType.Assembly.GetName().Name;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions()
{
    ApplicationName = applicationName,
    Args = args,
    ContentRootPath = Directory.GetCurrentDirectory(),
});

builder.Configuration.SetBasePath(Directory.GetCurrentDirectory());

if (Debugger.IsAttached)
{
    builder.Configuration.AddJsonFile(@"appsettings.debug.json", optional: true, reloadOnChange: true);
}

builder.Configuration.AddJsonFile($@"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                     .AddJsonFile($@"appsettings.{Environment.UserName}.json", optional: true, reloadOnChange: true)
                     .AddEnvironmentVariables()
                     ;

var isDevelopment = builder.Environment.IsDevelopment();

/*
 *  Logging Configuration
 */

if (isDevelopment)
{
    builder.Logging.AddConsole();

    if (Debugger.IsAttached)
    {
        builder.Logging.AddDebug();
    }
}

var applicationInsightsConnectionString = builder.Configuration.GetConnectionString(@"ApplicationInsights");

builder.Logging.AddApplicationInsights((telemetryConfiguration) => telemetryConfiguration.ConnectionString = applicationInsightsConnectionString, (_) => { })
               .AddFilter<ApplicationInsightsLoggerProvider>(string.Empty, LogLevel.Trace)
               ;

/*
 *  Options Configuration
 */

builder.Services.AddOptions<SemanticKernelOptions>()
                .Bind(builder.Configuration.GetSection(nameof(SemanticKernelOptions)))
                .ValidateDataAnnotations()
                .ValidateOnStart();

builder.Services.AddOptions<HuggingFaceOptions>()
                .Bind(builder.Configuration.GetSection(nameof(HuggingFaceOptions)))
                .ValidateDataAnnotations()
                .ValidateOnStart();

/*
 *  Services Configuration
 */

builder.Services.AddApplicationInsightsTelemetry(builder.Configuration)
                .AddHttpClient()
                .AddRouting()
                ;

/*
 *  MVC Configuration
 */

builder.Services.AddProblemDetails(options =>
{
    // Only include exception details in a development environment.
    options.IncludeExceptionDetails = (_, _) => isDevelopment;

    // Just in case, map 'NotImplementedException' to the '501 Not Implemented' HTTP status code.
    options.MapToStatusCode<NotImplementedException>(StatusCodes.Status501NotImplemented);

    // Because exceptions are handled polymorphically, this will act as a "catch all" mapping, which is
    // why it's added last. If an exception other than any mapped before is thrown, this will handle it.
    options.MapToStatusCode<Exception>(StatusCodes.Status500InternalServerError);
})
.AddControllers(options =>
{
    options.RequireHttpsPermanent = true;
    options.SuppressAsyncSuffixInActionNames = true;
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
});

/*
 *  OpenAPI (Swagger) Configuration
 */

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(Constants.ApiVersion, new OpenApiInfo
    {
        Version = Constants.ApiVersion,
        Title = Constants.ApiTitle,
        Description = Constants.ApiDescription,
    });

    options.EnableAnnotations();
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $@"{applicationName}.xml"));
});

/*
 *  Semantic Kernel Configuration
 */

builder.Services.AddSingleton<HuggingFaceDeepsetRobertaQuestionsAnsweringPlugin>();

builder.Services.AddScoped(sp =>
{
    var logger = sp.GetRequiredService<ILogger<IKernel>>();

    var kernel = new KernelBuilder()
        .WithLogger(logger)
        .Build();

    kernel.ImportSkill(sp.GetRequiredService<HuggingFaceDeepsetRobertaQuestionsAnsweringPlugin>(), 
                       HuggingFaceDeepsetRobertaQuestionsAnsweringPlugin.SkillName);

    return kernel;
});

/*
 *  Application Middleware Configuration
 */

var app = builder.Build();

if (isDevelopment)
{
    app.UseDeveloperExceptionPage()
       .UseSwagger()
       .UseSwaggerUI(options =>
       {
           options.SwaggerEndpoint($@"/swagger/{Constants.ApiVersion}/swagger.json", Constants.ApiVersion);

           options.DocumentTitle = Constants.ApiTitle;
           options.RoutePrefix = string.Empty;
       });
}

app.UseDefaultFiles()
   .UseStaticFiles()
   .UseProblemDetails()
   .UseRouting()
   .UseAuthentication()
   .UseAuthorization()
   .UseEndpoints(endpoints =>
   {
       endpoints.MapControllers();
   })
   ;

app.Run();
