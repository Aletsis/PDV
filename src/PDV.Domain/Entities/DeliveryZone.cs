using System;
using PDV.Domain.Common;

namespace PDV.Domain.Entities;

public class DeliveryZone : BaseEntity, IAggregateRoot
{
    public string Name { get; private set; } = null!;
    public Guid BranchId { get; private set; }
    public Branch? Branch { get; private set; }
    public string PolygonCoordinatesJson { get; private set; } = null!;
    public decimal DeliveryCost { get; private set; }
    public bool IsActive { get; private set; }

    private DeliveryZone() { } // EF Core

    public DeliveryZone(string name, Guid branchId, string polygonCoordinatesJson, decimal deliveryCost = 0)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("El nombre de la zona es requerido.");
        if (branchId == Guid.Empty) throw new ArgumentException("El ID de sucursal es requerido.");
        if (string.IsNullOrWhiteSpace(polygonCoordinatesJson)) throw new ArgumentException("Las coordenadas del polígono son requeridas.");

        Name = name.Trim();
        BranchId = branchId;
        PolygonCoordinatesJson = polygonCoordinatesJson.Trim();
        DeliveryCost = deliveryCost >= 0 ? deliveryCost : throw new ArgumentException("El costo de entrega no puede ser negativo.");
        IsActive = true;
    }

    public void Update(string name, string polygonCoordinatesJson, decimal deliveryCost)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("El nombre de la zona es requerido.");
        if (string.IsNullOrWhiteSpace(polygonCoordinatesJson)) throw new ArgumentException("Las coordenadas del polígono son requeridas.");

        Name = name.Trim();
        PolygonCoordinatesJson = polygonCoordinatesJson.Trim();
        DeliveryCost = deliveryCost >= 0 ? deliveryCost : throw new ArgumentException("El costo de entrega no puede ser negativo.");
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
