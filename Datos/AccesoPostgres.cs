using System;
using Npgsql;
using Modulo0.AdoNet.Dominio;
using Modulo0.AdoNet.Datos;

namespace Modulo0.AdoNet.Datos;

public class AccesoPostgres : IAccesoDatos
{
    private const string ConnectionString = "Host=localhost;Port=5432;Database=practico;Username=postgres;Password=postgres;";

    // =================================================================
    // TAREA 1: Crear Base de Datos y las 5 tablas exigidas
    // =================================================================
    public void CrearEstructura()
    {
        // Nos conectamos directo a Postgres de forma limpia sin comandos raros de C#
        string adminConnection = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres;";
        using (var connAdmin = new NpgsqlConnection(adminConnection))
        {
            connAdmin.Open();
            var cmdCreate = connAdmin.CreateCommand();
            // Si la base no existe, la crea. Si existe, no hace nada. ¡Simple!
            cmdCreate.CommandText = "SELECT 1 FROM pg_database WHERE datname = 'practico';";
            var existe = cmdCreate.ExecuteScalar();
            
            if (existe == null)
            {
                cmdCreate.CommandText = "CREATE DATABASE practico;";
                cmdCreate.ExecuteNonQuery();
            }
        }

        using (var conn = new NpgsqlConnection(ConnectionString))
        {
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                DROP TABLE IF EXISTS detalle_pedido CASCADE;
                DROP TABLE IF EXISTS pedidos CASCADE;
                DROP TABLE IF EXISTS productos CASCADE;
                DROP TABLE IF EXISTS clientes CASCADE;
                DROP TABLE IF EXISTS categorias CASCADE;

                CREATE TABLE categorias (id SERIAL PRIMARY KEY, nombre VARCHAR(50) NOT NULL UNIQUE);
                CREATE TABLE clientes (id SERIAL PRIMARY KEY, nombre VARCHAR(100) NOT NULL, email VARCHAR(150) NOT NULL UNIQUE);
                CREATE TABLE productos (id SERIAL PRIMARY KEY, nombre VARCHAR(100) NOT NULL, precio NUMERIC(10,2) NOT NULL CHECK (precio >= 0), stock INTEGER NOT NULL DEFAULT 0, categoria_id INTEGER NOT NULL REFERENCES categorias(id));
                CREATE TABLE pedidos (id SERIAL PRIMARY KEY, cliente_id INTEGER NOT NULL REFERENCES clientes(id) ON DELETE CASCADE, fecha TIMESTAMP NOT NULL DEFAULT NOW());
                CREATE TABLE detalle_pedido (pedido_id INTEGER NOT NULL REFERENCES pedidos(id) ON DELETE CASCADE, producto_id INTEGER NOT NULL REFERENCES productos(id), cantidad INTEGER NOT NULL CHECK (cantidad > 0), precio_unitario NUMERIC(10,2) NOT NULL CHECK (precio_unitario >= 0), CONSTRAINT pk_detalle_pg PRIMARY KEY (pedido_id, producto_id));
            ";
            cmd.ExecuteNonQuery();
            Console.WriteLine("Base 'practico' y estructura de 5 tablas creada en PostgreSQL.");
        }
    }

    // =================================================================
    // TAREA 2: Insertar datos usando transacciones
    // =================================================================
    public void InsertarDatosPrueba()
    {
        using (var conn = new NpgsqlConnection(ConnectionString))
        {
            conn.Open();
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    // 1. Insertar Categorías
                    var cmdCat = conn.CreateCommand(); cmdCat.Transaction = tx;
                    cmdCat.CommandText = "INSERT INTO categorias (nombre) VALUES (@nombre) RETURNING id;";
                    cmdCat.Parameters.AddWithValue("@nombre", "Electrónica");
                    var cat1 = cmdCat.ExecuteScalar(); int catElectronicaId = Convert.ToInt32(cat1);
                    cmdCat.Parameters["@nombre"].Value = "Libros";
                    var cat2 = cmdCat.ExecuteScalar(); int catLibrosId = Convert.ToInt32(cat2);

                    // 2. Insertar Productos
                    var cmdProd = conn.CreateCommand(); cmdProd.Transaction = tx;
                    cmdProd.CommandText = "INSERT INTO productos (nombre, precio, stock, categoria_id) VALUES (@nombre, @precio, @stock, @catId) RETURNING id;";
                    cmdProd.Parameters.AddWithValue("@nombre", "Notebook 14\""); cmdProd.Parameters.AddWithValue("@precio", 850000.00m); cmdProd.Parameters.AddWithValue("@stock", 10); cmdProd.Parameters.AddWithValue("@catId", catElectronicaId);
                    var p1 = cmdProd.ExecuteScalar(); int prodNotebookId = Convert.ToInt32(p1);
                    cmdProd.Parameters["@nombre"].Value = "Mouse inalámbrico"; cmdProd.Parameters["@precio"].Value = 12000.00m; cmdProd.Parameters["@stock"].Value = 25;
                    var p2 = cmdProd.ExecuteScalar(); int prodMouseId = Convert.ToInt32(p2);

                    // 3. Insertar Clientes
                    var cmdCli = conn.CreateCommand(); cmdCli.Transaction = tx;
                    cmdCli.CommandText = "INSERT INTO clientes (nombre, email) VALUES (@nombre, @email) RETURNING id;";
                    cmdCli.Parameters.AddWithValue("@nombre", "Ana Gómez"); 
                    cmdCli.Parameters.AddWithValue("@email", "ana@mail.com");
                    var cl = cmdCli.ExecuteScalar(); int clienteAnaId = Convert.ToInt32(cl);

                    // 4. Insertar Pedido
                    var cmdPed = conn.CreateCommand(); cmdPed.Transaction = tx;
                    cmdPed.CommandText = "INSERT INTO pedidos (cliente_id) VALUES (@clienteId) RETURNING id;";
                    cmdPed.Parameters.AddWithValue("@clienteId", clienteAnaId);
                    var ped = cmdPed.ExecuteScalar(); int pedidoId = Convert.ToInt32(ped);

                    // 5. Insertar Detalles
                    var cmdDet = conn.CreateCommand(); cmdDet.Transaction = tx;
                    cmdDet.CommandText = "INSERT INTO detalle_pedido (pedido_id, producto_id, cantidad, precio_unitario) VALUES (@pedidoId, @prodId, @cant, @precio);";
                    cmdDet.Parameters.AddWithValue("@pedidoId", pedidoId); cmdDet.Parameters.AddWithValue("@prodId", prodNotebookId); cmdDet.Parameters.AddWithValue("@cant", 1); cmdDet.Parameters.AddWithValue("@precio", 850000.00m);
                    cmdDet.ExecuteNonQuery();
                    cmdDet.Parameters["@prodId"].Value = prodMouseId; cmdDet.Parameters["@cant"].Value = 2; cmdDet.Parameters["@precio"].Value = 12000.00m;
                    cmdDet.ExecuteNonQuery();

                    tx.Commit();
                    Console.WriteLine("Datos de prueba insertados con éxito en PostgreSQL (Commit OK).");
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    Console.WriteLine($"Error en Postgres (Rollback realizado): {ex.Message}");
                }
            }
        }
    }

    public void EjecutarOperaciones() { }
    public void DemostrarRollback() { }
}