namespace Artificer.Application.Retainer;

/// <summary>Uma oferta de mercado normalizada (independente da fonte: MB ao vivo ou Universalis).</summary>
public readonly record struct MarketOffering(long PricePerUnit, bool IsHq, ulong RetainerId);
