import {
	BookOpenIcon,
	PencilRulerIcon,
	PuzzleIcon,
	HeadphonesIcon,
	SofaIcon,
	Disc3Icon,
	TentIcon,
	TagIcon,
	type LucideIcon,
} from "lucide-react";

/**
 * Kategori slug → ikon eşlemesi. `CategoryTreeDto`'da görsel/ikon alanı YOK
 * (canlı API'de ölçüldü: `id, name, slug, displayOrder, children`) — bu
 * yüzden ikon eşlemesi burada, sabit bir frontend haritası olarak duruyor.
 *
 * Slug'lar `GET /api/categories/tree` çıktısından birebir alındı. `muzik-2`
 * gerçekten böyle yazılıyor (kategori ağacında bir slug çakışmasının çözümünden
 * kalma) — `muzik` yazarsan sayfa 404 döner.
 */
export const CATEGORY_ICONS: Record<string, LucideIcon> = {
	kitap: BookOpenIcon,
	kirtasiye: PencilRulerIcon,
	"hobi-oyuncak": PuzzleIcon,
	elektronik: HeadphonesIcon,
	"ev-yasam": SofaIcon,
	"muzik-2": Disc3Icon,
	"spor-outdoor": TentIcon,
};

/** Haritada olmayan bir slug gelirse (yeni kök kategori eklenirse) düşülecek ikon. */
export const CATEGORY_ICON_FALLBACK: LucideIcon = TagIcon;

export function getCategoryIcon(slug: string): LucideIcon {
	return CATEGORY_ICONS[slug] ?? CATEGORY_ICON_FALLBACK;
}
