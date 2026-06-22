using System;
using Npgsql;
using MySqlConnector;
using Microsoft.Data.SqlClient;

namespace Modulo0.AdoNet;

// =================================================================
// 1. INTERFAZ Y ENTIDADES (Todo junto para evitar errores de carpetas)
// =================================================================
public interface IAccesoDatos
{
    void CrearEstructura();
    void InsertarDatosPrueba();
}

public class Categoria { public int Id { get; set; } public string? Nombre { get; set; } }
public class Cliente { public int Id { get; set; } public string? Nombre { get; set; } public string? Email { get; set; } }
public class Producto { public int Id { get; set; } public string? Nombre { get; set; } public decimal Precio { get; set; } public int Stock { get; set; } public int CategoriaId { get; set; } }
public class Pedido { public int Id { get; set; } public int ClienteId { get; set; } public DateTime Fecha { get; set; } }
public class DetallePedido { public int PedidoId { get; set; } public int ProductoId { get; set; } public int Cantidad { get; set; } public decimal PrecioUnitario { get; set; } }

// =================================================================
// 2. MOTOR POSTGRESQL
// =================================================================
public class AccesoPostgres : IAccesoDatos
{
    private const string ConnectionString = "Host=localhost;Port=5432;Database=practico;Username=postgres;Password=postgres;";

    public void CrearEstructura()
    {
        string adminConnection = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres;";
        using (var connAdmin = new NpgsqlConnection(adminConnection))
        {
            connAdmin.Open();
            using (var cmdCreate = connAdmin.CreateCommand())
            {
                cmdCreate.CommandText = "SELECT 1 FROM pg_database WHERE datname = 'practico';";
                var existe = cmdCreate.ExecuteScalar();
                if (existe == null)
                {
                    cmdCreate.CommandText = "CREATE DATABASE practico;";
                    cmdCreate.ExecuteNonQuery();
                }
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

    public void InsertarDatosPrueba()
    {
        using (var conn = new NpgsqlConnection(ConnectionString))
        {
            conn.Open();
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    var cmdCat = conn.CreateCommand(); cmdCat.Transaction = tx;
                    cmdCat.CommandText = "INSERT INTO categorias (nombre) VALUES (@nombre) RETURNING id;";
                    cmdCat.Parameters.AddWithValue("@nombre", "Electrónica");
                    int catElectronicaId = Convert.ToInt32(cmdCat.ExecuteScalar());
                    cmdCat.Parameters["@nombre"].Value = "Libros";
                    int catLibrosId = Convert.ToInt32(cmdCat.ExecuteScalar());

                    var cmdProd = conn.CreateCommand(); cmdProd.Transaction = tx;
                    cmdProd.CommandText = "INSERT INTO productos (nombre, precio, stock, categoria_id) VALUES (@nombre, @precio, @stock, @catId) RETURNING id;";
                    cmdProd.Parameters.AddWithValue("@nombre", "Notebook 14\""); cmdProd.Parameters.AddWithValue("@precio", 850000.00m); cmdProd.Parameters.AddWithValue("@stock", 10); cmdProd.Parameters.AddWithValue("@catId", catElectronicaId);
                    int prodNotebookId = Convert.ToInt32(cmdProd.ExecuteScalar());
                    cmdProd.Parameters["@nombre"].Value = "Mouse inalámbrico"; cmdProd.Parameters["@precio"].Value = 12000.00m; cmdProd.Parameters["@stock"].Value = 25;
                    int prodMouseId = Convert.ToInt32(cmdProd.ExecuteScalar());

                    var cmdCli = conn.CreateCommand(); cmdCli.Transaction = tx;
                    cmdCli.CommandText = "INSERT INTO clientes (nombre, email) VALUES (@nombre, @email) RETURNING id;";
                    cmdCli.Parameters.AddWithValue("@nombre", "Ana Gómez"); 
                    cmdCli.Parameters.AddWithValue("@email", "ana@mail.com");
                    int clienteAnaId = Convert.ToInt32(cmdCli.ExecuteScalar());

                    var cmdPed = conn.CreateCommand(); cmdPed.Transaction = tx;
                    cmdPed.CommandText = "INSERT INTO pedidos (cliente_id) VALUES (@clienteId) RETURNING id;";
                    cmdPed.Parameters.AddWithValue("@clienteId", clienteAnaId);
                    int pedidoId = Convert.ToInt32(cmdPed.ExecuteScalar());

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
}

// =================================================================
// 3. MOTOR MYSQL
// =================================================================
public class AccesoMySql : IAccesoDatos
{
    private const string ConnectionString = "Server=localhost;Port=3306;Database=practico;Uid=root;Pwd=root;";

    public void CrearEstructura()
    {
        string adminConnection = "Server=localhost;Port=3306;Uid=root;Pwd=root;";
        using (var connAdmin = new MySqlConnection(adminConnection))
        {
            connAdmin.Open();
            var cmdCreate = connAdmin.CreateCommand();
            cmdCreate.CommandText = "CREATE DATABASE IF NOT EXISTS practico;";
            cmdCreate.ExecuteNonQuery();
        }

        using (var conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                DROP TABLE IF EXISTS detalle_pedido;
                DROP TABLE IF EXISTS pedidos;
                DROP TABLE IF EXISTS productos;
                DROP TABLE IF EXISTS clientes;
                DROP TABLE IF EXISTS categorias;

                CREATE TABLE categorias (id INT AUTO_INCREMENT PRIMARY KEY, nombre VARCHAR(50) NOT NULL UNIQUE);
                CREATE TABLE clientes (id INT AUTO_INCREMENT PRIMARY KEY, nombre VARCHAR(100) NOT NULL, email VARCHAR(150) NOT NULL UNIQUE);
                CREATE TABLE productos (id INT AUTO_INCREMENT PRIMARY KEY, nombre VARCHAR(100) NOT NULL, precio DECIMAL(10,2) NOT NULL CHECK (precio >= 0), stock INT NOT NULL DEFAULT 0, categoria_id INT NOT NULL, FOREIGN KEY (categoria_id) REFERENCES categorias(id));
                CREATE TABLE pedidos (id INT AUTO_INCREMENT PRIMARY KEY, cliente_id INT NOT NULL, fecha DATETIME NOT NULL DEFAULT NOW(), FOREIGN KEY (cliente_id) REFERENCES clientes(id) ON DELETE CASCADE);
                CREATE TABLE detalle_pedido (pedido_id INT NOT NULL, producto_id INT NOT NULL, cantidad INT NOT NULL CHECK (cantidad > 0), precio_unitario DECIMAL(10,2) NOT NULL CHECK (precio_unitario >= 0), PRIMARY KEY (pedido_id, producto_id), FOREIGN KEY (pedido_id) REFERENCES pedidos(id) ON DELETE CASCADE, FOREIGN KEY (producto_id) REFERENCES productos(id));
            ";
            cmd.ExecuteNonQuery();
            Console.WriteLine("Base 'practico' y estructura de 5 tablas creada en MySQL.");
        }
    }

    public void InsertarDatosPrueba()
    {
        using (var conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    var cmdCat = conn.CreateCommand(); cmdCat.Transaction = tx;
                    cmdCat.CommandText = "INSERT INTO categorias (nombre) VALUES (@nombre);";
                    cmdCat.Parameters.AddWithValue("@nombre", "Electrónica"); cmdCat.ExecuteNonQuery();
                    int catElectronicaId = Convert.ToInt32(cmdCat.LastInsertedId);
                    
                    cmdCat.Parameters["@nombre"].Value = "Libros"; cmdCat.ExecuteNonQuery();
                    int catLibrosId = Convert.ToInt32(cmdCat.LastInsertedId);

                    var cmdProd = conn.CreateCommand(); cmdProd.Transaction = tx;
                    cmdProd.CommandText = "INSERT INTO productos (nombre, precio, stock, categoria_id) VALUES (@nombre, @precio, @stock, @catId);";
                    cmdProd.Parameters.AddWithValue("@nombre", "Notebook 14\""); cmdProd.Parameters.AddWithValue("@precio", 850000.00m); cmdProd.Parameters.AddWithValue("@stock", 10); cmdProd.Parameters.AddWithValue("@catId", catElectronicaId);
                    cmdProd.ExecuteNonQuery(); int prodNotebookId = Convert.ToInt32(cmdProd.LastInsertedId);
                    
                    cmdProd.Parameters["@nombre"].Value = "Mouse inalámbrico"; cmdProd.Parameters["@precio"].Value = 12000.00m; cmdProd.Parameters["@stock"].Value = 25;
                    cmdProd.ExecuteNonQuery(); int prodMouseId = Convert.ToInt32(cmdProd.LastInsertedId);

                    var cmdCli = conn.CreateCommand(); cmdCli.Transaction = tx;
                    cmdCli.CommandText = "INSERT INTO clientes (nombre, email) VALUES (@nombre, @email);";
                    cmdCli.Parameters.AddWithValue("@nombre", "Ana Gómez"); cmdCli.Parameters.AddWithValue("@email", "ana@mail.com");
                    cmdCli.ExecuteNonQuery(); int clienteAnaId = Convert.ToInt32(cmdCli.LastInsertedId);

                    var cmdPed = conn.CreateCommand(); cmdPed.Transaction = tx;
                    cmdPed.CommandText = "INSERT INTO pedidos (cliente_id) VALUES (@clienteId);";
                    cmdPed.Parameters.AddWithValue("@clienteId", clienteAnaId);
                    cmdPed.ExecuteNonQuery(); int pedidoId = Convert.ToInt32(cmdPed.LastInsertedId);

                    var cmdDet = conn.CreateCommand(); cmdDet.Transaction = tx;
                    cmdDet.CommandText = "INSERT INTO detalle_pedido (pedido_id, producto_id, cantidad, precio_unitario) VALUES (@pedidoId, @prodId, @cant, @precio);";
                    cmdDet.Parameters.AddWithValue("@pedidoId", pedidoId); cmdDet.Parameters.AddWithValue("@prodId", prodNotebookId); cmdDet.Parameters.AddWithValue("@cant", 1); cmdDet.Parameters.AddWithValue("@precio", 850000.00m);
                    cmdDet.ExecuteNonQuery();
                    
                    cmdDet.Parameters["@prodId"].Value = prodMouseId; cmdDet.Parameters["@cant"].Value = 2; cmdDet.Parameters["@precio"].Value = 12000.00m;
                    cmdDet.ExecuteNonQuery();

                    tx.Commit();
                    Console.WriteLine("Datos de prueba insertados con éxito en MySQL (Commit OK).");
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    Console.WriteLine($"Error en MySQL (Rollback realizado): {ex.Message}");
                }
            }
        }
    }
}

// =================================================================
// 4. MOTOR SQL SERVER (Con tu contraseña correcta y contenedor correcto)
// =================================================================
public class AccesoSqlServer : IAccesoDatos
{
    private const string ConnectionString = "Server=localhost,1433;Database=practico;User Id=sa;Password=Curso.NET2026;TrustServerCertificate=True;";

    public void CrearEstructura()
    {
        string adminConnection = "Server=localhost,1433;Database=master;User Id=sa;Password=Curso.NET2026;TrustServerCertificate=True;";
        using (var connAdmin = new SqlConnection(adminConnection))
        {
            connAdmin.Open();
            var cmdCreate = connAdmin.CreateCommand();
            cmdCreate.CommandText = "IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'practico') CREATE DATABASE practico;";
            cmdCreate.ExecuteNonQuery();
        }

        using (var conn = new SqlConnection(ConnectionString))
        {
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                IF OBJECT_ID('detalle_pedido', 'U') IS NOT NULL DROP TABLE detalle_pedido;
                IF OBJECT_ID('pedidos', 'U') IS NOT NULL DROP TABLE pedidos;
                IF OBJECT_ID('productos', 'U') IS NOT NULL DROP TABLE productos;
                IF OBJECT_ID('clientes', 'U') IS NOT NULL DROP TABLE clientes;
                IF OBJECT_ID('categorias', 'U') IS NOT NULL DROP TABLE categorias;

                CREATE TABLE categorias (id INT IDENTITY(1,1) PRIMARY KEY, nombre VARCHAR(50) NOT NULL UNIQUE);
                CREATE TABLE clientes (id INT IDENTITY(1,1) PRIMARY KEY, nombre VARCHAR(100) NOT NULL, email VARCHAR(150) NOT NULL UNIQUE);
                CREATE TABLE productos (id INT IDENTITY(1,1) PRIMARY KEY, nombre VARCHAR(100) NOT NULL, precio DECIMAL(10,2) NOT NULL CHECK (precio >= 0), stock INT NOT NULL DEFAULT 0, categoria_id INT NOT NULL REFERENCES categorias(id));
                CREATE TABLE pedidos (id INT IDENTITY(1,1) PRIMARY KEY, cliente_id INT NOT NULL REFERENCES clientes(id) ON DELETE CASCADE, fecha DATETIME NOT NULL DEFAULT GETDATE());
                CREATE TABLE detalle_pedido (pedido_id INT NOT NULL REFERENCES pedidos(id) ON DELETE CASCADE, producto_id INT NOT NULL REFERENCES productos(id), cantidad INT NOT NULL CHECK (cantidad > 0), precio_unitario DECIMAL(10,2) NOT NULL CHECK (precio_unitario >= 0), CONSTRAINT pk_detalle_ms PRIMARY KEY (pedido_id, producto_id));
            ";
            cmd.ExecuteNonQuery();
            Console.WriteLine("Base 'practico' y estructura de 5 tablas creada en SQL Server.");
        }
    }

    public void InsertarDatosPrueba()
    {
        using (var conn = new SqlConnection(ConnectionString))
        {
            conn.Open();
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    var cmdCat = conn.CreateCommand(); cmdCat.Transaction = tx;
                    cmdCat.CommandText = "INSERT INTO categorias (nombre) OUTPUT INSERTED.id VALUES (@nombre);";
                    cmdCat.Parameters.AddWithValue("@nombre", "Electrónica");
                    int catElectronicaId = Convert.ToInt32(cmdCat.ExecuteScalar());

                    var cmdProd = conn.CreateCommand(); cmdProd.Transaction = tx;
                    cmdProd.CommandText = "INSERT INTO productos (nombre, precio, stock, categoria_id) OUTPUT INSERTED.id VALUES (@nombre, @precio, @stock, @catId);";
                    cmdProd.Parameters.AddWithValue("@nombre", "Notebook 14\""); cmdProd.Parameters.AddWithValue("@precio", 850000.00m); cmdProd.Parameters.AddWithValue("@stock", 10); cmdProd.Parameters.AddWithValue("@catId", catElectronicaId);
                    int prodNotebookId = Convert.ToInt32(cmdProd.ExecuteScalar());
                    cmdProd.Parameters["@nombre"].Value = "Mouse inalámbrico"; cmdProd.Parameters["@precio"].Value = 12000.00m; cmdProd.Parameters["@stock"].Value = 25;
                    int prodMouseId = Convert.ToInt32(cmdProd.ExecuteScalar());

                    var cmdCli = conn.CreateCommand(); cmdCli.Transaction = tx;
                    cmdCli.CommandText = "INSERT INTO clientes (nombre, email) OUTPUT INSERTED.id VALUES (@nombre, @email);";
                    cmdCli.Parameters.AddWithValue("@nombre", "Ana Gómez"); cmdCli.Parameters.AddWithValue("@email", "ana@mail.com");
                    int clienteAnaId = Convert.ToInt32(cmdCli.ExecuteScalar());

                    var cmdPed = conn.CreateCommand(); cmdPed.Transaction = tx;
                    cmdPed.CommandText = "INSERT INTO pedidos (cliente_id) OUTPUT INSERTED.id VALUES (@clienteId);";
                    cmdPed.Parameters.AddWithValue("@clienteId", clienteAnaId);
                    int pedidoId = Convert.ToInt32(cmdPed.ExecuteScalar());

                    var cmdDet = conn.CreateCommand(); cmdDet.Transaction = tx;
                    cmdDet.CommandText = "INSERT INTO detalle_pedido (pedido_id, producto_id, cantidad, precio_unitario) VALUES (@pedidoId, @prodId, @cant, @precio);";
                    cmdDet.Parameters.AddWithValue("@pedidoId", pedidoId); cmdDet.Parameters.AddWithValue("@prodId", prodNotebookId); cmdDet.Parameters.AddWithValue("@cant", 1); cmdDet.Parameters.AddWithValue("@precio", 850000.00m);
                    cmdDet.ExecuteNonQuery();
                    cmdDet.Parameters["@prodId"].Value = prodMouseId; cmdDet.Parameters["@cant"].Value = 2; cmdDet.Parameters["@precio"].Value = 12000.00m;
                    cmdDet.ExecuteNonQuery();

                    tx.Commit();
                    Console.WriteLine("Datos de prueba insertados con éxito en SQL Server (Commit OK).");
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    Console.WriteLine($"Error en SQL Server (Rollback realizado): {ex.Message}");
                }
            }
        }
    }
}

// =================================================================
// 5. EJECUCIÓN PRINCIPAL
// =================================================================
class Program
{
    static void MainViejo()
    {
        // ---------------------------------------------------------
        
        // Opcion 1: new AccesoPostgres()
        // Opcion 2: new AccesoMySql()
        // Opcion 3: new AccesoSqlServer()
        // ---------------------------------------------------------
        Console.WriteLine("=== PROBANDO EL PRÁCTICO INTEGRADO ===");

        IAccesoDatos acceso = new AccesoSqlServer(); // <-- Acá pusimos tu SQL Server que usa 'sqlserver-lab'

        Console.WriteLine("\n[Ejecutando] Tarea 1: CrearEstructura()...");
        acceso.CrearEstructura(); 

        Console.WriteLine("\n[Ejecutando] Tarea 2: InsertarDatosPrueba()...");
        acceso.InsertarDatosPrueba(); 

        Console.WriteLine("\n=== FIN DE LA PRUEBA ===");
    }
}