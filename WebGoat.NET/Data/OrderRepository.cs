using WebGoatCore.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Threading;
using System.Data;
using System.Data.Common;


namespace WebGoatCore.Data
{
    public class OrderRepository
    {
        private readonly NorthwindContext _context;
        private readonly CustomerRepository _customerRepository;

        public OrderRepository(NorthwindContext context, CustomerRepository customerRepository)
        {
            _context = context;
            _customerRepository = customerRepository;
        }

        public Order GetOrderById(int orderId)
        {
            return _context.Orders.Single(o => o.OrderId == orderId);
        }
/* AC-09 SQL injection  vulnerability
        public int CreateOrder(Order order)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            // These commented lines cause EF Core to do wierd things.
            // Instead, make the query manually.

            // order = _context.Orders.Add(order).Entity;
            // _context.SaveChanges();
            // return order.OrderId;

            string shippedDate = order.ShippedDate.HasValue ? "'" + string.Format("yyyy-MM-dd", order.ShippedDate.Value) + "'" : "NULL";
            var sql = "INSERT INTO Orders (" +
                "CustomerId, EmployeeId, OrderDate, RequiredDate, ShippedDate, ShipVia, Freight, ShipName, ShipAddress, " +
                "ShipCity, ShipRegion, ShipPostalCode, ShipCountry" +
                ") VALUES (" +
                $"'{order.CustomerId}','{order.EmployeeId}','{order.OrderDate:yyyy-MM-dd}','{order.RequiredDate:yyyy-MM-dd}'," +
                $"{shippedDate},'{order.ShipVia}','{order.Freight}','{order.ShipName}','{order.ShipAddress}'," +
                $"'{order.ShipCity}','{order.ShipRegion}','{order.ShipPostalCode}','{order.ShipCountry}')";
            sql += ";\nSELECT OrderID FROM Orders ORDER BY OrderID DESC LIMIT 1;";

            using (var command = _context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = sql;
                _context.Database.OpenConnection();

                using var dataReader = command.ExecuteReader();
                dataReader.Read();
                order.OrderId = Convert.ToInt32(dataReader[0]);
            }

            sql = ";\nINSERT INTO OrderDetails (" +
                "OrderId, ProductId, UnitPrice, Quantity, Discount" +
                ") VALUES ";
            foreach (var (orderDetails, i) in order.OrderDetails.WithIndex())
            {
                orderDetails.OrderId = order.OrderId;
                sql += (i > 0 ? "," : "") +
                    $"('{orderDetails.OrderId}','{orderDetails.ProductId}','{orderDetails.UnitPrice}','{orderDetails.Quantity}'," +
                    $"'{orderDetails.Discount}')";
            }

            if (order.Shipment != null)
            {
                var shipment = order.Shipment;
                shipment.OrderId = order.OrderId;
                sql += ";\nINSERT INTO Shipments (" +
                    "OrderId, ShipperId, ShipmentDate, TrackingNumber" +
                    ") VALUES (" +
                    $"'{shipment.OrderId}','{shipment.ShipperId}','{shipment.ShipmentDate:yyyy-MM-dd}','{shipment.TrackingNumber}')";
            }

            using (var command = _context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = sql;
                _context.Database.OpenConnection();
                command.ExecuteNonQuery();
            }

            return order.OrderId;
        }
*/
        /// <summary>
        /// Opretter en ny ordre i databasen via fuldt parametriseret SQL (Løsning 2: Provider-uafhængig).
        /// Håndhæver Defense in Depth ved Tillidsgrænse 2 (TB2) og eliminerer AC-09 (SQL Injection).
        /*
        1. Ingen eksterne NuGet-afhængigheder: Ved at benytte den indbyggede `DbCommand.CreateParameter()`
           slipper man for typesammenstød og manglende assembly references som `SqliteParameter`.
        2. Klar adskillelse af data og instruktion (TB2): 
            Uanset hvad modtagernavnet indeholder (selv hvis nogen forsøger `' OR 1=1; DROP TABLE Orders; --`), 
            fortolker databasedriveren parameteren strengt som data. Databasens SQL-eksekveringstræ er allerede kompileret, før parametrene overføres.
        3. Fail-Secure & Transaktionsintegritet:
             Skulle databasen fejle undervejs, rulles transaktionen tilbage (`Rollback`), 
             og fejlen bobler kontrolleret op til middlewaren uden at efterlade databasen i en inkonsistent tilstand.

        */
        /// </summary>
        public int CreateOrder(Order order)
        {
            if (order == null) throw new ArgumentNullException(nameof(order));

            // 1. Åbn den underliggende ADO.NET-forbindelse via EF Core facaden
            using (var conn = _context.Database.GetDbConnection())
            {
                if (conn.State != ConnectionState.Open)
                {
                    conn.Open();
                }

                // 2. Eksplicit transaktionsstyring (Atomicity)
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = transaction;

                            // 3. Parametriseret SQL-sætning (adskiller kode og data fuldstændigt)
                            cmd.CommandText = @"
                                INSERT INTO Orders (
                                    CustomerId, EmployeeId, OrderDate, RequiredDate, ShippedDate, 
                                    ShipVia, Freight, ShipName, ShipAddress, ShipCity, ShipRegion, 
                                    ShipPostalCode, ShipCountry
                                ) VALUES (
                                    @CustomerId, @EmployeeId, @OrderDate, @RequiredDate, @ShippedDate, 
                                    @ShipVia, @Freight, @ShipName, @ShipAddress, @ShipCity, @ShipRegion, 
                                    @ShipPostalCode, @ShipCountry
                                );
                                SELECT last_insert_rowid();";

                            // 4. Provider-uafhængig parameterbinding via cmd.CreateParameter()
                            AddParam(cmd, "@CustomerId", DbType.String, order.CustomerId);
                            AddParam(cmd, "@EmployeeId", DbType.Int32, order.EmployeeId);
                            AddParam(cmd, "@OrderDate", DbType.String, order.OrderDate.ToString("yyyy-MM-dd"));
                            AddParam(cmd, "@RequiredDate", DbType.String, order.RequiredDate.ToString("yyyy-MM-dd"));
                            AddParam(cmd, "@ShippedDate", DbType.String, 
                                order.ShippedDate.HasValue ? order.ShippedDate.Value.ToString("yyyy-MM-dd") : null);
                            AddParam(cmd, "@ShipVia", DbType.Int32, order.ShipVia);
                            AddParam(cmd, "@Freight", DbType.Decimal, order.Freight);

                            // Sikker udpakning af ShipName domæneprimitivet (AC-09)
                            AddParam(cmd, "@ShipName", DbType.String, 
                                order.ShipName != null ? (string)order.ShipName : null);

                            AddParam(cmd, "@ShipAddress", DbType.String, order.ShipAddress);
                            AddParam(cmd, "@ShipCity", DbType.String, order.ShipCity);
                            AddParam(cmd, "@ShipRegion", DbType.String, order.ShipRegion);
                            AddParam(cmd, "@ShipPostalCode", DbType.String, order.ShipPostalCode);
                            AddParam(cmd, "@ShipCountry", DbType.String, order.ShipCountry);

                            // 5. Eksekver og hent det nyligt indsatte OrderID
                            var scalarResult = cmd.ExecuteScalar();
                            var newOrderId = Convert.ToInt32(scalarResult);

                            transaction.Commit();
                            return newOrderId;
                        }
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw; // Fail-Secure: Bobler op til global exception handler
                    }
                }
            }
        }

        /// <summary>
        /// Hjælpemetode til provider-uafhængig parameterbinding uden behov for SQLite-specifikke pakker.
        /// </summary>
        private static void AddParam(DbCommand cmd, string parameterName, DbType dbType, object value)
        {
            var parameter = cmd.CreateParameter();
            parameter.ParameterName = parameterName;
            parameter.DbType = dbType;
            parameter.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(parameter);
        }

        public void CreateOrderPayment(int orderId, decimal amountPaid, string creditCardNumber, DateTime expirationDate, string approvalCode)
        {
            var orderPayment = new OrderPayment()
            {
                AmountPaid = Convert.ToDouble(amountPaid),
                CreditCardNumber = creditCardNumber,
                ApprovalCode = approvalCode,
                ExpirationDate = expirationDate,
                OrderId = orderId,
                PaymentDate = DateTime.Now
            };
            _context.OrderPayments.Add(orderPayment);
            _context.SaveChanges();
        }

        public ICollection<Order> GetAllOrdersByCustomerId(string customerId)
        {
            return _context.Orders
                .Where(o => o.CustomerId == customerId)
                .OrderByDescending(o => o.OrderDate)
                .ThenByDescending(o => o.OrderId)
                .ToList();
        }
    }
}
