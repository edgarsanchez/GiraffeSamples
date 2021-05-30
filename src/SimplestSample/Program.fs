open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Giraffe
open Giraffe.EndpointRouting

type Country = { Id: int; Name: string; Gini: float }

let endpoints = [
        GET [
            route "/ping" (text "pong")
            route "/ec" (json { Id = 593; Name = "Ecuador"; Gini = 45.4 })
        ]
    ]

let configureApp (appBuilder: IApplicationBuilder) =
    // UseRouting() because we are using Giraffe over the ASP.NET Core routing engine
    // UseGiraffe(endpoints) makes Giraffe expose the HTTP end points defined by endpoints
    appBuilder.UseRouting().UseGiraffe(endpoints) |> ignore

let configureServices (services: IServiceCollection) =
    // AddRouting() enables ASP.NET Core routing
    // AddGiraffe() enables Giraffe on top of ASP.NET Core routing
    services.AddRouting().AddGiraffe() |> ignore

[<EntryPoint>]
let main args =
    Host
        .CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults( fun hostBuilder ->
            hostBuilder.Configure(configureApp).ConfigureServices(configureServices)
            |> ignore )
        .Build()
        .Run()

    0