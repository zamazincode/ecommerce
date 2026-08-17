import { test, expect } from "@playwright/test";

test("misafir: arama → sepete ekle → adres adımına geç", async ({ page }) => {
	await page.goto("/arama?q=kitap");

	await page.getByRole("main").getByRole("link").first().click();

	await expect(page).toHaveURL(/\/urun\//);

	await page.getByRole("button", { name: "Sepete Ekle" }).click();
	await expect(page.getByText("1")).toBeVisible();

	await page
		.getByRole("link", { name: /ödemeye geç|sepeti onayla/i })
		.click();
	await expect(page).toHaveURL(/\/giris/); // misafir → giriş sayfasına yönlenmeli
});
