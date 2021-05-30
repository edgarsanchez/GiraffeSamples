module rec ProductService

open System
open System.Data
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open FSharp.Control.Tasks
open Dapper
open Giraffe
open Giraffe.EndpointRouting

// Record used to return just a handful of the DB table columns
type ProductPartial = { ProductID: int; Name: string; Color: string; ListPrice: decimal; SellStartDate: DateTime }
// Record used to insert/update all the mandatory DB table columns
type Product = { ProductID: int; Name: string; ProductNumber: string; Color: string; StandardCost: decimal; ListPrice: decimal; SellStartDate: DateTime; RowGuid: Guid; ModifiedDate: DateTime }
// Record used to get back the column valued created/modified by an INSERT or UPDATE operation
type UpsertResult = { ProductID: int; RowGuid: Guid; ModifiedDate: DateTime }

// Get back all the rows of the Product table
// You don't really want to do this in a production setting, but most of us want to see how to do it :-)
let getProducts next (ctx: HttpContext) =
    task {
        // The IDbConnection instance is actually provided by ASP.NET Core Dependency injection
        // For this to work we have to configure this in Program.configureServices() using AddTransient()
        use connection = ctx.GetService<IDbConnection>()
        let! products = connection.QueryAsync<ProductPartial> 
                            "SELECT ProductID, Name, Color, ListPrice, SellStartDate FROM SalesLT.Product"
        return! json products next ctx 
    }

let getProduct idArg next (ctx: HttpContext) =
    task {
        // The ILogger instance is actually provided by ASP.NET Core Dependency Injection
        // Logging being so usual, this has its own configuration function hostBuilder.ConfigureLogging()
        let logger = ctx.GetLogger(nameof(ProductService))
        logger.LogInformation($"Function {nameof(getProduct)} called at {DateTime.Now}")
        
        use connection = ctx.GetService<IDbConnection>()
        let! product = connection.QuerySingleOrDefaultAsync<ProductPartial>("""
                            SELECT ProductID, Name, Color, ListPrice, SellStartDate
                            FROM SalesLT.Product
                            WHERE ProductId = @IdArg""",
                            {| IdArg = idArg |})
        // A nuissance between F# and a C# library (Dapper):
        // An F# record can never have a null value, so trying a `isNull product` is flagged by the compiler
        // OTOH Dapper QuerySingle() returns a NULL if no row is returned so we have to write
        // `isNull (box product)` to overcome this inconsistency and check for the null value 
        if isNull (box product) then
            return! RequestErrors.NOT_FOUND $"Product with ProductID={idArg} not found." next ctx
        else
            return! json product next ctx 
    }

let postProduct next (ctx: HttpContext) =
    task {
        try
            // Get the new Product object from its JSON representation in the request body
            let! newProduct = ctx.BindJsonAsync<Product>()
            use connection = ctx.GetService<IDbConnection>()
            // The result will get the columns from the OUTPUT clause
            let! result = 
                connection.QuerySingleOrDefaultAsync<UpsertResult>("""
                    INSERT SalesLT.Product (Name, ProductNumber, Color, StandardCost, ListPrice, SellStartDate)
                    OUTPUT INSERTED.ProductID, INSERTED.rowguid, INSERTED.ModifiedDate
                    VALUES (@Name, @ProductNumber, @Color, @StandardCost, @ListPrice, @SellStartDate)""",
                    newProduct )
            // Create a record merging the new Product object and the result from OUTPUT clause
            let insertedProduct = { newProduct with ProductID = result.ProductID; RowGuid = result.RowGuid; ModifiedDate = result.ModifiedDate }
            return! Successful.CREATED insertedProduct next ctx
        with ex ->
            return! RequestErrors.CONFLICT $"I couldn't insert new product: {ex.Message}" next ctx
    }            

let putProduct idArg next (ctx: HttpContext) =
    task {
        try
            let! updatedProduct0 = ctx.BindJsonAsync<Product>()
            let updatedProduct = { updatedProduct0 with ProductID = idArg; ModifiedDate = DateTime.Now }
            use connection = ctx.GetService<IDbConnection>()
            let! result = 
                connection.QuerySingleOrDefaultAsync<UpsertResult>("""
                    UPDATE SalesLT.Product
                    SET Name=@Name, ProductNumber=@ProductNumber, Color=@Color, StandardCost=@StandardCost, ListPrice=@ListPrice, SellStartDate=@SellStartDate, ModifiedDate=@ModifiedDate
                    OUTPUT INSERTED.ProductID, INSERTED.rowguid, INSERTED.ModifiedDate
                    WHERE ProductId=@ProductID""",
                    updatedProduct )
            if isNull (box result) then
                return! RequestErrors.NOT_FOUND $"Product with ProductID={idArg} not found." next ctx
            else
                let finalProduct = { updatedProduct with ProductID = result.ProductID; RowGuid = result.RowGuid; ModifiedDate = result.ModifiedDate }
                return! Successful.ACCEPTED finalProduct next ctx
        with ex ->
            return! RequestErrors.CONFLICT $"I couldn't update product: {ex.Message}" next ctx
    }            

let deleteProduct idArg next (ctx: HttpContext) =
    task {
        use connection = ctx.GetService<IDbConnection>()
        try
            let! deletedCount = 
                connection.ExecuteAsync("DELETE SalesLT.Product WHERE ProductId=@ProductID", {| ProductId = idArg |} )
            if deletedCount = 0 then
                return! RequestErrors.NOT_FOUND $"Product with ProductID={idArg} not found." next ctx
            else
                return! Successful.OK $"Product with ProductID={idArg} has been deleted." next ctx
        with ex ->
            return! RequestErrors.BAD_REQUEST $"I couldn't delete product: {ex.Message}" next ctx
    }        

let productService = [
        GET [
            route "/products" getProducts
            routef "/products/%i" getProduct 
        ]
        POST    [ route "/products" postProduct ]
        PUT     [ routef "/products/%i" putProduct ]
        DELETE  [ routef "/products/%i" deleteProduct ]
    ]
