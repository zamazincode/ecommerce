namespace Commerce.Domain.Pricing;

/// SAF SINIF. Veritabanı yok, saat yok, log yok.
///
/// Bir toplamı ağırlıklara göre satırlara dağıtır ve dağıtılanların toplamının
/// hedefe BİREBİR eşit olmasını garanti eder (kuruş artığı en büyük satıra).
/// Ödeme sağlayıcıları sepet kalemleri toplamının tutara eşit olmasını zorunlu
/// tutar; 0.01'lik bir sapma bile isteğin reddedilmesine yeter (K1).
public static class AmountAllocator
{
    public static IReadOnlyList<decimal> Distribute(IReadOnlyList<decimal> weights, decimal total)
    {
        if (weights.Count == 0) return [];

        var sumOfWeights = weights.Sum();

        // Ağırlıkların toplamı sıfır/negatifse dağıtacak bir oran yok —
        // tamamı ilk satıra gider (tek kalem yazmaya zorlamamak için).
        if (sumOfWeights <= 0m)
        {
            var singleLine = new decimal[weights.Count];
            singleLine[0] = total;
            return singleLine;
        }

        var result = new decimal[weights.Count];
        for (var i = 0; i < weights.Count; i++)
        {
            var share = Math.Round(total * weights[i] / sumOfWeights, 2, MidpointRounding.AwayFromZero);
            // Negatif ağırlık girse bile satır negatif olmasın — fark en büyük
            // satıra taşınacağı için toplam yine de hedefe eşitlenir.
            result[i] = share < 0m ? 0m : share;
        }

        // Yuvarlama artığı (pozitif ya da negatif) en büyük paya sahip satıra
        // eklenir. Böylece Σresult == total HER ZAMAN sağlanır.
        var remainder = total - result.Sum();
        if (remainder != 0m)
        {
            // >= kullanılıyor: eşit paylı satırlarda artık SON satıra gider
            // (deterministik ve testle kilitli davranış).
            var largestIndex = 0;
            for (var i = 1; i < result.Length; i++)
                if (result[i] >= result[largestIndex]) largestIndex = i;

            result[largestIndex] += remainder;
            if (result[largestIndex] < 0m) result[largestIndex] = 0m;
        }

        return result;
    }
}
