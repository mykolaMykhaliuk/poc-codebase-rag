using SampleApp.Models;

namespace SampleApp.Services;

/// <summary>
/// Service for managing product catalog operations.
/// </summary>
public class ProductService
{
    private readonly List<Product> _products = new();
    private readonly ILogger<ProductService> _logger;

    public ProductService(ILogger<ProductService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Adds a new product to the catalog.
    /// </summary>
    public Product AddProduct(string name, string? description, decimal price, string category, int stockQuantity)
    {
        if (price < 0)
        {
            throw new ArgumentException("Price cannot be negative", nameof(price));
        }

        if (stockQuantity < 0)
        {
            throw new ArgumentException("Stock quantity cannot be negative", nameof(stockQuantity));
        }

        var product = new Product
        {
            Id = _products.Count + 1,
            Name = name,
            Description = description,
            Price = price,
            Category = category,
            StockQuantity = stockQuantity
        };

        _products.Add(product);
        _logger.LogInformation("Added new product: {ProductName} (ID: {ProductId})", name, product.Id);
        return product;
    }

    /// <summary>
    /// Gets a product by its ID.
    /// </summary>
    public Product? GetById(int id)
    {
        return _products.FirstOrDefault(p => p.Id == id);
    }

    /// <summary>
    /// Gets all available products.
    /// </summary>
    public IEnumerable<Product> GetAvailable()
    {
        return _products.Where(p => p.IsAvailable);
    }

    /// <summary>
    /// Searches products by name or description.
    /// </summary>
    public IEnumerable<Product> Search(string query)
    {
        var lowerQuery = query.ToLowerInvariant();
        return _products.Where(p =>
            p.Name.ToLowerInvariant().Contains(lowerQuery) ||
            (p.Description?.ToLowerInvariant().Contains(lowerQuery) ?? false));
    }

    /// <summary>
    /// Gets products by category.
    /// </summary>
    public IEnumerable<Product> GetByCategory(string category)
    {
        return _products.Where(p =>
            p.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Updates the stock quantity for a product.
    /// </summary>
    public void UpdateStock(int productId, int quantityChange)
    {
        var product = GetById(productId);
        if (product == null)
        {
            throw new InvalidOperationException($"Product with ID {productId} not found");
        }

        var newQuantity = product.StockQuantity + quantityChange;
        if (newQuantity < 0)
        {
            throw new InvalidOperationException($"Insufficient stock. Available: {product.StockQuantity}");
        }

        product.StockQuantity = newQuantity;
        product.UpdatedAt = DateTime.UtcNow;
        _logger.LogInformation("Updated stock for product {ProductId}: {OldQuantity} -> {NewQuantity}",
            productId, product.StockQuantity - quantityChange, newQuantity);
    }

    /// <summary>
    /// Updates the price of a product.
    /// </summary>
    public void UpdatePrice(int productId, decimal newPrice)
    {
        if (newPrice < 0)
        {
            throw new ArgumentException("Price cannot be negative", nameof(newPrice));
        }

        var product = GetById(productId);
        if (product == null)
        {
            throw new InvalidOperationException($"Product with ID {productId} not found");
        }

        var oldPrice = product.Price;
        product.Price = newPrice;
        product.UpdatedAt = DateTime.UtcNow;
        _logger.LogInformation("Updated price for product {ProductId}: {OldPrice} -> {NewPrice}",
            productId, oldPrice, newPrice);
    }
}
