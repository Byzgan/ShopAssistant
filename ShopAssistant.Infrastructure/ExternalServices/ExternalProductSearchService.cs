namespace ShopAssistant.Infrastructure.ExternalServices;

using Contracts.Interfaces.Integrations;
using Contracts.Models.Integrations;

/// <summary>
/// Simulates an external service for searching products.
/// In a real implementation, this would call an external API.
/// </summary>
public class ExternalProductSearchService : IProductSearchService
{
    public Task<IReadOnlyList<ProductSearchResult>> SearchProductsAsync(ProductSearchRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        cancellationToken.ThrowIfCancellationRequested();

        var type = (request.ProductType ?? string.Empty).Trim().ToLowerInvariant();
        var brand = (request.Brand ?? string.Empty).Trim().ToLowerInvariant();

        // Map various user inputs to canonical type keys
        var typeKeyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "iphone", "iphone" },
            { "ipad", "ipad" },
            { "macbook", "macbook" },
            { "tws", "tws" },
            { "earbud", "tws" },
            { "playstation", "playstation" },
            { "ps5", "playstation" },
            { "airpods", "airpods" },
            { "surface", "surface" },
            { "kindle", "kindle" },
            { "galaxy tab", "galaxy tab" }
        };

        // Try to determine the main type key (default to raw type if not found)
        string mainTypeKey = typeKeyMap.FirstOrDefault(kvp => type.Contains(kvp.Key)).Value ?? type;

        // If mainTypeKey or brand is empty, fallback to just mainTypeKey
        var fullKey = !string.IsNullOrWhiteSpace(brand) ? $"{mainTypeKey}:{brand}" : mainTypeKey;

        if (DemoProducts.TryGetValue(fullKey, out var found) || DemoProducts.TryGetValue(mainTypeKey, out found))
            return Task.FromResult(found);

        // For Galaxy Tab, also try matching Samsung Tab requests
        if ((type.Contains("galaxy tab") || (brand.Contains("samsung") && type.Contains("tab"))) && DemoProducts.TryGetValue("galaxy tab", out found))
            return Task.FromResult(found);

        // Default fallback: generate some generic product names
        var capitalized = Capitalize(request.ProductType);
        const string urlBase = "https://store.example.com/products/";
        var results = new List<ProductSearchResult>
        {
            new() { Name = $"{capitalized} Pro {(request.Brand)}".Trim(), Price = "$199", Url = urlBase + "pro" },
            new() { Name = $"{capitalized} Plus {(request.Brand)}".Trim(), Price = "$149", Url = urlBase + "plus" },
            new() { Name = $"{capitalized} Basic {(request.Brand)}".Trim(), Price = "$99", Url = urlBase + "basic" }
        };

        return Task.FromResult<IReadOnlyList<ProductSearchResult>>(results);
    }

    private static string Capitalize(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return input ?? string.Empty;
        return char.ToUpper(input[0]) + input[1..];
    }

    // All demo products organized by key (e.g., "tws:sony", "iphone", "macbook")
    private static readonly Dictionary<string, IReadOnlyList<ProductSearchResult>> DemoProducts = new (StringComparer.OrdinalIgnoreCase)
        {
            ["iphone"] =
            [
                new ProductSearchResult { Name = "iPhone 15 Pro Max 256GB", Price = "$1299", Url = "https://store.example.com/products/iphone-15-pro-max" },
                new ProductSearchResult { Name = "iPhone 15 128GB", Price = "$899", Url = "https://store.example.com/products/iphone-15" },
                new ProductSearchResult { Name = "iPhone 14 (Renewed)", Price = "$699", Url = "https://store.example.com/products/iphone-14-renewed" }
            ],
            ["ipad"] =
            [
                new ProductSearchResult { Name = "iPad Pro 12.9-inch (M4) 256GB", Price = "$1099", Url = "https://store.example.com/products/ipad-pro-12-9-m4" },
                new ProductSearchResult { Name = "iPad Air 11-inch (M2) 128GB", Price = "$599", Url = "https://store.example.com/products/ipad-air-11-m2" },
                new ProductSearchResult { Name = "iPad 10th Generation 64GB", Price = "$449", Url = "https://store.example.com/products/ipad-10th-gen" }
            ],
            ["tws:sony"] =
            [
                new ProductSearchResult { Name = "Sony WF-1000XM5 TWS Noise Cancelling", Price = "$278", Url = "https://store.example.com/products/sony-wf-1000xm5" },
                new ProductSearchResult { Name = "Sony LinkBuds S Truly Wireless", Price = "$148", Url = "https://store.example.com/products/sony-linkbuds-s" }
            ],
            ["tws:apple"] =
            [
                new ProductSearchResult { Name = "Apple AirPods Pro (2nd Gen)", Price = "$249", Url = "https://store.example.com/products/airpods-pro-2" },
                new ProductSearchResult { Name = "Apple AirPods (3rd Gen)", Price = "$179", Url = "https://store.example.com/products/airpods-3" }
            ],
            ["tws:jabra"] =
            [
                new ProductSearchResult { Name = "Jabra Elite 7 Active", Price = "$179", Url = "https://store.example.com/products/jabra-elite-7" },
                new ProductSearchResult { Name = "Jabra Elite 4 Active", Price = "$119", Url = "https://store.example.com/products/jabra-elite-4" }
            ],
            ["tws"] =
            [
                new ProductSearchResult { Name = "Sony WF-1000XM5 TWS Noise Cancelling", Price = "$278", Url = "https://store.example.com/products/sony-wf-1000xm5" },
                new ProductSearchResult { Name = "Apple AirPods Pro (2nd Gen)", Price = "$249", Url = "https://store.example.com/products/airpods-pro-2" },
                new ProductSearchResult { Name = "Jabra Elite 7 Active", Price = "$179", Url = "https://store.example.com/products/jabra-elite-7" },
                new ProductSearchResult { Name = "Samsung Galaxy Buds2 Pro", Price = "$229", Url = "https://store.example.com/products/galaxy-buds2-pro" },
                new ProductSearchResult { Name = "Bose QuietComfort Earbuds II", Price = "$299", Url = "https://store.example.com/products/bose-qc-earbuds-2" },
                new ProductSearchResult { Name = "Nothing Ear", Price = "$149", Url = "https://store.example.com/products/nothing-ear-2" },
                new ProductSearchResult { Name = "Sennheiser Momentum True Wireless 3", Price = "$249", Url = "https://store.example.com/products/sennheiser-momentum-tw-3" }
            ],
            ["galaxy tab"] =
            [
                new ProductSearchResult { Name = "Samsung Galaxy Tab S9 Ultra 14.6”", Price = "$1199", Url = "https://store.example.com/products/galaxy-tab-s9-ultra" },
                new ProductSearchResult { Name = "Samsung Galaxy Tab S9 FE", Price = "$499", Url = "https://store.example.com/products/galaxy-tab-s9-fe" },
                new ProductSearchResult { Name = "Samsung Galaxy Tab A9+", Price = "$299", Url = "https://store.example.com/products/galaxy-tab-a9-plus" }
            ],
            ["macbook"] =
            [
                new ProductSearchResult { Name = "MacBook Pro 16” M3 Max", Price = "$3499", Url = "https://store.example.com/products/macbook-pro-16-m3" },
                new ProductSearchResult { Name = "MacBook Air 13” M2", Price = "$1099", Url = "https://store.example.com/products/macbook-air-13-m2" },
                new ProductSearchResult { Name = "MacBook Air 15” M2", Price = "$1299", Url = "https://store.example.com/products/macbook-air-15-m2" }
            ],
            ["playstation"] =
            [
                new ProductSearchResult { Name = "PlayStation 5 (Disc Edition)", Price = "$499", Url = "https://store.example.com/products/ps5-disc" },
                new ProductSearchResult { Name = "PlayStation 5 Digital Edition", Price = "$449", Url = "https://store.example.com/products/ps5-digital" },
                new ProductSearchResult { Name = "DualSense Wireless Controller", Price = "$69", Url = "https://store.example.com/products/ps5-dualsense" }
            ],
            ["airpods"] =
            [
                new ProductSearchResult { Name = "Apple AirPods Pro (2nd Gen)", Price = "$249", Url = "https://store.example.com/products/airpods-pro-2" },
                new ProductSearchResult { Name = "Apple AirPods 3", Price = "$169", Url = "https://store.example.com/products/airpods-3" }
            ],
            ["kindle"] =
            [
                new ProductSearchResult { Name = "Kindle Paperwhite 11th Gen (8GB, 6.8\" Display)", Price = "$149", Url = "https://store.example.com/products/kindle-paperwhite-11th-gen" },
                new ProductSearchResult { Name = "Kindle Scribe (16GB, 10.2\" Display)", Price = "$339", Url = "https://store.example.com/products/kindle-scribe" },
                new ProductSearchResult { Name = "Kindle 10th Generation (2019)", Price = "$99", Url = "https://store.example.com/products/kindle-10th-gen" }
            ]
        };
}
