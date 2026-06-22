// =====================================================================
//  MÓDULO 0 (Lab 2) — STORED PROCEDURES con ADO.NET sobre PostgreSQL
// =====================================================================
//
//  Seguimos en ADO.NET CRUDO (sin EF). La novedad respecto al Lab 1:
//    1) Usamos un motor relacional real: PostgreSQL (vía Npgsql).
//       SQLite NO soporta procedimientos almacenados; por eso cambiamos.
//    2) Trabajamos con un MODELO RELACIONAL: dos tablas relacionadas por
//       una clave foránea (clientes 1 --- N pedidos).
//    3) Aprendemos a EJECUTAR lógica que vive DENTRO de la base:
//
//  PostgreSQL distingue dos tipos de "programas almacenados":
//    - FUNCTION  : devuelve valores; se invoca dentro de un SELECT.
//                  (puede devolver un escalar o una tabla)
//    - PROCEDURE : se invoca con CALL; puede tener parámetros de SALIDA
//                  (INOUT) y manejar transacciones. (PostgreSQL 11+)
//  (En SQL Server a ambos se les dice "stored procedure".)
//
//  Objetos ADO.NET que reaparecen: NpgsqlConnection, NpgsqlCommand,
//  NpgsqlParameter (¡ahora también de SALIDA!), NpgsqlDataReader.
// =====================================================================

using Npgsql;
using NpgsqlTypes;
using System.Data;

namespace Modulo0.ProcedimientosSqlServer;

class ProgramPostgres
{
    // Apunta al PostgreSQL del docker (ver README). Cambiá si usás otro host.
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=tienda;Username=postgres;Password=postgres";

    // =================================================================
    //  MAIN — orquesta toda la demo de extremo a extremo.
    // =================================================================
    
    static void MainPostgres()
    {
        CrearEsquemaRelacional();      // tablas + datos de clientes
        CrearProgramasAlmacenados();   // la función y el procedimiento en la BD

        int anaId  = BuscarClienteId("ana@mail.com");
        int betoId = BuscarClienteId("beto@mail.com");

        // --- 1) PROCEDURE con parámetro de SALIDA (CALL + INOUT) ---
        Console.WriteLine("== CALL sp_registrar_pedido (PROCEDURE con salida) ==");
        int p1 = RegistrarPedido(anaId, 1500.00m);
        Console.WriteLine($"  Nuevo pedido de Ana  -> id {p1}");
        int p2 = RegistrarPedido(anaId, 250.50m);
        Console.WriteLine($"  Nuevo pedido de Ana  -> id {p2}");
        int p3 = RegistrarPedido(betoId, 999.99m);
        Console.WriteLine($"  Nuevo pedido de Beto -> id {p3}");

        // --- 2) La MISMA procedure, pero con CommandType.StoredProcedure ---
        //     (la forma "clásica" de ADO.NET, sin escribir el CALL a mano)
        Console.WriteLine("\n== StoredProcedure (CommandType) ==");
        int p4 = RegistrarPedidoClasico(anaId, 75.00m);
        Console.WriteLine($"  Nuevo pedido de Ana  -> id {p4}");

        // --- 3) FUNCTION que devuelve una TABLA (SELECT * FROM fn(...)) ---
        Console.WriteLine("\n== fn_pedidos_de_cliente (FUNCTION que devuelve filas) ==");
        Console.WriteLine("  Pedidos de Ana:");
        PedidosDeCliente(anaId);

        // --- 4) FUNCTION ESCALAR (SELECT fn(...)) con ExecuteScalar ---
        Console.WriteLine("\n== fn_total_gastado (FUNCTION escalar) ==");
        Console.WriteLine($"  Total gastado por Ana : ${TotalGastado(anaId)}");
        Console.WriteLine($"  Total gastado por Beto: ${TotalGastado(betoId)}");
    }

    // =================================================================
    //  Preparación: modelo relacional (clientes 1 --- N pedidos)
    // =================================================================
    static void CrearEsquemaRelacional()
    {
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        // Una clave foránea (cliente_id -> clientes.id) define la relación 1-N.
        cmd.CommandText = """
            DROP TABLE IF EXISTS pedidos;
            DROP TABLE IF EXISTS clientes;

            CREATE TABLE clientes (
                id     SERIAL PRIMARY KEY,
                nombre TEXT NOT NULL,
                email  TEXT NOT NULL UNIQUE
            );

            CREATE TABLE pedidos (
                id         SERIAL PRIMARY KEY,
                cliente_id INT NOT NULL REFERENCES clientes(id),  -- FK: relación 1-N
                fecha      TIMESTAMPTZ NOT NULL DEFAULT now(),
                total      NUMERIC(10,2) NOT NULL
            );

            INSERT INTO clientes (nombre, email) VALUES
                ('Ana',  'ana@mail.com'),
                ('Beto', 'beto@mail.com');
            """;
        cmd.ExecuteNonQuery();
        Console.WriteLine("✅ Esquema relacional creado (clientes 1-N pedidos) + clientes cargados.\n");
    }

    // =================================================================
    //  Preparación: crear la FUNCTION y la PROCEDURE dentro de la BD.
    //  En un proyecto real esto suele vivir en scripts .sql / migraciones;
    //  acá lo hacemos desde el programa para que el lab sea autocontenido.
    // =================================================================
    static void CrearProgramasAlmacenados()
    {
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            -- PROCEDURE: registra un pedido y DEVUELVE el id generado por INOUT.
            CREATE OR REPLACE PROCEDURE sp_registrar_pedido(
                p_cliente_id INT,
                p_total      NUMERIC,
                INOUT p_nuevo_id INT DEFAULT NULL
            )
            LANGUAGE plpgsql
            AS $$
            BEGIN
                INSERT INTO pedidos (cliente_id, total)
                VALUES (p_cliente_id, p_total)
                RETURNING id INTO p_nuevo_id;   -- el id autogenerado vuelve por la salida
            END;
            $$;

            -- FUNCTION que devuelve una TABLA: los pedidos de un cliente.
            CREATE OR REPLACE FUNCTION fn_pedidos_de_cliente(p_cliente_id INT)
            RETURNS TABLE(pedido_id INT, fecha TIMESTAMPTZ, total NUMERIC)
            LANGUAGE sql
            AS $$
                SELECT id, fecha, total
                FROM pedidos
                WHERE cliente_id = p_cliente_id
                ORDER BY id;
            $$;

            -- FUNCTION ESCALAR: total gastado por un cliente.
            CREATE OR REPLACE FUNCTION fn_total_gastado(p_cliente_id INT)
            RETURNS NUMERIC
            LANGUAGE sql
            AS $$
                SELECT COALESCE(SUM(total), 0)
                FROM pedidos
                WHERE cliente_id = p_cliente_id;
            $$;
            """;
        cmd.ExecuteNonQuery();
        Console.WriteLine("✅ Procedimiento y funciones creados en la base.\n");
    }

    // =================================================================
    //  Helper: obtener el id de un cliente por email (READ común).
    // =================================================================
    static int BuscarClienteId(string email)
    {
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();

        using var cmd = new NpgsqlCommand("SELECT id FROM clientes WHERE email = @email", conn);
        cmd.Parameters.AddWithValue("email", email);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    // =================================================================
    //  (1) Ejecutar una PROCEDURE con CALL y leer su parámetro de SALIDA.
    // =================================================================
    static int RegistrarPedido(int clienteId, decimal total)
    {
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();

        // Escribimos el CALL a mano. El tercer argumento (@nuevo_id) es el
        // parámetro de SALIDA: lo declaramos como InputOutput y, tras ejecutar,
        // leemos su .Value con el id que generó la base.
        using var cmd = new NpgsqlCommand(
            "CALL sp_registrar_pedido(@cliente, @total, @nuevo_id)", conn);
        cmd.Parameters.AddWithValue("cliente", clienteId);
        cmd.Parameters.AddWithValue("total", total);

        var salida = new NpgsqlParameter("nuevo_id", NpgsqlDbType.Integer)
        {
            Direction = ParameterDirection.InputOutput,  // <- parámetro de salida
            Value = DBNull.Value
        };
        cmd.Parameters.Add(salida);

        cmd.ExecuteNonQuery();
        return Convert.ToInt32(salida.Value);   // el id vuelve por acá
    }

    // =================================================================
    //  (2) La MISMA procedure, pero al estilo "clásico" de ADO.NET:
    //      CommandType.StoredProcedure + solo el nombre (sin escribir CALL).
    //      Npgsql arma el CALL por nosotros a partir de los parámetros.
    // =================================================================
    static int RegistrarPedidoClasico(int clienteId, decimal total)
    {
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();

        using var cmd = new NpgsqlCommand("sp_registrar_pedido", conn)
        {
            CommandType = CommandType.StoredProcedure   // <- la clave de este enfoque
        };
        cmd.Parameters.AddWithValue("p_cliente_id", clienteId);
        cmd.Parameters.AddWithValue("p_total", total);

        var salida = new NpgsqlParameter("p_nuevo_id", NpgsqlDbType.Integer)
        {
            Direction = ParameterDirection.InputOutput,
            Value = DBNull.Value
        };
        cmd.Parameters.Add(salida);

        cmd.ExecuteNonQuery();
        return Convert.ToInt32(salida.Value);
    }

    // =================================================================
    //  (3) Ejecutar una FUNCTION que devuelve una TABLA.
    //      Se llama dentro de un SELECT y se lee con un DataReader.
    // =================================================================
    static void PedidosDeCliente(int clienteId)
    {
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();

        using var cmd = new NpgsqlCommand(
            "SELECT pedido_id, fecha, total FROM fn_pedidos_de_cliente(@id)", conn);
        cmd.Parameters.AddWithValue("id", clienteId);

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
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();

        using var cmd = new NpgsqlCommand("SELECT fn_total_gastado(@id)", conn);
        cmd.Parameters.AddWithValue("id", clienteId);
        return Convert.ToDecimal(cmd.ExecuteScalar());
    }
}
