// =====================================================================
//  MÓDULO 0 (Lab 8) — STORED PROCEDURES con ADO.NET sobre MySQL
// =====================================================================
//
//  Es el MISMO ejercicio del Lab 4 (PostgreSQL) y Lab 6 (SQL Server) pero con
//  MySQL, sobre un modelo relacional clientes (1) --- (N) pedidos. ADO.NET crudo.
//
//  Diferencias de MySQL que vale la pena conocer:
//    - Procedimientos: CREATE PROCEDURE ... CALL proc(...), con parámetros
//      IN / OUT / INOUT. El id generado se obtiene con LAST_INSERT_ID().
//    - MySQL NO tiene "funciones de tabla" (TVF). Para devolver un conjunto de
//      filas se usa una PROCEDURE que hace un SELECT (devuelve un result set).
//    - Sí tiene FUNCTIONS escalares (deben declarar una característica como
//      READS SQL DATA cuando el binary logging está activo).
//
//  Requiere MySQL corriendo (ver README -> Docker).
// =====================================================================

using System.Data;
using MySqlConnector;

namespace Modulo0.ProcedimientosMySql;

class Program
{
    private const string ConnectionString =
        "Server=localhost;Port=3306;Database=tienda;User ID=root;Password=ClaveTemporal2026";

    static void Main()
    {
        CrearEsquemaRelacional();
        CrearProgramasAlmacenados();

        int anaId  = BuscarClienteId("ana@mail.com");
        int betoId = BuscarClienteId("beto@mail.com");

        // --- 1) PROCEDURE con parámetro OUT (CommandType.StoredProcedure) ---
        Console.WriteLine("== sp_registrar_pedido (PROCEDURE con OUT) ==");
        Console.WriteLine($"  Nuevo pedido de Ana  -> id {RegistrarPedido(anaId, 1500.00m)}");
        Console.WriteLine($"  Nuevo pedido de Ana  -> id {RegistrarPedido(anaId, 250.50m)}");
        Console.WriteLine($"  Nuevo pedido de Beto -> id {RegistrarPedido(betoId, 999.99m)}");

        // --- 2) La misma procedure invocada con CALL + variable de sesión ---
        Console.WriteLine("\n== CALL con variable de sesión ==");
        Console.WriteLine($"  Nuevo pedido de Ana  -> id {RegistrarPedidoConCall(anaId, 75.00m)}");

        // --- 3) PROCEDURE que devuelve un RESULT SET (reemplaza a la TVF) ---
        Console.WriteLine("\n== CALL sp_pedidos_de_cliente (PROCEDURE que devuelve filas) ==");
        Console.WriteLine("  Pedidos de Ana:");
        PedidosDeCliente(anaId);

        // --- 4) FUNCTION ESCALAR: SELECT fn(...) ---
        Console.WriteLine("\n== fn_total_gastado (FUNCTION escalar) ==");
        Console.WriteLine($"  Total gastado por Ana : ${TotalGastado(anaId)}");
        Console.WriteLine($"  Total gastado por Beto: ${TotalGastado(betoId)}");
    }

    // =================================================================
    //  Modelo relacional: clientes (1) --- (N) pedidos
    // =================================================================
    static void CrearEsquemaRelacional()
    {
        using var conn = new MySqlConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DROP TABLE IF EXISTS pedidos;
            DROP TABLE IF EXISTS clientes;

            CREATE TABLE clientes (
                id     INT AUTO_INCREMENT PRIMARY KEY,
                nombre VARCHAR(100) NOT NULL,
                email  VARCHAR(200) NOT NULL UNIQUE
            );

            CREATE TABLE pedidos (
                id         INT AUTO_INCREMENT PRIMARY KEY,
                cliente_id INT NOT NULL,
                fecha      DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
                total      DECIMAL(10,2) NOT NULL,
                FOREIGN KEY (cliente_id) REFERENCES clientes(id)   -- relación 1-N
            );

            INSERT INTO clientes (nombre, email) VALUES
                ('Ana',  'ana@mail.com'),
                ('Beto', 'beto@mail.com');
            """;
        cmd.ExecuteNonQuery();
        Console.WriteLine("✅ Esquema relacional creado (clientes 1-N pedidos) + clientes cargados.\n");
    }

    // =================================================================
    //  Crear el procedure y las funciones.
    //  En ADO.NET NO hace falta DELIMITER (eso es solo del cliente mysql):
    //  cada CREATE va en su propio comando. MySQL no tiene CREATE OR REPLACE
    //  para procedimientos, así que hacemos DROP IF EXISTS + CREATE.
    // =================================================================
    static void CrearProgramasAlmacenados()
    {
        using var conn = new MySqlConnection(ConnectionString);
        conn.Open();

        // PROCEDURE: registra un pedido y devuelve el id por parámetro OUT.
        Ejecutar(conn, "DROP PROCEDURE IF EXISTS sp_registrar_pedido;");
        Ejecutar(conn, """
            CREATE PROCEDURE sp_registrar_pedido(
                IN  p_cliente_id INT,
                IN  p_total      DECIMAL(10,2),
                OUT p_nuevo_id   INT)
            BEGIN
                INSERT INTO pedidos (cliente_id, total) VALUES (p_cliente_id, p_total);
                SET p_nuevo_id = LAST_INSERT_ID();   -- el id autogenerado
            END;
            """);

        // PROCEDURE que devuelve un RESULT SET (MySQL no tiene TVF).
        Ejecutar(conn, "DROP PROCEDURE IF EXISTS sp_pedidos_de_cliente;");
        Ejecutar(conn, """
            CREATE PROCEDURE sp_pedidos_de_cliente(IN p_cliente_id INT)
            BEGIN
                SELECT id AS pedido_id, fecha, total
                FROM pedidos
                WHERE cliente_id = p_cliente_id
                ORDER BY id;
            END;
            """);

        // FUNCTION ESCALAR. READS SQL DATA es necesario con binary logging activo.
        Ejecutar(conn, "DROP FUNCTION IF EXISTS fn_total_gastado;");
        Ejecutar(conn, """
            CREATE FUNCTION fn_total_gastado(p_cliente_id INT)
            RETURNS DECIMAL(10,2)
            READS SQL DATA
            RETURN (SELECT IFNULL(SUM(total), 0) FROM pedidos WHERE cliente_id = p_cliente_id);
            """);

        Console.WriteLine("✅ Procedimientos y función creados en la base.\n");
    }

    static void Ejecutar(MySqlConnection conn, string sql)
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
        using var conn = new MySqlConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM clientes WHERE email = @email;";
        cmd.Parameters.AddWithValue("@email", email);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    // =================================================================
    //  (1) PROCEDURE con OUT vía CommandType.StoredProcedure.
    //      Es la forma más robusta en MySQL de leer un parámetro OUT.
    // =================================================================
    static int RegistrarPedido(int clienteId, decimal total)
    {
        using var conn = new MySqlConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "sp_registrar_pedido";        // solo el nombre
        cmd.CommandType = CommandType.StoredProcedure;  // <- MySqlConnector cablea el OUT

        cmd.Parameters.AddWithValue("@p_cliente_id", clienteId);
        cmd.Parameters.AddWithValue("@p_total", total);
        var salida = new MySqlParameter("@p_nuevo_id", MySqlDbType.Int32) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(salida);

        cmd.ExecuteNonQuery();
        return Convert.ToInt32(salida.Value);
    }

    // =================================================================
    //  (2) La misma procedure con CALL explícito y variable de sesión.
    //      En MySQL el OUT vuelve a una @variable de sesión, que después
    //      leemos con un SELECT. (Requiere AllowUserVariables en la conexión.)
    // =================================================================
    static int RegistrarPedidoConCall(int clienteId, decimal total)
    {
        // AllowUserVariables=true habilita el uso de @variables de sesión (@id_out).
        using var conn = new MySqlConnection(ConnectionString + ";AllowUserVariables=true");
        conn.Open();

        using (var call = conn.CreateCommand())
        {
            call.CommandText = "CALL sp_registrar_pedido(@cliente, @total, @id_out);";
            call.Parameters.AddWithValue("@cliente", clienteId);
            call.Parameters.AddWithValue("@total", total);
            call.ExecuteNonQuery();
        }

        // Leemos la variable de sesión donde la procedure dejó el id.
        using var leer = conn.CreateCommand();
        leer.CommandText = "SELECT @id_out;";
        return Convert.ToInt32(leer.ExecuteScalar());
    }

    // =================================================================
    //  (3) PROCEDURE que devuelve filas: se invoca con CALL y se lee con
    //      un DataReader (en MySQL esto reemplaza a la TVF de otros motores).
    // =================================================================
    static void PedidosDeCliente(int clienteId)
    {
        using var conn = new MySqlConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "CALL sp_pedidos_de_cliente(@id);";
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
    //  (4) FUNCTION ESCALAR (devuelve un único valor).
    // =================================================================
    static decimal TotalGastado(int clienteId)
    {
        using var conn = new MySqlConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT fn_total_gastado(@id);";
        cmd.Parameters.AddWithValue("@id", clienteId);
        return Convert.ToDecimal(cmd.ExecuteScalar());
    }
}