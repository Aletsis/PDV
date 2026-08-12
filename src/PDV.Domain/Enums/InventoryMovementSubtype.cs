namespace PDV.Domain.Enums;

/// <summary>
/// Subclasificación de movimientos de inventario.
/// Permite mapear cada tipo de operación a un Concepto específico en CONTPAQi Comercial.
/// </summary>
public enum InventoryMovementSubtype
{
    // Subclasificaciones de Compras
    PurchaseGroceries = 101,      // Abarrotes
    PurchasePettyCash = 102,      // Caja chica
    PurchaseStandard = 103,       // Compras
    PurchaseFixedExpenses = 104,  // Gastos fijos
    PurchaseSuppliers = 105,      // Proveedores

    // Subclasificaciones de Traspaso entre sucursales
    TransferGroceries = 201,      // Abarrotes
    TransferWarehouse = 202,      // Almacén
    TransferSupplies = 203,       // Insumos

    // Ajustes y otros
    AdjustmentInputGeneral = 301, // Ajuste de entrada general
    AdjustmentOutputGeneral = 401,// Ajuste de salida / merma general
    InitialInventory = 501        // Inventario inicial
}
