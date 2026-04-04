namespace StealthStockOverlay.Models;

public class QuoteItem
{
    public string Symbol { get; set; } = "";
    public string DisplaySymbol { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public decimal Price { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
}
