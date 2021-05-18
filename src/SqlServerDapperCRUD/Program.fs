open System.Data
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Data.SqlClient
open Giraffe
open ProductService

let checkPermissions =
    let validateApiKey (ctx: HttpContext) =
        match ctx.TryGetRequestHeader "X-API-Key" with
        | Some key ->
            key = ctx.GetService<IConfiguration>().["ApiKey"]
        | None ->
            false

    authorizeRequest validateApiKey (RequestErrors.UNAUTHORIZED "Basic" "CRUDServices" "No permissions to call this service.")

let configureApp (app: IApplicationBuilder) =
    app.UseGiraffe (productService checkPermissions)

let configureServices (services: IServiceCollection) =
    services.AddTransient<IDbConnection>(
                fun serviceProvider ->
                    let settings = serviceProvider.GetService<IConfiguration>()
                    upcast new SqlConnection(settings.["DbConnectionString"]) )
            .AddGiraffe() |> ignore

[<EntryPoint>]
let main args =
    Host
        .CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(
            fun hostBuilder ->
                hostBuilder
                    .Configure(configureApp)
                    .ConfigureServices(configureServices)
                    .ConfigureLogging(fun logBuilder -> logBuilder.AddConsole() |> ignore)
                |> ignore )
        .Build()
        .Run()

    0
