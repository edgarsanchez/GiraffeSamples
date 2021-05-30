open System
open System.Data
open System.Text
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Primitives
open Microsoft.Data.SqlClient
open FSharp.Control.Tasks
open Giraffe
open Giraffe.EndpointRouting
open ProductService

// This function to control if there is an adequate header in the HTTP request
// is too low level for my taste, but I haven't figured any better way to do it
let checkPermissions (ctx: HttpContext) (next: Func<Task>) =
    // Get the X-API-Key header from the HTTP request
    // Check if it's equal to the ApiKey entry in appsettings.json
    let validateApiKey (ctx: HttpContext) =
        match ctx.TryGetRequestHeader "X-API-Key" with
        | Some key ->
            key = ctx.GetService<IConfiguration>().["ApiKey"]
        | None ->
            false

    // code is the HTTP status code to be returned to the caller
    // message will be returned on the body
    let setStatus code (message: string) =
        unitTask {
            ctx.Response.StatusCode <- code
            let messageBytes = Encoding.Unicode.GetBytes message
            ctx.Response.Headers.Add("Content-Length", StringValues(messageBytes.Length.ToString()))
            do! ctx.Response.Body.WriteAsync(ReadOnlyMemory(messageBytes))
        }

    unitTask {
        if validateApiKey ctx then
            // We've got a valid key in a header request, so let's continue with the request processing
            return! next.Invoke()
        else
            // Stop the request processing with an Unauthorized HTTP status code
            do! setStatus StatusCodes.Status401Unauthorized "Missing or invalid API key."
    }

let configureApp (app: IApplicationBuilder) =
    app
        // UseRouting() because we are using Giraffe over the ASP.NET Core routing engine
        .UseRouting()
        // Check the service invocation permissions before we get to any end point
        .Use(checkPermissions)
        // UseGiraffe(productService) makes Giraffe expose the HTTP end points defined by productService
        .UseGiraffe(productService) |> ignore

let configureServices (services: IServiceCollection) =
    services
        // Configure a SqlConnection instance to be used from the services
        .AddTransient<IDbConnection>( fun serviceProvider ->
            // The configuration information is in appsettings.json
            let settings = serviceProvider.GetService<IConfiguration>()
            upcast new SqlConnection(settings.["DbConnectionString"]) )
        // AddRouting() enables ASP.NET Core routing
        .AddRouting()
        // AddGiraffe() enables Giraffe on top of ASP.NET Core routing
        .AddGiraffe() |> ignore

[<EntryPoint>]
let main args =
    Host
        .CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults( fun hostBuilder ->
            hostBuilder
                .Configure(configureApp)
                .ConfigureServices(configureServices)
                // Enable a logging sink to be used from the services
                .ConfigureLogging(fun logBuilder -> logBuilder.AddConsole() |> ignore)
            |> ignore )
        .Build()
        .Run()

    0
