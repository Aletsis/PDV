using PDV.Domain.Entities;
using PDV.Domain.Enums;

namespace PDV.Application.Features.Orders.Dtos;

public class CartItemDto : System.ComponentModel.INotifyPropertyChanged
{
    private int _quantity = 1;
    private decimal _weight = 0;
    private decimal? _priceOverride;
    private string? _notes;
    private bool _isFulfilled;

    public Product Product { get; set; } = null!;
    public Guid SaleItemId { get; set; }
    public Guid OrderItemId { get; set; }
    public decimal RequestedQuantity { get; set; }
    
    public string? Notes
    {
        get => _notes;
        set
        {
            if (_notes != value)
            {
                _notes = value;
                OnPropertyChanged(nameof(Notes));
            }
        }
    }

    public bool IsFulfilled
    {
        get => _isFulfilled;
        set
        {
            if (_isFulfilled != value)
            {
                _isFulfilled = value;
                OnPropertyChanged(nameof(IsFulfilled));
            }
        }
    }

    public int Quantity 
    { 
        get => _quantity; 
        set 
        {
            if (_quantity != value)
            {
                _quantity = value;
                OnPropertyChanged(nameof(Quantity));
                OnPropertyChanged(nameof(QuantityDisplay));
                OnPropertyChanged(nameof(Total));
            }
        } 
    }

    public decimal Weight 
    { 
        get => _weight; 
        set 
        {
            if (_weight != value)
            {
                _weight = value;
                OnPropertyChanged(nameof(Weight));
                OnPropertyChanged(nameof(QuantityDisplay));
                OnPropertyChanged(nameof(Total));
            }
        } 
    }

    public decimal? PriceOverride 
    { 
        get => _priceOverride; 
        set 
        {
            if (_priceOverride != value)
            {
                _priceOverride = value;
                OnPropertyChanged(nameof(PriceOverride));
                OnPropertyChanged(nameof(Total));
            }
        } 
    }

    public decimal QuantityDisplay => Product.SaleType == SaleType.Bulk ? Weight : Quantity;

    public decimal Total => Product.SaleType == SaleType.Bulk 
        ? (PriceOverride ?? Product.Price) * Weight 
        : (PriceOverride ?? Product.Price) * Quantity;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
