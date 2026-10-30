using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace ViewsExercise.Tests;

// Runs each of your .sql files against your own Northwind database and compares what it returns with the
// file's Expect line. Nothing is changed: every file runs inside a transaction that is rolled back, so the
// views, procedures and functions you made in SSMS are still there afterwards.
// You don't need to change this file.
[TestClass]
public sealed class QueryTests
{
    // The NORTHWIND_CONNECTION environment variable, if set, points the tests at another database.
    private static readonly string connectionString =
        Environment.GetEnvironmentVariable("NORTHWIND_CONNECTION")
        ?? "Server=localhost;Database=Northwind;Integrated Security=True;TrustServerCertificate=True";

    // The object each file creates. It is dropped first, inside the transaction, so a file that uses CREATE
    // rather than CREATE OR ALTER still runs again; the rollback puts yours back.
    private static readonly Dictionary<string, string> creates = new()
    {
        ["01_Customer_Contacts_View.sql"] = "VIEW IF EXISTS CustomerContacts",
        ["02_Order_Totals_View.sql"] = "VIEW IF EXISTS OrderTotals",
        ["05_Orders_For_Customer_Procedure.sql"] = "PROCEDURE IF EXISTS GetOrdersForCustomer",
        ["06_Price_Range_Procedure.sql"] = "PROCEDURE IF EXISTS GetProductsInPriceRange",
        ["07_Add_Shipper_Procedure.sql"] = "PROCEDURE IF EXISTS AddShipper",
        ["08_Full_Name_Function.sql"] = "FUNCTION IF EXISTS dbo.FullName",
        ["09_Orders_Taken_By_Function.sql"] = "FUNCTION IF EXISTS dbo.OrdersTakenBy",
        ["10_Bonus_Days_To_Ship.sql"] = "FUNCTION IF EXISTS dbo.DaysToShip",
    };

    // Runs the files in order in one transaction (their GO lines split them into batches), then the check
    // query if there is one, and returns every result the last file (or the check) shows, in order.
    private static List<DataTable> Run(string[] files, string? check = null)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var results = new List<DataTable>();
        foreach (string file in files)
        {
            results = [];
            string sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Queries", file));
            if (creates.TryGetValue(file, out string? created))
            {
                Execute(connection, transaction, $"DROP {created};", results);
            }
            foreach (string batch in Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(batch)) Execute(connection, transaction, batch, results);
            }
            Assert.IsNotEmpty(results, $"{file} shows no result yet. Write your SQL under the problem: the CREATE, GO, then the query that uses it.");
        }
        if (check is not null)
        {
            results = [];
            Execute(connection, transaction, check, results);
        }
        transaction.Rollback();
        return results;
    }

    private static List<DataTable> Run(string file) => Run([file]);

    private static void Execute(SqlConnection connection, SqlTransaction transaction, string sql, List<DataTable> results)
    {
        using var command = new SqlCommand(sql, connection, transaction);
        using var reader = command.ExecuteReader();
        while (!reader.IsClosed && reader.FieldCount > 0)
        {
            var result = new DataTable();
            result.Load(reader);   // reads one result, then moves on to the next
            results.Add(result);
        }
    }

    // True when one of the row's values is the text, or the row reads it across its values (a first and a
    // last name in two columns), so your column names and order don't matter.
    private static bool Has(DataRow row, string value) =>
        row.ItemArray.Any(cell => string.Equals(cell?.ToString(), value, StringComparison.Ordinal))
        || string.Join(" ", row.ItemArray.Select(cell => cell?.ToString())).Contains(value, StringComparison.Ordinal);

    // True when one of the row's values is the number: 17250.0000 is the same answer as 17250.00.
    private static bool Has(DataRow row, decimal value) =>
        row.ItemArray.Any(cell => Number(cell) is decimal number && Math.Abs(number - value) < 0.005m);

    private static decimal? Number(object? cell) =>
        cell is not null and not DBNull && decimal.TryParse(Convert.ToString(cell, CultureInfo.InvariantCulture),
            NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number) ? number : null;

    private static DataRow[] Rows(DataTable result) => result.Rows.Cast<DataRow>().ToArray();

    private static void CountIs(DataTable result, decimal count, string message)
    {
        DataRow[] rows = Rows(result);
        Assert.HasCount(1, rows, $"{message} One row: the count.");
        Assert.IsTrue(Has(rows[0], count), $"{message} The count is {count}.");
    }

    [TestMethod]
    public void Problem01_CustomerContactsView_CountsTheGermanCustomers() =>
        CountIs(Run("01_Customer_Contacts_View.sql")[^1], 11m, "01: create the CustomerContacts view, then count its customers in Germany.");

    [TestMethod]
    public void Problem02_OrderTotalsView_ListsTheThreeBiggestOrders()
    {
        DataRow[] rows = Rows(Run("02_Order_Totals_View.sql")[^1]);

        Assert.HasCount(3, rows, "02: the three biggest orders, so 3 rows.");
        Assert.IsTrue(Has(rows[0], 10865m) && Has(rows[0], "QUICK-Stop") && Has(rows[0], 17250.00m), "02: the first is 10865, QUICK-Stop, 17250.00.");
        Assert.IsTrue(Has(rows[1], 11030m) && Has(rows[1], "Save-a-lot Markets") && Has(rows[1], 16321.90m), "02: the second is 11030, Save-a-lot Markets, 16321.90.");
        Assert.IsTrue(Has(rows[2], 10981m) && Has(rows[2], "Hanari Carnes") && Has(rows[2], 15810.00m), "02: the third is 10981, Hanari Carnes, 15810.00.");
    }

    [TestMethod]
    public void Problem03_QueryAView_CountsThe2018OrdersOver5000() =>
        // 03 uses the view 02 makes, so 02 runs first.
        CountIs(Run(["02_Order_Totals_View.sql", "03_Query_A_View.sql"])[^1], 16m, "03: count the 2018 orders in OrderTotals worth more than $5,000.");

    [TestMethod]
    public void Problem04_TheCourseView_CountsAlfkisLines()
    {
        try
        {
            CountIs(Run("04_The_Course_View.sql")[^1], 12m, "04: count the CustomerOrders lines for ALFKI.");
        }
        catch (SqlException e) when (e.Message.Contains("CustomerOrders"))
        {
            Assert.Fail("04: Northwind has no CustomerOrders view yet. Run Northwind_Views.sql from the course's sample databases first.");
        }
    }

    [TestMethod]
    public void Problem05_OrdersForCustomerProcedure_ListsAlfkisOrdersOldestFirst()
    {
        DataRow[] rows = Rows(Run("05_Orders_For_Customer_Procedure.sql")[^1]);

        Assert.HasCount(6, rows, "05: ALFKI placed 6 orders.");
        Assert.IsTrue(Has(rows[0], 10643m), "05: oldest first, the first is order 10643.");
        Assert.IsTrue(rows[0].ItemArray.OfType<DateTime>().Any(date => date.Date == new DateTime(2017, 8, 25)), "05: order 10643 was placed 2017-08-25. Show the OrderDate too.");
    }

    [TestMethod]
    public void Problem06_PriceRangeProcedure_RunsForTenToTwelveThenUpToFive()
    {
        List<DataTable> results = Run("06_Price_Range_Procedure.sql");

        Assert.IsTrue(results.Count >= 2, "06: run the procedure twice: for 10 to 12, then with only @MaxPrice = 5.");
        DataRow[] tenToTwelve = Rows(results[^2]), upToFive = Rows(results[^1]);
        Assert.HasCount(4, tenToTwelve, "06: 4 products cost 10 to 12. BETWEEN includes both ends.");
        string[] names = ["Aniseed Syrup", "Sir Rodney's Scones", "Longlife Tofu", "Spegesild"];
        for (int i = 0; i < names.Length; i++)
        {
            Assert.IsTrue(Has(tenToTwelve[i], names[i]), $"06: cheapest first, row {i + 1} for 10 to 12 is {names[i]}.");
        }
        Assert.HasCount(2, upToFive, "06: 2 products cost up to 5, when @MinPrice takes its default of 0.");
        Assert.IsTrue(Has(upToFive[0], "Geitost") && Has(upToFive[1], "Guaraná Fantástica"), "06: up to 5, cheapest first: Geitost, then Guaraná Fantástica.");
    }

    [TestMethod]
    public void Problem07_AddShipperProcedure_AddsTheShipperAndReturnsItsId()
    {
        // The id is 4 in a fresh Northwind; it is higher once a shipper has been added (even one rolled back).
        List<DataTable> results = Run(["07_Add_Shipper_Procedure.sql"],
            check: "SELECT ShipperId, CompanyName, Phone FROM Shippers WHERE ShipperId = IDENT_CURRENT('Shippers');");
        DataRow[] added = Rows(results[^1]);

        Assert.HasCount(1, added, "07: the procedure adds a row to Shippers.");
        Assert.IsTrue(Has(added[0], "Rapid Couriers") && Has(added[0], "(503) 555-0199"), "07: run it for Rapid Couriers, (503) 555-0199.");
    }

    [TestMethod]
    public void Problem07_AddShipperProcedure_ReturnsTheNewShipperId()
    {
        DataRow[] rows = Rows(Run("07_Add_Shipper_Procedure.sql")[^1]);

        Assert.HasCount(1, rows, "07: the procedure returns one row: the new ShipperId (SCOPE_IDENTITY()).");
        Assert.IsTrue(rows[0].ItemArray.Any(cell => Number(cell) is > 3m), "07: the new ShipperId is 4 in a fresh Northwind, higher after shippers have been added.");
    }

    [TestMethod]
    public void Problem08_FullNameFunction_ListsEveryEmployeeByTheirFullName()
    {
        DataRow[] rows = Rows(Run("08_Full_Name_Function.sql")[^1]);

        Assert.HasCount(9, rows, "08: one row per employee, so 9 rows.");
        string[] names = ["Nancy Davolio", "Andrew Fuller", "Janet Leverling"];
        for (int i = 0; i < names.Length; i++)
        {
            Assert.IsTrue(rows[i].ItemArray.Any(cell => cell?.ToString() == names[i]), $"08: by EmployeeId, row {i + 1} is {names[i]}, in one column.");
        }
    }

    [TestMethod]
    public void Problem09_OrdersTakenByFunction_CountsEmployeeNinesOrders() =>
        CountIs(Run("09_Orders_Taken_By_Function.sql")[^1], 43m, "09: count the orders dbo.OrdersTakenBy(9) returns.");

    [TestMethod]
    public void Problem10_BonusDaysToShip_AveragesEachShipper()
    {
        DataRow[] rows = Rows(Run("10_Bonus_Days_To_Ship.sql")[^1]);

        Assert.HasCount(3, rows, "10: one row per shipper, so 3 rows.");
        Assert.IsTrue(Has(rows[0], "Federal Shipping") && Has(rows[0], 7m), "10: by name, the first is Federal Shipping, 7.");
        Assert.IsTrue(Has(rows[1], "Speedy Express") && Has(rows[1], 8m), "10: the second is Speedy Express, 8.");
        Assert.IsTrue(Has(rows[2], "United Package") && Has(rows[2], 9m), "10: the last is United Package, 9.");
    }
}
