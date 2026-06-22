// =====================================================================
//  MÓDULO 0 (Lab 6) — STORED PROCEDURES con ADO.NET sobre SQL Server
// =====================================================================
//
//  Es el MISMO ejercicio del Lab 4 (PostgreSQL) pero con SQL Server, para
//  ver las diferencias de dialecto y de "programas almacenados" entre motores.
//  Seguimos en ADO.NET crudo (sin EF), sobre un MODELO RELACIONAL:
//  clientes (1) --- (N) pedidos (relación por clave foránea).
//
//  En SQL Server, a diferencia de PostgreSQL:
//    - Todo "programa con nombre" que ejecuta acciones es un STORED PROCEDURE
//      (CREATE PROCEDURE), se invoca con EXEC y puede tener parámetros OUTPUT.
//    - Las FUNCTIONS son aparte: ESCALARES (devuelven un valor) o de TABLA
//      (TVF: RETURNS TABLE, se usan dentro de un SELECT ... FROM fn(...)).
//    - Autoincremental: IDENTITY(1,1) (no SERIAL). El id generado se obtiene
//      con SCOPE_IDENTITY().
//
//  Requiere SQL Server corriendo (ver README -> Docker).
// =====================================================================

using System.Data;
using Microsoft.Data.SqlClient;

namespace Modulo0.ProcedimientosSqlServer;

class ProgramSqlServer
{
    private const string ConnMaster =
        "Server=localhost,1433;Database=master;User Id=sa;Password=Curso.NET2026;TrustServerCertificate=True";
    private const string ConnTienda =
        "Server=localhost,1433;Database=tienda;User Id=sa;Password=Curso.NET2026;TrustServerCertificate=True";

    static void MainSqlServer()
    {
        AsegurarBaseDeDatos();
        CrearEsquemaRelacional();
        CrearProgramasAlmacenados();

        int anaId  = BuscarClienteId("ana@mail.com");
        int betoId = BuscarClienteId("beto@mail.com");

        // --- 1) STORED PROCEDURE con parámetro OUTPUT (EXEC) ---
        Console.WriteLine("== EXEC sp_registrar_pedido (PROCEDURE con OUTPUT) ==");
        Console.WriteLine($"  Nuevo pedido de Ana  -> id {RegistrarPedido(anaId, 1500.00m)}");
        Console.WriteLine($"  Nuevo pedido de Ana  -> id {RegistrarPedido(anaId, 250.50m)}");
        Console.WriteLine($"  Nuevo pedido de Beto -> id {RegistrarPedido(betoId, 999.99m)}");

        // --- 2) La MISMA procedure con CommandType.StoredProcedure ---
        Console.WriteLine("\n== StoredProcedure (CommandType) ==");
        Console.WriteLine($"  Nuevo pedido de Ana  -> id {RegistrarPedidoClasico(anaId, 75.00m)}");

        // --- 3) FUNCTION de TABLA (TVF): SELECT * FROM dbo.fn(...) ---
        Console.WriteLine("\n== dbo.fn_pedidos_de_cliente (TVF que devuelve filas) ==");
        Console.WriteLine("  Pedidos de Ana:");
        PedidosDeCliente(anaId);

        // --- 4) FUNCTION ESCALAR: SELECT dbo.fn(...) ---
        Console.WriteLine("\n== dbo.fn_total_gastado (FUNCTION escalar) ==");
        Console.WriteLine($"  Total gastado por Ana : ${TotalGastado(anaId)}");
        Console.WriteLine($"  Total gastado por Beto: ${TotalGastado(betoId)}");
    }

    // =================================================================
    //  Crear la base si no existe (SQL Server no la crea solo).
    // =================================================================
    static void AsegurarBaseDeDatos()
    {
        using var conn = new SqlConnection(ConnMaster);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "IF DB_ID('tienda') IS NULL CREATE DATABASE tienda;";
        cmd.ExecuteNonQuery();
    }

    // =================================================================
    //  Modelo relacional: clientes (1) --- (N) pedidos
    // =================================================================
    static void CrearEsquemaRelacional()
    {
        using var conn = new SqlConnection(ConnTienda);
        conn.Open();

        using var cmd = conn.CreateCommand();
        // Primero pedidos (tiene la FK), después clientes. IDENTITY = autoincremental.
        cmd.CommandText = """
            DROP TABLE IF EXISTS pedidos;
            DROP TABLE IF EXISTS clientes;

            CREATE TABLE clientes (
                id     INT IDENTITY(1,1) PRIMARY KEY,
                nombre NVARCHAR(100) NOT NULL,
                email  NVARCHAR(200) NOT NULL UNIQUE
            );

            CREATE TABLE pedidos (
                id         INT IDENTITY(1,1) PRIMARY KEY,
                cliente_id INT NOT NULL REFERENCES clientes(id),  -- FK: relación 1-N
                fecha      DATETIME2     NOT NULL DEFAULT SYSDATETIME(),
                total      DECIMAL(10,2) NOT NULL
            );

            INSERT INTO clientes (nombre, email) VALUES
                ('Ana',  'ana@mail.com'),
                ('Beto', 'beto@mail.com');
            """;
        cmd.ExecuteNonQuery();
        Console.WriteLine("✅ Esquema relacional creado (clientes 1-N pedidos) + clientes cargados.\n");
    }

    // =================================================================
    //  Crear el PROCEDURE y las FUNCTIONS.
    //  IMPORTANTE (T-SQL): CREATE PROCEDURE/FUNCTION debe ser la PRIMERA
    //  sentencia de su lote. Por eso cada una va en su PROPIO ExecuteNonQuery
    //  (no se pueden mandar todas juntas como en PostgreSQL).
    //  Usamos CREATE OR ALTER para poder re-ejecutar el lab sin error.
    // =================================================================
    static void CrearProgramasAlmacenados()
    {
        using var conn = new SqlConnection(ConnTienda);
        conn.Open();

        // PROCEDURE: registra un pedido y devuelve el id por parámetro OUTPUT.
        Ejecutar(conn, """
            CREATE OR ALTER PROCEDURE sp_registrar_pedido
                @cliente_id INT,
                @total      DECIMAL(10,2),
                @nuevo_id   INT OUTPUT
            AS
            BEGIN
                SET NOCOUNT ON;
                INSERT INTO pedidos (cliente_id, total) VALUES (@cliente_id, @total);
                SET @nuevo_id = SCOPE_IDENTITY();   -- el id autogenerado
            END;
            """);

        // FUNCTION de TABLA (inline TVF): los pedidos de un cliente.
        Ejecutar(conn, """
            CREATE OR ALTER FUNCTION dbo.fn_pedidos_de_cliente(@cliente_id INT)
            RETURNS TABLE
            AS
            RETURN (
                SELECT id AS pedido_id, fecha, total
                FROM pedidos
                WHERE cliente_id = @cliente_id
            );
            """);

        // FUNCTION ESCALAR: total gastado por un cliente.
        Ejecutar(conn, """
            CREATE OR ALTER FUNCTION dbo.fn_total_gastado(@cliente_id INT)
            RETURNS DECIMAL(10,2)
            AS
            BEGIN
                RETURN (SELECT ISNULL(SUM(total), 0) FROM pedidos WHERE cliente_id = @cliente_id);
            END;
            """);

        Console.WriteLine("✅ Procedimiento y funciones creados en la base.\n");
    }

    // Helper: ejecuta una sentencia suelta sobre una conexión ya abierta.
    static void Ejecutar(SqlConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    // =================================================================
    //  Helper: id de un cliente por email (READ común).
    // =================================================================
    static int BuscarClienteId(string email)
    {
        using var conn = new SqlConnection(ConnTienda);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM clientes WHERE email = @email;";
        cmd.Parameters.AddWithValue("@email", email);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    // =================================================================
    //  (1) Ejecutar la PROCEDURE con EXEC y leer su parámetro OUTPUT.
    // =================================================================
    static int RegistrarPedido(int clienteId, decimal total)
    {
        using var conn = new SqlConnection(ConnTienda);
        conn.Open();

        using var cmd = conn.CreateCommand();
        // EXEC con el tercer argumento marcado OUTPUT: SQL Server escribe ahí
        // el id generado y nosotros lo leemos del SqlParameter tras ejecutar.
        cmd.CommandText = "EXEC sp_registrar_pedido @cliente_id, @total, @nuevo_id OUTPUT;";
        cmd.Parameters.AddWithValue("@cliente_id", clienteId);
        cmd.Parameters.AddWithValue("@total", total);

        var salida = new SqlParameter("@nuevo_id", SqlDbType.Int) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(salida);

        cmd.ExecuteNonQuery();
        return Convert.ToInt32(salida.Value);
    }

    // =================================================================
    //  (2) La MISMA procedure con CommandType.StoredProcedure (estilo clásico).
    // =================================================================
    static int RegistrarPedidoClasico(int clienteId, decimal total)
    {
        using var conn = new SqlConnection(ConnTienda);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "sp_registrar_pedido";        // solo el nombre
        cmd.CommandType = CommandType.StoredProcedure;  // <- la clave
        cmd.Parameters.AddWithValue("@cliente_id", clienteId);
        cmd.Parameters.AddWithValue("@total", total);

        var salida = new SqlParameter("@nuevo_id", SqlDbType.Int) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(salida);

        cmd.ExecuteNonQuery();
        return Convert.ToInt32(salida.Value);
    }

    // =================================================================
    //  (3) Ejecutar una FUNCTION de TABLA (TVF): se usa dentro de un SELECT.
    // =================================================================
    static void PedidosDeCliente(int clienteId)
    {
        using var conn = new SqlConnection(ConnTienda);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT pedido_id, fecha, total FROM dbo.fn_pedidos_de_cliente(@id);";
        cmd.Parameters.AddWithValue("@id", clienteId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            int id        = reader.GetInt32(0);
            DateTime fec  = reader.GetDateTime(1);
            decimal total = reader.GetDecimal(2);
            Console.WriteLine($"    Pedido #{id} — {fec:yyyy-MM-dd} — ${total}");
        }
    }

    // =================================================================
    //  (4) Ejecutar una FUNCTION ESCALAR (devuelve un único valor).
    // =================================================================
    static decimal TotalGastado(int clienteId)
    {
        using var conn = new SqlConnection(ConnTienda);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT dbo.fn_total_gastado(@id);";
        cmd.Parameters.AddWithValue("@id", clienteId);
        return Convert.ToDecimal(cmd.ExecuteScalar());
    }
}
