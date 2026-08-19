// Kargo bedava eşiği — `api/src/Commerce.Domain/Pricing/ShippingCalculator.cs`
// içindeki `FreeShippingThreshold` (200₺) ile AYNI olmalı. Backend'e API
// çağrısı gerekmiyor, sabit — eşik neredeyse hiç değişmiyor ve değişirse
// zaten `ShippingCalculator` de birlikte güncellenir. Hem duyuru çubuğunda
// hem sepet panelinde/promosyon kartlarında kullanılıyor, tek yerden okunsun
// diye ortak bir modülde tutuluyor.
export const FREE_SHIPPING_THRESHOLD = 200;
