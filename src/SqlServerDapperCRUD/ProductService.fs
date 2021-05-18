module ProductService

open System
open System.Data
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks
open Dapper
open Giraffe

type ProductPartial = { ProductID: int; Name: string; Color: string; ListPrice: decimal; SellStartDate: DateTime }
type Product = { ProductID: int; Name: string; ProductNumber: string; Color: string; StandardCost: decimal; ListPrice: decimal; SellStartDate: DateTime; RowGuid: Guid; ModifiedDate: DateTime }
type UpsertResult = { ProductID: int; RowGuid: Guid; ModifiedDate: DateTime }

let getProducts next (ctx: HttpContext) =
    task {
        use connection = ctx.GetService<IDbConnection>()
        let! products = connection.QueryAsync<ProductPartial> 
                            "SELECT ProductID, Name, Color, ListPrice, SellStartDate FROM SalesLT.Product"
        return! json products next ctx 
    }

let getProduct idArg next (ctx: HttpContext) =
    task {
        use connection = ctx.GetService<IDbConnection>()
        let! product = connection.QuerySingleOrDefaultAsync<ProductPartial>("""
                            SELECT ProductID, Name, Color, ListPrice, SellStartDate
                            FROM SalesLT.Product
                            WHERE ProductId = @IdArg""",
                            {| IdArg = idArg |})
        if isNull (box product) then
            return! RequestErrors.NOT_FOUND $"Product with ProductID={idArg} not found." next ctx
        else
            return! json product next ctx 
    }

let postProduct next (ctx: HttpContext) =
    task {
        try
            let! newProduct = ctx.BindJsonAsync<Product>()
            use connection = ctx.GetService<IDbConnection>()
            let! result = 
                connection.QuerySingleOrDefaultAsync<UpsertResult>("""
                    INSERT SalesLT.Product (Name, ProductNumber, Color, StandardCost, ListPrice, SellStartDate)
                    OUTPUT INSERTED.ProductID, INSERTED.rowguid, INSERTED.ModifiedDate
                    VALUES (@Name, @ProductNumber, @Color, @StandardCost, @ListPrice, @SellStartDate)""",
                    newProduct )
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

let productService checkPermissions = //: (HttpFunc -> HttpContext -> HttpFuncResult) =
    checkPermissions >=>
        choose [
            GET >=>
                choose [
                     route "/products" >=> getProducts
                     routef "/products/%i" getProduct 
                ]
            POST    >=> route "/products" >=> postProduct
            PUT     >=> routef "/products/%i" putProduct
            DELETE  >=> routef "/products/%i" deleteProduct
        ]
