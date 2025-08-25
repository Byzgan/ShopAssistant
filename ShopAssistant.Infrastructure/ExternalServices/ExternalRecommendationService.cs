using ShopAssistant.Contracts.Interfaces.Integrations;
using ShopAssistant.Contracts.Models.Integrations;

namespace ShopAssistant.Infrastructure.ExternalServices;

/// <summary>
/// Dummy implementation of the recommendation service.
/// Returns sample products filtered by input fields, for demonstration/testing.
/// </summary>
public class ExternalRecommendationService : IRecommendationService
{
    /// <summary>
    /// Filters and returns sample products based on category, preference, and discount criteria for demonstration or testing purposes.
    /// </summary>
    public Task<List<RecommendationResult>> GetRecommendationsAsync(RecommendationRequest request, CancellationToken cancellationToken)
    {
        // Normalize for "no preference" (supporting English and Norwegian)
        var pref = (request.Preference).Trim().ToLowerInvariant();
        bool skipPreference = string.IsNullOrWhiteSpace(pref) || new[] { "no", "none", "n/a", "-", "nei", "ingen", "ikke", "uten" }.Contains(pref);

        var results = AllProducts
            .Where(p =>
                (string.IsNullOrWhiteSpace(request.Category) || string.Equals(p.Category, request.Category, StringComparison.OrdinalIgnoreCase)) &&
                (!request.DiscountOnly || p.Discount) &&
                (!request.Budget.HasValue || p.Price <= request.Budget.Value) &&
                (skipPreference || string.Equals(p.Preference, request.Preference, StringComparison.OrdinalIgnoreCase))
            )
            .Take(5)
            .Select(p => new RecommendationResult
            {
                Name = p.Name,
                Price = p.Price,
                Url = p.Url
            })
            .ToList();

        return Task.FromResult(results);
    }


    // For dummy filtering, extend RecommendationResult for mock data purposes
    private class Product : RecommendationResult
    {
        public string Category { get; init; } = string.Empty;
        public string Preference { get; init; } = string.Empty;
        public bool Discount { get; init; }
    }

    /// <summary>
    /// Returns the localized known category dictionary. Key = canonical, Value = synonyms.
    /// </summary>
    public Task<Dictionary<string, List<string>>> GetKnownCategoriesAsync(string lang)
    {
        lang = lang.ToLowerInvariant();
        if (lang.StartsWith("no"))
        {
            return Task.FromResult(new Dictionary<string, List<string>>
            {
                ["Smartphone"] = ["smarttelefon", "mobil", "telefon", "phone", "cell"],
                ["Tablet"] = ["nettbrett", "tablet", "ipad", "tab"],
                ["Smart Watch"] = ["smartklokke", "klokke", "watch", "smartwatch"],
                ["Fitness Tracker"] = ["aktivitetsmåler", "fitness", "tracker", "fitbit", "armbånd"],
                ["Headphones"] = ["hodetelefoner", "headphones", "headset", "ørepropper", "earbuds", "øretelefoner", "buds", "tws"],
                ["Smart Speaker"] = ["høyttaler", "speaker", "assistant", "smarthøyttaler"],
                ["Charger"] = ["lader", "charger", "ladestasjon", "charging"],
                ["Action Camera"] = ["kamera", "camera", "gopro", "actioncam", "action camera"],
                ["E-reader"] = ["lesebrett", "e-leser", "e-reader", "kindle"],
                ["Laptop"] = ["laptop", "bærbar", "notebook", "macbook", "pc"],
                ["Smart Camera"] = ["overvåkningskamera", "smart kamera", "security camera"],
                ["Projector"] = ["projektor", "projector"],
                ["Computer Accessory"] = ["tilbehør", "mus", "tastatur", "keyboard", "mouse", "accessory"],
                ["Smart Home"] = ["smarthus", "smart home", "lyspære", "bulb", "term", "thermostat", "smart plug"],
                ["VR Headset"] = ["vr", "virtual reality", "vr-headset", "vr headset"],
                ["Camera Accessory"] = ["gimbal", "kamera tilbehør", "stabilizer", "accessory"],
                ["Microphone"] = ["mikrofon", "microphone"],
                ["Speaker"] = ["høyttaler", "speaker", "bluetooth speaker"],
                ["Printer"] = ["printer", "skrivemaskin", "photo printer"],
                ["Storage"] = ["hdd", "ssd", "harddisk", "storage", "lagring"]
            });
        }
        
        // English
        return Task.FromResult(new Dictionary<string, List<string>>
        {
            ["Smartphone"] = ["smartphone", "phone", "cell", "mobile"],
            ["Tablet"] = ["tablet", "ipad", "tab"],
            ["Smart Watch"] = ["smart watch", "smartwatch", "smart-watch", "watch"],
            ["Fitness Tracker"] = ["fitness tracker", "fitness band", "tracker", "fitbit", "activity band"],
            ["Headphones"] = ["headphones", "headset", "over-ear", "earbuds", "buds", "earphones", "in-ear", "tws"],
            ["Smart Speaker"] = ["smart speaker", "assistant", "home speaker"],
            ["Charger"] = ["charger", "charging pad", "charging station"],
            ["Action Camera"] = ["action camera", "gopro", "actioncam"],
            ["E-reader"] = ["e-reader", "kindle", "ebook", "ereader"],
            ["Laptop"] = ["laptop", "notebook", "macbook", "chromebook", "ultrabook"],
            ["Smart Camera"] = ["smart camera", "security camera", "surveillance"],
            ["Projector"] = ["projector", "mini projector"],
            ["Computer Accessory"] = ["accessory", "mouse", "keyboard", "combo", "computer accessory"],
            ["Smart Home"] = ["smart home", "bulb", "plug", "thermostat", "light panel", "home automation"],
            ["VR Headset"] = ["vr headset", "virtual reality", "oculus"],
            ["Camera Accessory"] = ["camera accessory", "gimbal", "stabilizer"],
            ["Microphone"] = ["microphone", "mic"],
            ["Speaker"] = ["speaker", "bluetooth speaker"],
            ["Printer"] = ["printer", "photo printer"],
            ["Storage"] = ["storage", "hdd", "ssd", "hard disk", "drive"],
            ["computer"] = ["computer", "pc", "desktop", "workstation"],
            ["laptop"] = ["laptop", "notebook", "ultrabook", "macbook", "chromebook", "computer"]

        });
    }

    // Expanded electronics product catalog (mock data)
    private static readonly List<Product> AllProducts =
    [
        // Samsung
        new() { Name = "Samsung Galaxy S24 Ultra", Price = 1299.99m, Url = "https://shop.com/product/samsung-galaxy-s24-ultra", Category = "Smartphone", Discount = false, Preference = "top-rated" },
        new() { Name = "Samsung Galaxy S24", Price = 999.99m, Url = "https://shop.com/product/samsung-galaxy-s24", Category = "Smartphone", Discount = true, Preference = "trending" },
        new() { Name = "Samsung Galaxy A55 5G", Price = 449.99m, Url = "https://shop.com/product/samsung-galaxy-a55", Category = "Smartphone", Discount = true, Preference = "eco-friendly" },
        new() { Name = "Samsung Galaxy Z Fold5", Price = 1799.99m, Url = "https://shop.com/product/samsung-galaxy-z-fold5", Category = "Smartphone", Discount = false, Preference = "top-rated" },
        new() { Name = "Samsung Galaxy S23 FE", Price = 599.99m, Url = "https://shop.com/product/samsung-galaxy-s23-fe", Category = "Smartphone", Discount = true, Preference = "classic" },

        // Apple
        new() { Name = "Apple iPhone 15 Pro Max", Price = 1199.99m, Url = "https://shop.com/product/iphone-15-pro-max", Category = "Smartphone", Discount = false, Preference = "top-rated" },
        new() { Name = "Apple iPhone 15", Price = 799.99m, Url = "https://shop.com/product/iphone-15", Category = "Smartphone", Discount = true, Preference = "trending" },
        new() { Name = "Apple iPhone SE (3rd Gen)", Price = 429.99m, Url = "https://shop.com/product/iphone-se-2022", Category = "Smartphone", Discount = true, Preference = "eco-friendly" },
        new() { Name = "Apple iPhone 14", Price = 699.99m, Url = "https://shop.com/product/iphone-14", Category = "Smartphone", Discount = true, Preference = "classic" },

        // Google
        new() { Name = "Google Pixel 8 Pro", Price = 999.99m, Url = "https://shop.com/product/google-pixel-8-pro", Category = "Smartphone", Discount = false, Preference = "top-rated" },
        new() { Name = "Google Pixel 8a", Price = 499.99m, Url = "https://shop.com/product/google-pixel-8a", Category = "Smartphone", Discount = true, Preference = "eco-friendly" },

        // Xiaomi
        new() { Name = "Xiaomi 14 Ultra", Price = 999.99m, Url = "https://shop.com/product/xiaomi-14-ultra", Category = "Smartphone", Discount = false, Preference = "top-rated" },
        new() { Name = "Xiaomi Redmi Note 13 Pro", Price = 349.99m, Url = "https://shop.com/product/redmi-note-13-pro", Category = "Smartphone", Discount = true, Preference = "trending" },

        // OnePlus
        new() { Name = "OnePlus 12", Price = 799.99m, Url = "https://shop.com/product/oneplus-12", Category = "Smartphone", Discount = false, Preference = "top-rated" },
        new() { Name = "OnePlus Nord CE 4", Price = 329.99m, Url = "https://shop.com/product/oneplus-nord-ce-4", Category = "Smartphone", Discount = true, Preference = "eco-friendly" },

        // Oppo
        new() { Name = "OPPO Find X7 Ultra", Price = 1099.99m, Url = "https://shop.com/product/oppo-find-x7-ultra", Category = "Smartphone", Discount = false, Preference = "top-rated" },
        new() { Name = "OPPO Reno 12 Pro", Price = 549.99m, Url = "https://shop.com/product/oppo-reno-12-pro", Category = "Smartphone", Discount = true, Preference = "trending" },

        // Motorola
        new() { Name = "Motorola Edge 50 Pro", Price = 599.99m, Url = "https://shop.com/product/motorola-edge-50-pro", Category = "Smartphone", Discount = false, Preference = "classic" },
        new() { Name = "Motorola Moto G Power 5G (2024)", Price = 299.99m, Url = "https://shop.com/product/moto-g-power-5g", Category = "Smartphone", Discount = true, Preference = "eco-friendly" },

        // Apple iPad
        new() { Name = "Apple iPad Pro 13\" (M4)", Price = 1199.99m, Url = "https://shop.com/product/ipad-pro-13", Category = "Tablet", Discount = false, Preference = "top-rated" },
        new() { Name = "Apple iPad Air (M2)", Price = 599.99m, Url = "https://shop.com/product/ipad-air-m2", Category = "Tablet", Discount = true, Preference = "trending" },
        new() { Name = "Apple iPad 10th Gen", Price = 349.99m, Url = "https://shop.com/product/ipad-10th-gen", Category = "Tablet", Discount = true, Preference = "classic" },
        new() { Name = "Apple iPad Mini (6th Gen)", Price = 499.99m, Url = "https://shop.com/product/ipad-mini-6", Category = "Tablet", Discount = true, Preference = "eco-friendly" },

        // Samsung Galaxy Tab
        new() { Name = "Samsung Galaxy Tab S9 Ultra", Price = 1199.99m, Url = "https://shop.com/product/galaxy-tab-s9-ultra", Category = "Tablet", Discount = false, Preference = "top-rated" },
        new() { Name = "Samsung Galaxy Tab S9 FE", Price = 449.99m, Url = "https://shop.com/product/galaxy-tab-s9-fe", Category = "Tablet", Discount = true, Preference = "trending" },
        new() { Name = "Samsung Galaxy Tab A9+", Price = 229.99m, Url = "https://shop.com/product/galaxy-tab-a9-plus", Category = "Tablet", Discount = true, Preference = "eco-friendly" },

        // Lenovo
        new() { Name = "Lenovo Tab P12 Pro", Price = 499.99m, Url = "https://shop.com/product/lenovo-tab-p12-pro", Category = "Tablet", Discount = true, Preference = "classic" },
        new() { Name = "Lenovo Tab M11", Price = 169.99m, Url = "https://shop.com/product/lenovo-tab-m11", Category = "Tablet", Discount = true, Preference = "eco-friendly" },

        // Xiaomi
        new() { Name = "Xiaomi Pad 6", Price = 299.99m, Url = "https://shop.com/product/xiaomi-pad-6", Category = "Tablet", Discount = true, Preference = "trending" },

        // Huawei
        new() { Name = "Huawei MatePad Pro 13.2", Price = 899.99m, Url = "https://shop.com/product/huawei-matepad-pro-13", Category = "Tablet", Discount = false, Preference = "top-rated" },

        // --- TWS EARBUDS (REAL MODELS) ---
        new() { Name = "Apple AirPods Pro (2nd Gen)", Price = 249.99m, Url = "https://shop.com/product/airpods-pro-2", Category = "Earbuds", Discount = true, Preference = "top-rated" },
        new() { Name = "Samsung Galaxy Buds2 Pro", Price = 179.99m, Url = "https://shop.com/product/galaxy-buds2-pro", Category = "Earbuds", Discount = true, Preference = "trending" },
        new() { Name = "Sony WF-1000XM5", Price = 299.99m, Url = "https://shop.com/product/sony-wf-1000xm5", Category = "Earbuds", Discount = false, Preference = "top-rated" },
        new() { Name = "Jabra Elite 8 Active", Price = 199.99m, Url = "https://shop.com/product/jabra-elite-8", Category = "Earbuds", Discount = true, Preference = "trending" },
        new() { Name = "Nothing Ear (2)", Price = 129.99m, Url = "https://shop.com/product/nothing-ear-2", Category = "Earbuds", Discount = true, Preference = "eco-friendly" },
        new() { Name = "Xiaomi Redmi Buds 5 Pro", Price = 79.99m, Url = "https://shop.com/product/redmi-buds-5-pro", Category = "Earbuds", Discount = true, Preference = "classic" },

        // --- SMARTWATCHES (REAL MODELS) ---
        new() { Name = "Apple Watch Series 9", Price = 399.99m, Url = "https://shop.com/product/apple-watch-series-9", Category = "Smart Watch", Discount = false, Preference = "top-rated" },
        new() { Name = "Apple Watch SE (2nd Gen)", Price = 249.99m, Url = "https://shop.com/product/apple-watch-se-2", Category = "Smart Watch", Discount = true, Preference = "classic" },
        new() { Name = "Samsung Galaxy Watch6 Classic", Price = 399.99m, Url = "https://shop.com/product/galaxy-watch6-classic", Category = "Smart Watch", Discount = true, Preference = "trending" },
        new() { Name = "Samsung Galaxy Watch6", Price = 299.99m, Url = "https://shop.com/product/galaxy-watch6", Category = "Smart Watch", Discount = true, Preference = "eco-friendly" },
        new() { Name = "Garmin Venu 3", Price = 449.99m, Url = "https://shop.com/product/garmin-venu-3", Category = "Smart Watch", Discount = false, Preference = "top-rated" },
        new() { Name = "Garmin Forerunner 265", Price = 429.99m, Url = "https://shop.com/product/garmin-forerunner-265", Category = "Smart Watch", Discount = true, Preference = "trending" },
        new() { Name = "Fitbit Versa 4", Price = 199.99m, Url = "https://shop.com/product/fitbit-versa-4", Category = "Smart Watch", Discount = true, Preference = "classic" },
        new() { Name = "Fitbit Sense 2", Price = 299.99m, Url = "https://shop.com/product/fitbit-sense-2", Category = "Smart Watch", Discount = true, Preference = "eco-friendly" },
        new() { Name = "Huawei Watch GT 4", Price = 299.99m, Url = "https://shop.com/product/huawei-watch-gt-4", Category = "Smart Watch", Discount = false, Preference = "top-rated" },
        new() { Name = "Xiaomi Watch 2 Pro", Price = 269.99m, Url = "https://shop.com/product/xiaomi-watch-2-pro", Category = "Smart Watch", Discount = true, Preference = "trending" },
        new() { Name = "Amazfit GTR 4", Price = 199.99m, Url = "https://shop.com/product/amazfit-gtr-4", Category = "Smart Watch", Discount = true, Preference = "classic" },
        new() { Name = "Polar Vantage V3", Price = 599.99m, Url = "https://shop.com/product/polar-vantage-v3", Category = "Smart Watch", Discount = false, Preference = "top-rated" },
        
     ];
        

}
