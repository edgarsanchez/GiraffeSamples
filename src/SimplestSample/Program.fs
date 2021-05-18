open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Giraffe

type Country = { Id: int; Name: string; Gini: float }

let webApp =
    choose [
        route "/ping"   >=> text "pong"
        route "/ec"     >=> json { Id = 593; Name = "Ecuador"; Gini = 45.4 }
    ]

let configureApp (app: IApplicationBuilder) =
    app.UseGiraffe webApp

let configureServices (services: IServiceCollection) =
    services.AddGiraffe() |> ignore

[<EntryPoint>]
let main _ =
    Host
        .CreateDefaultBuilder()
        .ConfigureWebHostDefaults(
            fun hostBuilder ->
                hostBuilder
                    .Configure(configureApp)
                    .ConfigureServices(configureServices)
                |> ignore )
        .Build()
        .Run()

    0
