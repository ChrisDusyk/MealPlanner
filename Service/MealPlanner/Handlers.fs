namespace MealPlanner

open System
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Types
open Giraffe

module Handlers =
    let composeDatabaseWithDataSource (context: Microsoft.AspNetCore.Http.HttpContext) handler =
        let dataSource = context.GetService<Npgsql.NpgsqlDataSource>()
        handler dataSource

    let pingHandler : HttpHandler =
        handleContext(
            fun ctx ->
                let metadata = ctx.GetService<IOptions<Config.Metadata>>().Value
                let response =
                    {
                        Name = metadata.Name
                        Version = metadata.Version
                        Database = "magic"
                    }
                ctx.WriteJsonAsync response
        )
