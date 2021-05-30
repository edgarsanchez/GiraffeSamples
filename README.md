# F# Giraffe Web Services Samples
This repository contains (almost) ready to run samples of Web services using the Giraffe library on F#. The goal is to show specific techniques in each sample rather than to create a big all-encompassing project, so far we've got:

1. **Simplest Sample**, a bare-bones project with just two GET services:
   1. /ping which returns a pong text
   2. /ec which returns a simple JSON object on the body
2. **SQL Server with Dapper CRUD services**, a quite elaborate sample with services for CRUD operations
   1. The services are implemented asynchronously using tasks
   2. The services uses the Dapper library to work against a SQL Server table
   3. The services return adequate HTTP status codes: OK, Created, NotFound, etc.
   4. The services uses a header token to authorize the calls
   5. In order for this example to work you must configure the appsettings.json file:
      1. Set the APIKey entry with your authorization token
      2. Set the DbConnectionString entry with your SQL Server connection string
      3. The SQL commands use the Products table of the AdventureWorksLT sample database

In the future we will adding other examples, some ideas:
1. CRUD services with MongoDB
2. CRUD services with Entity Framework
3. Using OAuth tokens for call authorization

We are now using Giraffe 5.0, based on ASP.NET Core routing.

Suggestions and comments most welcomed!
