using System;

namespace Modulo0.AdoNet.Dominio;

public class Categoria { 
public int Id { get; set; } 
public string? Nombre { get; set; } }


public class Cliente { 
public int Id { get; set; } 
public string? Nombre { get; set; } 
public string? Email { get; set; } }


public class Producto { 
public int Id { get; set; } 
public string? Nombre { get; set; } 
public decimal Precio { get; set; } 
public int Stock { get; set; } 
public int CategoriaId { get; set; } }


public class Pedido { 
public int Id { get; set; } 
public int ClienteId { get; set; } 
public DateTime Fecha { get; set; } }


public class DetallePedido { 
public int PedidoId { get; set; } 
public int ProductoId { get; set; } 
public int Cantidad { get; set; } 
public decimal PrecioUnitario { get; set; } }

// Una clase cortita para usar en el menú más adelante


public class ReporteProducto { 
public string? Producto { get; set; } 
public string? Categoría { get; set; } 
public decimal Precio { get; set; } 
public int Stock { get; set; } 
}