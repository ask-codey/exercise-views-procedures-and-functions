-- 06. Create a stored procedure named GetProductsInPriceRange with two money parameters: @MinPrice, which defaults to 0, and @MaxPrice. It returns the ProductName and UnitPrice of the products priced between them (BETWEEN includes both ends), cheapest first. Run it for 10 to 12, then with only @MaxPrice = 5.
-- Expect: 4 rows for 10 to 12 (Aniseed Syrup, Sir Rodney's Scones, Longlife Tofu, Spegesild); 2 rows for up to 5 (Geitost, Guaraná Fantástica).

-- Write your SQL below. Put GO on a line of its own after a CREATE.
