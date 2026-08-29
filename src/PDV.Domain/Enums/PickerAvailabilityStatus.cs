namespace PDV.Domain.Enums;

public enum PickerAvailabilityStatus
{
    Available = 0,        // Disponible para recibir y surtir pedidos
    Busy = 1,             // En surtido activo (calculado o asignado)
    MealBreak = 2,        // En hora de comida
    OperationalBreak = 3, // Pausa operativa (baño, almacén, descanso breve)
    OffDuty = 4           // Fuera de turno / Descanso / Ausente
}
