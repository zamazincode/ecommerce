import Header from "@/components/layout/header";
import { Footer } from "@/components/layout/footer";
import { CartPanel } from "@/components/cart/cart-panel";

// Mağaza header/footer/sepet paneli yalnızca (shop) ve (auth) rota
// gruplarında görünür — admin panelinin kendi tam ekran kabuğu var
// (bkz. app/admin/layout.tsx), mağaza header'ıyla iç içe girmemeli.
export default function ShopLayout({
	children,
}: Readonly<{
	children: React.ReactNode;
}>) {
	return (
		<>
			<Header />
			<div className="flex-1 min-h-screen">{children}</div>
			<Footer />
			<CartPanel />
		</>
	);
}
