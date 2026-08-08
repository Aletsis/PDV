namespace PDV.WebUI.Services;

/// <summary>
/// Servicio Scoped que mantiene en memoria el turno activo del cajero
/// durante toda la sesión de conexión Blazor Server.
/// </summary>
public class ShiftSessionService
{
    public Guid? ActiveShiftId { get; private set; }
    public Guid? ActiveCashRegisterId { get; private set; }
    public Guid? BranchId { get; private set; }
    public string? CashRegisterName { get; private set; }
    public decimal InitialCash { get; private set; }
    public DateTime? ShiftStartTime { get; private set; }

    public bool HasActiveShift => ActiveShiftId.HasValue;

    public void SetActive(Guid shiftId, Guid cashRegisterId, string cashRegisterName, decimal initialCash, DateTime startTime, Guid? branchId = null)
    {
        ActiveShiftId = shiftId;
        ActiveCashRegisterId = cashRegisterId;
        CashRegisterName = cashRegisterName;
        InitialCash = initialCash;
        ShiftStartTime = startTime;
        BranchId = branchId;
    }

    public void Clear()
    {
        ActiveShiftId = null;
        ActiveCashRegisterId = null;
        BranchId = null;
        CashRegisterName = null;
        InitialCash = 0;
        ShiftStartTime = null;
    }
}
