namespace MealPlanner

open Npgsql.FSharp
open Types

module Database =
    let getMealsForWeek = []

// let readBook (reader : RowReader) =
//         {
//             Id = reader.int "id"
//             Name = reader.textOrNone "name"
//             Author = reader.textOrNone "author"
//             PublicId = reader.uuid "public_id"
//         }

//     let getBooks dataSource =
//         dataSource
//         |> Sql.fromDataSource
//         |> Sql.query "SELECT * FROM books"
//         |> Sql.execute readBook

//     let getBookById dataSource id =
//         dataSource
//         |> Sql.fromDataSource
//         |> Sql.query "SELECT * FROM books WHERE public_id = @public_id"
//         |> Sql.parameters [ "@public_id", Sql.uuid id ]
//         |> Sql.executeRow (fun read ->
//             if read.NpgsqlReader.HasRows then
//                 readBook read |> Some
//             else
//                 None)

//     let createBook dataSource book =
//         dataSource
//         |> Sql.fromDataSource
//         |> Sql.query "INSERT INTO books(name, author, public_id) VALUES (@name, @author, @public_id) RETURNING *"
//         |> Sql.parameters [ "@name", Sql.textOrNone book.Name; "@author", Sql.textOrNone book.Author; "@public_id", Sql.uuid book.PublicId ]
//         |> Sql.executeRow readBook

//     let updateBook dataSource book =
//         dataSource
//         |> Sql.fromDataSource
//         |> Sql.query "UPDATE books SET name = @name, author = @author WHERE public_id = @public_id RETURNING *"
//         |> Sql.parameters [ "@name", Sql.textOrNone book.Name; "@author", Sql.textOrNone book.Author; "@public_id", Sql.uuid book.PublicId ]
//         |> Sql.executeRow readBook
