namespace MealPlanner

open System
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Giraffe

module Handlers =
    let composeDatabaseWithDataSource (context: Microsoft.AspNetCore.Http.HttpContext) handler =
        let dataSource = context.GetService<Npgsql.NpgsqlDataSource>()
        handler dataSource

    let pingHandler : HttpHandler =
        Successful.OK "Hello"
