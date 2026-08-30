using System;
using System.Collections.Generic;
using PDV.Domain.Common;
using PDV.Domain.Enums;
using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

public class DeliveryRoute : BaseEntity, IAggregateRoot
{
    private readonly List<Order> _orders = new();

    public int Folio { get; private set; }
    public Guid BranchId { get; private set; }
    public Branch? Branch { get; private set; }
    public Guid? DeliveryZoneId { get; private set; }
    public DeliveryZone? DeliveryZone { get; private set; }
    public string? DeliveryManId { get; private set; }
    public DateTime CreatedDate { get; private set; }
    public DateTime? DispatchedDate { get; private set; }
    public DateTime? SettledDate { get; private set; }
    public DeliveryRouteStatus Status { get; private set; }

    public IReadOnlyCollection<Order> Orders => _orders.AsReadOnly();

    #pragma warning disable CS8618
    private DeliveryRoute() { } // For EF Core
    #pragma warning restore CS8618

    public DeliveryRoute(Guid branchId, Guid? deliveryZoneId, string? deliveryManId, int folio)
    {
        if (branchId == Guid.Empty) throw new DomainException("El ID de sucursal es requerido.");
        if (folio <= 0) throw new DomainException("El folio de la ruta debe ser mayor a cero.");

        BranchId = branchId;
        DeliveryZoneId = deliveryZoneId;
        DeliveryManId = deliveryManId;
        Folio = folio;
        CreatedDate = DateTime.Now;
        Status = DeliveryRouteStatus.Created;
    }

    public void AssignDeliveryMan(string deliveryManId)
    {
        if (string.IsNullOrWhiteSpace(deliveryManId)) throw new DomainException("El ID del repartidor es requerido.");
        if (Status != DeliveryRouteStatus.Created) throw new DomainException("Solo se puede asignar repartidor a una ruta en estado Creada.");

        DeliveryManId = deliveryManId;
    }

    public void AddOrder(Order order)
    {
        if (order == null) throw new DomainException("El pedido no puede ser nulo.");
        if (Status != DeliveryRouteStatus.Created) throw new DomainException("No se pueden agregar pedidos a una ruta que ya está en camino o liquidada.");
        if (order.Status != OrderStatus.Confirmed && order.Status != OrderStatus.Routed)
            throw new DomainException("Solo se pueden agregar pedidos confirmados a la ruta.");
        
        _orders.Add(order);
        order.AssignRoute(Id, CreatedBy ?? "system");
    }

    public void Dispatch(string? deliveryManId = null)
    {
        if (Status != DeliveryRouteStatus.Created) throw new DomainException("La ruta debe estar en estado Creada para poder despacharse.");
        if (_orders.Count == 0) throw new DomainException("No se puede despachar una ruta sin pedidos.");

        if (!string.IsNullOrWhiteSpace(deliveryManId))
        {
            DeliveryManId = deliveryManId;
        }

        if (string.IsNullOrWhiteSpace(DeliveryManId))
            throw new DomainException("Se requiere un repartidor asignado para despachar la ruta.");

        Status = DeliveryRouteStatus.EnRoute;
        DispatchedDate = DateTime.Now;

        foreach (var order in _orders)
        {
            order.AssignDeliveryMan(DeliveryManId);
        }
    }

    public void Settle()
    {
        if (Status != DeliveryRouteStatus.EnRoute) throw new DomainException("Solo se puede liquidar una ruta que se encuentra en camino.");
        
        Status = DeliveryRouteStatus.Settled;
        SettledDate = DateTime.Now;
    }
}

