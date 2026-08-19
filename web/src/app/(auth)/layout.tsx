import Header from "@/components/layout/header";
import { Footer } from "@/components/layout/footer";
import { CartPanel } from "@/components/cart/cart-panel";
import { AuthGate } from "@/components/layout/auth-gate";

export default function AuthLayout({
	children,
}: Readonly<{
	children: React.ReactNode;
}>) {
	return (
		<>
			<Header />
			{/*
			 * Giriş/kayıt/şifre sıfırlama/e-posta doğrulama sayfalarının
			 * paylaştığı ortak kabuk — kartı dikey+yatay ortalar. Yükseklik payı
			 * DevTools'ta ölçüldü: duyuru çubuğu + header sabit (~111px, <lg),
			 * lg'de kategori şeridi eklenince ~156px oluyor; mobilde ayrıca
			 * header altındaki arama şeridi var. Kırılım noktalarına göre üç
			 * ayrı değer kullanılıyor, `main`'in kendisi de landmark rolünü
			 * sayfaların eski `<main>`'inden devralıyor.
			 */}
			<main className="grid min-h-[calc(100vh-10.25rem)] place-items-center bg-surface px-4 py-12 md:min-h-[calc(100vh-7rem)] lg:min-h-[calc(100vh-9.75rem)]">
				<AuthGate>{children}</AuthGate>
			</main>
			<Footer />
			<CartPanel />
		</>
	);
}
