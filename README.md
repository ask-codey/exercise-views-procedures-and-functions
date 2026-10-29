# Practice: Views, Procedures and Functions

Practice SQL for the lesson **Practice: Views, Procedures and Functions** in the C# Full-Stack Bootcamp on [ask-codey.com](https://www.ask-codey.com).

Ten problems that create views, stored procedures and functions in the **Northwind** database, then use them. Each file holds one problem as a comment, and what the right answer looks like: write your SQL under it.

## Getting started

1. Open SQL Server Management Studio, and connect to `localhost`.
2. Choose **File > Open > File...** and open `01_Customer_Contacts_View.sql`.
3. Choose **Northwind** in the database drop-down.
4. Write the `CREATE`, then `GO` on a line of its own, then the query that uses it. Run the whole file with **F5**.

Compare your result with the file's **Expect** line. The objects you create stay in Northwind; refresh **Object Explorer** to see them under **Views** and **Programmability**.

## Check your answers

`ViewsPractice.Tests` runs each of your files against your Northwind database and compares what it returns with the file's **Expect** line. Nothing is changed: every file runs inside a transaction that is rolled back, so the views, procedures and functions you made stay as they are. It needs the .NET SDK, which you installed in the first week. Open a terminal in this folder and run:

```bash
dotnet test ViewsPractice.Tests
```

At first every test fails: that is expected. Each problem you solve turns its test green, and a failing test's message says what the right answer looks like. Your column names and their order don't matter. The tests use the Northwind database on `localhost`; to use another one, set the `NORTHWIND_CONNECTION` environment variable to its connection string.

## Starting over

Use `CREATE OR ALTER`, so you can run a file again after fixing it. To put Northwind back as it was, run `Northwind.sql` from the course's sample databases again.

## Tips

- A `CREATE VIEW`, `CREATE PROCEDURE` or `CREATE FUNCTION` must be alone in its batch: end it with `GO`.
- Run procedures with `EXEC`; use functions inside a `SELECT`. A scalar function needs `dbo.` in front of its name.
- Some tools, such as sqlcmd, show money values with four decimal places: 17250.0000 is the same answer as the 17250.00 in an **Expect** line.
