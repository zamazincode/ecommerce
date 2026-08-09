namespace Commerce.Domain.Common;

/// Domain katmanının iş kuralı ihlali. Commerce.Api bunu 400'e çevirir.
/// Domain, Api'nin exception tiplerini tanımaz — katman bağımsızlığı böyle korunur.
public class DomainRuleException(string message) : Exception(message);
