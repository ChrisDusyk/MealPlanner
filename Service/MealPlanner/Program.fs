namespace MealPlanner
#nowarn "20"
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open System.Text.Json.Serialization
open Giraffe

module Program =
    let exitCode = 0

    [<EntryPoint>]
    let main args =

        let builder = WebApplication.CreateBuilder(args)
        
        builder.Services.AddAuthorization()
        builder.Logging.AddConsole()
        builder.Services.AddGiraffe()

        // Set up System.Text.Json with Fsharp.SystemTextJson for better serialization support
        let options = JsonFSharpOptions.Default().ToJsonSerializerOptions()
        builder.Services.AddSingleton<Json.ISerializer>(SystemTextJson.Serializer(options))

        builder.Services.AddNpgsqlDataSource(builder.Configuration.GetConnectionString("DefaultConnection"))

        let routes =
            choose [
                route "/ping" >=> Handlers.pingHandler
                subRoute "/api"
                    (choose [
                        GET >=> choose [
                            route "/books" >=> Handlers.getBooksHandler
                            routef "/books/%s" Handlers.getBookByIdHandler
                        ]
                        POST >=> choose [
                            route "/books" >=> Handlers.createBookHandler
                        ]
                        PUT >=> choose [
                            routef "/books/%s" Handlers.updateBookHandler
                        ]
                    ])
                
                RequestErrors.NOT_FOUND "Not Found"
            ]
        let app = builder.Build()

        app.UseGiraffe routes

        app.UseAuthorization()

        app.Run()

        exitCode
