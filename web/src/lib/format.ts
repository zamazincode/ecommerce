const currencyFormatter = new Intl.NumberFormat("tr-TR", {
	style: "currency",
	currency: "TRY",
	minimumFractionDigits: 2,
	maximumFractionDigits: 2,
});

/** 1234.5 -> "₺1.234,50" */
export function formatPrice(amount: number): string {
	return currencyFormatter.format(amount);
}
